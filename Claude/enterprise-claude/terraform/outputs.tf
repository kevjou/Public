output "alb_dns_name" {
  description = "ALB DNS name — point your app_domain CNAME here"
  value       = aws_lb.main.dns_name
}

output "alb_zone_id" {
  description = "ALB hosted zone ID — for Route 53 alias records"
  value       = aws_lb.main.zone_id
}

output "cognito_user_pool_id" {
  description = "Cognito User Pool ID"
  value       = aws_cognito_user_pool.main.id
}

output "cognito_user_pool_arn" {
  description = "Cognito User Pool ARN"
  value       = aws_cognito_user_pool.main.arn
}

output "cognito_app_client_id" {
  description = "Cognito App Client ID"
  value       = aws_cognito_user_pool_client.main.id
}

output "cognito_hosted_ui_url" {
  description = "Cognito hosted UI login URL"
  value       = "https://${aws_cognito_user_pool_domain.main.domain}.auth.${var.aws_region}.amazoncognito.com"
}

output "dynamodb_usage_table" {
  description = "DynamoDB usage tracking table name"
  value       = aws_dynamodb_table.usage.name
}

output "audit_bucket_name" {
  description = "S3 audit log bucket name"
  value       = aws_s3_bucket.audit.id
}

output "kms_key_arn" {
  description = "KMS key ARN — required for Anthropic BAA data-at-rest documentation"
  value       = aws_kms_key.main.arn
}

output "ecs_cluster_name" {
  description = "ECS cluster name"
  value       = aws_ecs_cluster.main.name
}

output "cloudwatch_dashboard_url" {
  description = "CloudWatch dashboard URL"
  value       = "https://${var.aws_region}.console.aws.amazon.com/cloudwatch/home?region=${var.aws_region}#dashboards:name=${local.name_prefix}"
}
