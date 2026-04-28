locals {
  audit_log_expiry_days = var.audit_log_retention_years * 365
}

# ── S3: Audit Log Archive ────────────────────────────────────────────────────
# Stores CloudTrail + application audit events. HIPAA requires 6-year retention.

resource "random_id" "audit_bucket_suffix" {
  byte_length = 4
}

resource "aws_s3_bucket" "audit" {
  bucket = "${local.name_prefix}-audit-${random_id.audit_bucket_suffix.hex}"

  # Prevent accidental deletion of compliance-critical data
  lifecycle {
    prevent_destroy = true
  }

  tags = { HIPAAScope = "true", Purpose = "audit-logs" }
}

resource "aws_s3_bucket_versioning" "audit" {
  bucket = aws_s3_bucket.audit.id
  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "audit" {
  bucket = aws_s3_bucket.audit.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm     = "aws:kms"
      kms_master_key_id = aws_kms_key.main.arn
    }
    bucket_key_enabled = true
  }
}

resource "aws_s3_bucket_public_access_block" "audit" {
  bucket                  = aws_s3_bucket.audit.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_lifecycle_configuration" "audit" {
  bucket = aws_s3_bucket.audit.id

  rule {
    id     = "hipaa-retention"
    status = "Enabled"

    transition {
      days          = 90
      storage_class = "STANDARD_IA"
    }

    transition {
      days          = 365
      storage_class = "GLACIER"
    }

    expiration {
      days = local.audit_log_expiry_days
    }
  }
}

# Deny HTTP access — all audit log reads/writes must use HTTPS
resource "aws_s3_bucket_policy" "audit" {
  bucket = aws_s3_bucket.audit.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "DenyHTTP"
        Effect    = "Deny"
        Principal = "*"
        Action    = "s3:*"
        Resource  = ["${aws_s3_bucket.audit.arn}", "${aws_s3_bucket.audit.arn}/*"]
        Condition = {
          Bool = { "aws:SecureTransport" = "false" }
        }
      },
      {
        Sid       = "AllowCloudTrailWrite"
        Effect    = "Allow"
        Principal = { Service = "cloudtrail.amazonaws.com" }
        Action    = "s3:PutObject"
        Resource  = "${aws_s3_bucket.audit.arn}/cloudtrail/*"
        Condition = {
          StringEquals = {
            "s3:x-amz-acl"               = "bucket-owner-full-control"
            "aws:SourceAccount"           = data.aws_caller_identity.current.account_id
          }
        }
      },
      {
        Sid       = "AllowCloudTrailAclCheck"
        Effect    = "Allow"
        Principal = { Service = "cloudtrail.amazonaws.com" }
        Action    = "s3:GetBucketAcl"
        Resource  = aws_s3_bucket.audit.arn
      }
    ]
  })

  depends_on = [aws_s3_bucket_public_access_block.audit]
}

# ── DynamoDB: Usage Tracking ─────────────────────────────────────────────────
# Tracks token consumption per user for cost allocation and audit.

resource "aws_dynamodb_table" "usage" {
  name         = "${local.name_prefix}-usage"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "user_id"
  range_key    = "timestamp"

  attribute {
    name = "user_id"
    type = "S"
  }

  attribute {
    name = "timestamp"
    type = "S"
  }

  attribute {
    name = "department"
    type = "S"
  }

  global_secondary_index {
    name            = "department-timestamp-index"
    hash_key        = "department"
    range_key       = "timestamp"
    projection_type = "ALL"
  }

  server_side_encryption {
    enabled     = true
    kms_key_arn = aws_kms_key.main.arn
  }

  point_in_time_recovery {
    enabled = true
  }

  ttl {
    attribute_name = "expires_at"
    enabled        = true
  }

  tags = { Purpose = "usage-tracking" }
}
