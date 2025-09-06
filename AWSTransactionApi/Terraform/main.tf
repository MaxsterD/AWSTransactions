terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}
# -------------------
# IAM Role para Lambda
# -------------------
resource "aws_iam_role" "iam_for_lambda" {
  name = "ExecutionLambda"

  assume_role_policy = jsonencode({
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "lambda_basic_execution" {
  role       = aws_iam_role.iam_for_lambda.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

# -------------------
# Lambda Function
# -------------------
resource "aws_lambda_function" "dotnet_api" {
  function_name = var.lambda_name
  filename      = "${path.module}/../../publish/app.zip"
  handler       = "AWSTransactionApi::AWSTransactionApi.LambdaEntryPoint::FunctionHandlerAsync"
  runtime       = "dotnet8"
  role          = aws_iam_role.iam_for_lambda.arn
  memory_size   = 512
  timeout       = 900
  publish       = true

  source_code_hash = filebase64sha256("${path.module}/../../publish/app.zip")

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT = "Production"
    }
  }

  depends_on = [aws_iam_role_policy_attachment.lambda_basic_execution]
}

# ==========================
# API Gateway REST API
# ==========================
resource "aws_api_gateway_rest_api" "CardApi" {
  name        = "CardApi"
  description = "API para manejar tarjetas"
}

# ==========================
# Resource /Transaction
# ==========================
resource "aws_api_gateway_resource" "TransactionResource" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_rest_api.CardApi.root_resource_id
  path_part   = "Transaction"
}

# ==========================
# Resource /Transaction/card
# ==========================
resource "aws_api_gateway_resource" "CardResource" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.TransactionResource.id
  path_part   = "card"
}

# ==========================
# Resource /Transaction/card/activate
# ==========================
resource "aws_api_gateway_resource" "CardActivate" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.CardResource.id
  path_part   = "activate"
}

# ==========================
# Method POST /card/activate
# ==========================
resource "aws_api_gateway_method" "PostCardActivate" {
  rest_api_id   = aws_api_gateway_rest_api.CardApi.id
  resource_id   = aws_api_gateway_resource.CardActivate.id
  http_method   = "POST"
  authorization = "NONE"
}

# ==========================
# Integration POST /card/activate -> Lambda
# ==========================
resource "aws_api_gateway_integration" "IntegrationPostCardActivate" {
  rest_api_id             = aws_api_gateway_rest_api.CardApi.id
  resource_id             = aws_api_gateway_resource.CardActivate.id
  http_method             = aws_api_gateway_method.PostCardActivate.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.dotnet_api.invoke_arn
}

# ==========================
# Permisos Lambda -> API Gateway
# ==========================
resource "aws_lambda_permission" "ApiGatewayCardActivate" {
  statement_id  = "AllowExecutionFromAPIGatewayCardActivate"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.dotnet_api.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.CardApi.execution_arn}/*/POST/Transaction/card/activate"
}

# ==========================
# Deployment
# ==========================
resource "aws_api_gateway_deployment" "CardApiDeployment" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id

  depends_on = [
    aws_api_gateway_integration.IntegrationPostCardActivate,
    aws_lambda_permission.ApiGatewayCardActivate
  ]
}

# ==========================
# Stage (prod)
# ==========================
resource "aws_api_gateway_stage" "ProdStage" {
  deployment_id = aws_api_gateway_deployment.CardApiDeployment.id
  rest_api_id   = aws_api_gateway_rest_api.CardApi.id
  stage_name    = "prod"
}

# ==========================
# Output URL
# ==========================
output "api_url_card_activate" {
  value = "https://${aws_api_gateway_rest_api.CardApi.id}.execute-api.${var.aws_region}.amazonaws.com/${aws_api_gateway_stage.ProdStage.stage_name}/Transaction/card/activate"
}

# ==========================
# DynamoDB Cards Table
# ==========================
resource "aws_dynamodb_table" "cards" {
  name           = "cards"
  billing_mode   = "PROVISIONED"
  read_capacity  = 20
  write_capacity = 20

  hash_key  = "uuid"
  range_key = "createdAt"

  attribute {
    name = "uuid"
    type = "S"
  }

  attribute {
    name = "createdAt"
    type = "S"
  }

  tags = {
    Service     = "TransactionApi"
    Environment = "prod"
    Team        = "Backend"
    Owner       = "Sebastian"
  }
}

# ==========================
# DynamoDB Transactions Table
# ==========================
resource "aws_dynamodb_table" "transactions" {
  name           = "transactions"
  billing_mode   = "PROVISIONED"
  read_capacity  = 20
  write_capacity = 20

  hash_key  = "uuid"
  range_key = "createdAt"

  attribute {
    name = "uuid"
    type = "S"
  }

  attribute {
    name = "createdAt"
    type = "S"
  }

  tags = {
    Service     = "TransactionApi"
    Environment = "prod"
    Team        = "Backend"
    Owner       = "Sebastian"
  }
}
