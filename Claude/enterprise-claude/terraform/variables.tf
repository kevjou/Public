variable "aws_region" {
  description = "AWS region. Must be on the HIPAA-eligible services list."
  type        = string
  default     = "us-east-1"

  validation {
    condition = contains([
      "us-east-1", "us-east-2", "us-west-1", "us-west-2",
      "eu-west-1", "eu-central-1", "ap-southeast-1",
      "ap-southeast-2", "ap-northeast-1", "ca-central-1"
    ], var.aws_region)
    error_message = "Region must be HIPAA-eligible."
  }
}

variable "environment" {
  description = "Deployment environment"
  type        = string
  default     = "prod"

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Must be dev, staging, or prod."
  }
}

variable "project_name" {
  description = "Project name prefix for all resource names"
  type        = string
  default     = "enterprise-claude"
}

variable "vpc_cidr" {
  description = "CIDR block for the VPC"
  type        = string
  default     = "10.0.0.0/16"
}

variable "availability_zones" {
  description = "Availability zones to deploy into — minimum 2 for HA"
  type        = list(string)
  default     = ["us-east-1a", "us-east-1b"]
}

variable "proxy_image" {
  description = "ECR image URI for the Claude proxy service (e.g. 123456789.dkr.ecr.us-east-1.amazonaws.com/claude-proxy:latest)"
  type        = string
}

variable "proxy_cpu" {
  description = "Fargate task vCPU units (256, 512, 1024, 2048, 4096)"
  type        = number
  default     = 512
}

variable "proxy_memory" {
  description = "Fargate task memory in MiB"
  type        = number
  default     = 1024
}

variable "proxy_desired_count" {
  description = "Steady-state number of proxy tasks"
  type        = number
  default     = 2
}

variable "proxy_min_count" {
  description = "Auto-scaling minimum task count"
  type        = number
  default     = 2
}

variable "proxy_max_count" {
  description = "Auto-scaling maximum task count"
  type        = number
  default     = 10
}

variable "anthropic_api_key_secret_arn" {
  description = "Secrets Manager ARN for the Anthropic API key (create this before applying)"
  type        = string
  sensitive   = true
}

variable "acm_certificate_arn" {
  description = "ACM certificate ARN for HTTPS on the ALB"
  type        = string
}

variable "app_domain" {
  description = "Public domain for the application (e.g. claude-internal.yourcompany.com)"
  type        = string
}

variable "alert_email" {
  description = "Email address for CloudWatch alarm notifications"
  type        = string
}

variable "log_retention_days" {
  description = "CloudWatch log retention in days. HIPAA minimum is 6 years (2190 days)."
  type        = number
  default     = 2190
}

variable "audit_log_retention_years" {
  description = "S3 audit log retention in years for HIPAA (minimum 6)"
  type        = number
  default     = 6
}

variable "mfa_configuration" {
  description = "Cognito MFA setting — ON enforces for all users (HIPAA recommended)"
  type        = string
  default     = "ON"

  validation {
    condition     = contains(["ON", "OPTIONAL", "OFF"], var.mfa_configuration)
    error_message = "Must be ON, OPTIONAL, or OFF."
  }
}

variable "waf_rate_limit" {
  description = "Max requests per 5-minute window per IP address"
  type        = number
  default     = 500
}
