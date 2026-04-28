resource "aws_cognito_user_pool" "main" {
  name = "${local.name_prefix}-user-pool"

  # HIPAA: enforce MFA for all users
  mfa_configuration = var.mfa_configuration

  software_token_mfa_configuration {
    enabled = true
  }

  # HIPAA: strong password policy
  password_policy {
    minimum_length                   = 14
    require_lowercase                = true
    require_uppercase                = true
    require_numbers                  = true
    require_symbols                  = true
    temporary_password_validity_days = 1
  }

  account_recovery_setting {
    recovery_mechanism {
      name     = "verified_email"
      priority = 1
    }
  }

  # Prevent self-registration — only admins provision accounts
  admin_create_user_config {
    allow_admin_create_user_only = true

    invite_message_template {
      email_subject = "Your ${var.project_name} access"
      email_message = "Your temporary password is {####}. You must set a new password and enroll MFA on first login."
      sms_message   = "Your temporary password is {####}"
    }
  }

  # HIPAA: advanced security detects compromised credentials and anomalous sign-ins
  user_pool_add_ons {
    advanced_security_mode = "ENFORCED"
  }

  auto_verified_attributes = ["email"]

  schema {
    name                     = "email"
    attribute_data_type      = "String"
    required                 = true
    mutable                  = true
    string_attribute_constraints {
      min_length = 1
      max_length = 256
    }
  }

  schema {
    name                     = "department"
    attribute_data_type      = "String"
    required                 = false
    mutable                  = true
    string_attribute_constraints {
      min_length = 1
      max_length = 100
    }
  }

  tags = { HIPAAScope = "true" }
}

resource "aws_cognito_user_pool_client" "main" {
  name         = "${local.name_prefix}-app-client"
  user_pool_id = aws_cognito_user_pool.main.id

  generate_secret = true

  # SRP auth only — no username/password over the wire
  explicit_auth_flows = [
    "ALLOW_USER_SRP_AUTH",
    "ALLOW_REFRESH_TOKEN_AUTH"
  ]

  # Authorization code flow only — no implicit grant
  allowed_oauth_flows                  = ["code"]
  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_scopes                 = ["openid", "email", "profile"]
  callback_urls                        = ["https://${var.app_domain}/callback"]
  logout_urls                          = ["https://${var.app_domain}/logout"]
  supported_identity_providers         = ["COGNITO"]

  # HIPAA: short-lived tokens aligned to a standard workday
  access_token_validity  = 1  # hour
  id_token_validity      = 1  # hour
  refresh_token_validity = 8  # hours

  token_validity_units {
    access_token  = "hours"
    id_token      = "hours"
    refresh_token = "hours"
  }

  prevent_user_existence_errors = "ENABLED"

  read_attributes  = ["email", "custom:department"]
  write_attributes = ["email", "custom:department"]
}

resource "aws_cognito_user_pool_domain" "main" {
  domain       = "${local.name_prefix}-auth"
  user_pool_id = aws_cognito_user_pool.main.id
}
