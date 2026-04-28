import json
import logging
import os
import time
from contextlib import asynccontextmanager
from datetime import datetime, timezone
from typing import Any

import boto3
import httpx
from fastapi import FastAPI, HTTPException, Request, Security
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from jose import JWTError, jwk, jwt
from jose.utils import base64url_decode

logger = logging.getLogger("claude-proxy")
logging.basicConfig(
    level=os.getenv("LOG_LEVEL", "INFO"),
    format='{"time": "%(asctime)s", "level": "%(levelname)s", "message": %(message)s}',
)

REGION = os.environ["AWS_REGION"]
USER_POOL_ID = os.environ["COGNITO_USER_POOL_ID"]
DYNAMODB_TABLE = os.environ["DYNAMODB_TABLE"]
ANTHROPIC_API_KEY = os.environ["ANTHROPIC_API_KEY"]
ANTHROPIC_BASE_URL = "https://api.anthropic.com"

JWKS_URL = f"https://cognito-idp.{REGION}.amazonaws.com/{USER_POOL_ID}/.well-known/jwks.json"

dynamodb = boto3.resource("dynamodb", region_name=REGION)
usage_table = dynamodb.Table(DYNAMODB_TABLE)

_jwks_cache: dict = {}
_http_client: httpx.AsyncClient | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global _http_client, _jwks_cache
    async with httpx.AsyncClient(timeout=30.0) as client:
        _http_client = client
        response = await client.get(JWKS_URL)
        response.raise_for_status()
        _jwks_cache = response.json()
        logger.info('"JWKS loaded, proxy ready"')
        yield
    _http_client = None


app = FastAPI(title="Claude Proxy", lifespan=lifespan)
bearer = HTTPBearer()


def _get_public_key(token: str) -> Any:
    headers = jwt.get_unverified_headers(token)
    kid = headers.get("kid")
    for key in _jwks_cache.get("keys", []):
        if key["kid"] == kid:
            return jwk.construct(key)
    raise HTTPException(status_code=401, detail="Signing key not found")


def verify_token(credentials: HTTPAuthorizationCredentials = Security(bearer)) -> dict:
    token = credentials.credentials
    try:
        public_key = _get_public_key(token)
        message, encoded_sig = token.rsplit(".", 1)
        decoded_sig = base64url_decode(encoded_sig.encode())
        if not public_key.verify(message.encode(), decoded_sig):
            raise HTTPException(status_code=401, detail="Invalid token signature")

        claims = jwt.get_unverified_claims(token)
        if claims.get("token_use") != "access":
            raise HTTPException(status_code=401, detail="Wrong token type")

        exp = claims.get("exp", 0)
        if time.time() > exp:
            raise HTTPException(status_code=401, detail="Token expired")

        return claims

    except JWTError as e:
        _audit_log("auth_failure", {"reason": str(e)})
        raise HTTPException(status_code=401, detail="Invalid token")


def _audit_log(event: str, data: dict) -> None:
    logger.info(json.dumps({"event": event, **data}))


def _log_usage(user_id: str, department: str, model: str, usage: dict) -> None:
    input_tokens = usage.get("input_tokens", 0)
    output_tokens = usage.get("output_tokens", 0)
    cache_read = usage.get("cache_read_input_tokens", 0)
    cache_write = usage.get("cache_creation_input_tokens", 0)

    # Approximate cost in USD based on Sonnet 4.x pricing
    cost = (input_tokens * 3 + output_tokens * 15 + cache_read * 0.3 + cache_write * 3.75) / 1_000_000

    usage_table.put_item(Item={
        "user_id":        user_id,
        "timestamp":      datetime.now(timezone.utc).isoformat(),
        "department":     department or "unknown",
        "model":          model,
        "input_tokens":   input_tokens,
        "output_tokens":  output_tokens,
        "cache_read_tokens": cache_read,
        "cost_usd":       str(round(cost, 8)),
    })


@app.get("/health")
async def health():
    return {"status": "ok"}


@app.post("/v1/messages")
async def proxy_messages(request: Request, claims: dict = Security(verify_token)):
    user_id = claims.get("sub", "unknown")
    department = claims.get("custom:department", "unknown")

    body = await request.json()
    model = body.get("model", "unknown")

    _audit_log("request", {"user_id": user_id, "department": department, "model": model})

    headers = {
        "x-api-key":         ANTHROPIC_API_KEY,
        "anthropic-version": "2023-06-01",
        "content-type":      "application/json",
    }

    response = await _http_client.post(
        f"{ANTHROPIC_BASE_URL}/v1/messages",
        json=body,
        headers=headers,
        timeout=120.0,
    )

    if response.status_code != 200:
        _audit_log("upstream_error", {
            "user_id": user_id,
            "status":  response.status_code,
            "model":   model,
        })
        raise HTTPException(status_code=response.status_code, detail=response.text)

    result = response.json()
    usage = result.get("usage", {})

    try:
        _log_usage(user_id, department, model, usage)
    except Exception:
        logger.exception('"Usage logging failed — request still succeeded"')

    _audit_log("response", {
        "user_id":       user_id,
        "model":         model,
        "input_tokens":  usage.get("input_tokens"),
        "output_tokens": usage.get("output_tokens"),
    })

    return result
