terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

data "aws_caller_identity" "current" {}

# -------------------
# IAM Role para Lambdas
# -------------------
resource "aws_iam_role" "iam_for_lambda" {
  name = "ExecutionLambda"

  assume_role_policy = jsonencode({
    Version = "2012-10-17",
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
# Lambda Functions
# -------------------
locals {
  lambda_config = {
    create_request_card   = "create-request-card-lambda"
    card_activate         = "card-activate-lambda"
    card_purchase         = "card-purchase-lambda"
    card_transaction_save = "card-transaction-save-lambda"
    card_paid_credit_card = "card-paid-credit-card-lambda"
    card_get_report       = "card-get-report-lambda"
    card_request_failed   = "card-request-failed"
  }

  lambda_sqs_config = {
    create_card_sqs = "create-request-card-sqs-lambda"
  }
}

resource "aws_lambda_function" "lambdas" {
  for_each      = local.lambda_config
  function_name = each.value
  filename      = "${path.module}/../../publish/app.zip"
  handler       = "AWSTransactionApi::AWSTransactionApi.LambdaEntryPoint::FunctionHandlerAsync"
  runtime       = "dotnet8"
  role          = aws_iam_role.iam_for_lambda.arn
  memory_size   = 512
  timeout       = 900
  publish       = true
  source_code_hash = filebase64sha256("${path.module}/../../publish/app.zip")
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
# /Transaction/card (base path)
# ==========================
resource "aws_api_gateway_resource" "CardResource" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.TransactionResource.id
  path_part   = "card"
}

# ==========================
# /Transaction/transactions (base path para transacciones)
# ==========================
resource "aws_api_gateway_resource" "TransactionsResource" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.TransactionResource.id
  path_part   = "transactions"
}

# -------------------
# Endpoints -> Lambdas
# -------------------

# 1) POST /Transaction/card/create -> create_request_card
resource "aws_api_gateway_resource" "CardCreate" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.CardResource.id
  path_part   = "create"
}

resource "aws_api_gateway_method" "PostCardCreate" {
  rest_api_id   = aws_api_gateway_rest_api.CardApi.id
  resource_id   = aws_api_gateway_resource.CardCreate.id
  http_method   = "POST"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "IntegrationPostCardCreate" {
  rest_api_id             = aws_api_gateway_rest_api.CardApi.id
  resource_id             = aws_api_gateway_resource.CardCreate.id
  http_method             = aws_api_gateway_method.PostCardCreate.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.lambdas["create_request_card"].invoke_arn
}

resource "aws_lambda_permission" "ApiGatewayCardCreate" {
  statement_id  = "AllowExecutionFromAPIGatewayCardCreate"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.lambdas["create_request_card"].function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.CardApi.execution_arn}/*/POST/Transaction/card/create"
}

# 2) POST /Transaction/card/activate -> card_activate
resource "aws_api_gateway_resource" "CardActivate" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.CardResource.id
  path_part   = "activate"
}

resource "aws_api_gateway_method" "PostCardActivate" {
  rest_api_id   = aws_api_gateway_rest_api.CardApi.id
  resource_id   = aws_api_gateway_resource.CardActivate.id
  http_method   = "POST"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "IntegrationPostCardActivate" {
  rest_api_id             = aws_api_gateway_rest_api.CardApi.id
  resource_id             = aws_api_gateway_resource.CardActivate.id
  http_method             = aws_api_gateway_method.PostCardActivate.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.lambdas["card_activate"].invoke_arn
}

resource "aws_lambda_permission" "ApiGatewayCardActivate" {
  statement_id  = "AllowExecutionFromAPIGatewayCardActivate"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.lambdas["card_activate"].function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.CardApi.execution_arn}/*/POST/Transaction/card/activate"
}

# 3) POST /Transaction/transactions/purchase -> card_purchase
resource "aws_api_gateway_resource" "TransactionsPurchase" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.TransactionsResource.id
  path_part   = "purchase"
}

resource "aws_api_gateway_method" "PostTransactionsPurchase" {
  rest_api_id   = aws_api_gateway_rest_api.CardApi.id
  resource_id   = aws_api_gateway_resource.TransactionsPurchase.id
  http_method   = "POST"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "IntegrationTransactionsPurchase" {
  rest_api_id             = aws_api_gateway_rest_api.CardApi.id
  resource_id             = aws_api_gateway_resource.TransactionsPurchase.id
  http_method             = aws_api_gateway_method.PostTransactionsPurchase.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.lambdas["card_purchase"].invoke_arn
}

resource "aws_lambda_permission" "ApiGatewayTransactionsPurchase" {
  statement_id  = "AllowExecutionFromAPIGatewayTransactionsPurchase"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.lambdas["card_purchase"].function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.CardApi.execution_arn}/*/POST/Transaction/transactions/purchase"
}

# 4) POST /Transaction/transactions/save/{cardId} -> card_transaction_save
resource "aws_api_gateway_resource" "TransactionsSave" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.TransactionsResource.id
  path_part   = "save"
}

resource "aws_api_gateway_resource" "TransactionsSaveCardId" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.TransactionsSave.id
  path_part   = "{cardId}"
}

resource "aws_api_gateway_method" "PostTransactionsSave" {
  rest_api_id   = aws_api_gateway_rest_api.CardApi.id
  resource_id   = aws_api_gateway_resource.TransactionsSaveCardId.id
  http_method   = "POST"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "IntegrationTransactionsSave" {
  rest_api_id             = aws_api_gateway_rest_api.CardApi.id
  resource_id             = aws_api_gateway_resource.TransactionsSaveCardId.id
  http_method             = aws_api_gateway_method.PostTransactionsSave.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.lambdas["card_transaction_save"].invoke_arn
}

resource "aws_lambda_permission" "ApiGatewayTransactionsSave" {
  statement_id  = "AllowExecutionFromAPIGatewayTransactionsSave"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.lambdas["card_transaction_save"].function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.CardApi.execution_arn}/*/POST/Transaction/transactions/save/*"
}

# 5) POST /Transaction/card/paid/{cardId} -> card_paid_credit_card
resource "aws_api_gateway_resource" "CardPaid" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.CardResource.id
  path_part   = "paid"
}

resource "aws_api_gateway_resource" "CardPaidCardId" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.CardPaid.id
  path_part   = "{cardId}"
}

resource "aws_api_gateway_method" "PostCardPaid" {
  rest_api_id   = aws_api_gateway_rest_api.CardApi.id
  resource_id   = aws_api_gateway_resource.CardPaidCardId.id
  http_method   = "POST"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "IntegrationCardPaid" {
  rest_api_id             = aws_api_gateway_rest_api.CardApi.id
  resource_id             = aws_api_gateway_resource.CardPaidCardId.id
  http_method             = aws_api_gateway_method.PostCardPaid.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.lambdas["card_paid_credit_card"].invoke_arn
}

resource "aws_lambda_permission" "ApiGatewayCardPaid" {
  statement_id  = "AllowExecutionFromAPIGatewayCardPaid"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.lambdas["card_paid_credit_card"].function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.CardApi.execution_arn}/*/POST/Transaction/card/paid/*"
}

# 6) GET /Transaction/card/{cardId} -> card_get_report
resource "aws_api_gateway_resource" "CardReport" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id
  parent_id   = aws_api_gateway_resource.CardResource.id
  path_part   = "{cardId}"
}

resource "aws_api_gateway_method" "GetCardReport" {
  rest_api_id   = aws_api_gateway_rest_api.CardApi.id
  resource_id   = aws_api_gateway_resource.CardReport.id
  http_method   = "GET"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "IntegrationCardReport" {
  rest_api_id             = aws_api_gateway_rest_api.CardApi.id
  resource_id             = aws_api_gateway_resource.CardReport.id
  http_method             = aws_api_gateway_method.GetCardReport.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.lambdas["card_get_report"].invoke_arn
}

resource "aws_lambda_permission" "ApiGatewayCardReport" {
  statement_id  = "AllowExecutionFromAPIGatewayCardReport"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.lambdas["card_get_report"].function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.CardApi.execution_arn}/*/GET/Transaction/card/*"
}

# 7) card-request-failed (no expuesto en API, ligado a SQS o DLQ)
# (puedes engancharlo a un trigger SQS más adelante)
# ====================================================

# ==========================
# Deployment & Stage
# ==========================
resource "aws_api_gateway_deployment" "CardApiDeployment" {
  rest_api_id = aws_api_gateway_rest_api.CardApi.id

  lifecycle {
    create_before_destroy = true
  }

  triggers = {
    redeploy = timestamp()
  }

  depends_on = [
    aws_api_gateway_integration.IntegrationPostCardCreate,
    aws_api_gateway_integration.IntegrationPostCardActivate,
    aws_api_gateway_integration.IntegrationTransactionsPurchase,
    aws_api_gateway_integration.IntegrationTransactionsSave,
    aws_api_gateway_integration.IntegrationCardPaid,
    aws_api_gateway_integration.IntegrationCardReport
  ]
}

resource "aws_api_gateway_stage" "ProdStage" {
  deployment_id = aws_api_gateway_deployment.CardApiDeployment.id
  rest_api_id   = aws_api_gateway_rest_api.CardApi.id
  stage_name    = "prod"
}

# ==========================
# Output URLs
# ==========================
output "api_url_card_create" {
  value = "https://${aws_api_gateway_rest_api.CardApi.id}.execute-api.${var.aws_region}.amazonaws.com/${aws_api_gateway_stage.ProdStage.stage_name}/Transaction/card/create"
}

output "api_url_card_activate" {
  value = "https://${aws_api_gateway_rest_api.CardApi.id}.execute-api.${var.aws_region}.amazonaws.com/${aws_api_gateway_stage.ProdStage.stage_name}/Transaction/card/activate"
}

output "api_url_card_purchase" {
  value = "https://${aws_api_gateway_rest_api.CardApi.id}.execute-api.${var.aws_region}.amazonaws.com/${aws_api_gateway_stage.ProdStage.stage_name}/Transaction/transactions/purchase"
}

output "api_url_card_save" {
  value = "https://${aws_api_gateway_rest_api.CardApi.id}.execute-api.${var.aws_region}.amazonaws.com/${aws_api_gateway_stage.ProdStage.stage_name}/Transaction/transactions/save/{cardId}"
}

output "api_url_card_paid" {
  value = "https://${aws_api_gateway_rest_api.CardApi.id}.execute-api.${var.aws_region}.amazonaws.com/${aws_api_gateway_stage.ProdStage.stage_name}/Transaction/card/paid/{cardId}"
}

output "api_url_card_report" {
  value = "https://${aws_api_gateway_rest_api.CardApi.id}.execute-api.${var.aws_region}.amazonaws.com/${aws_api_gateway_stage.ProdStage.stage_name}/Transaction/card/{cardId}"
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

# ==========================
# DynamoDB Cards-Error Table
# ==========================
resource "aws_dynamodb_table" "cards_error" {
  name           = "card-table-error"
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
# DynamoDB Policy
# ==========================

resource "aws_iam_policy" "dynamodb_policy" {
  name = "TransactionApiDynamoDBPolicy"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = [
          "dynamodb:PutItem",
          "dynamodb:GetItem",
          "dynamodb:UpdateItem",
          "dynamodb:DeleteItem",
          "dynamodb:Query",
          "dynamodb:Scan",
          "dynamodb:DescribeTable"
        ]
        Resource = [
          aws_dynamodb_table.cards.arn,
          aws_dynamodb_table.transactions.arn,
          aws_dynamodb_table.cards_error.arn
        ]
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "lambda_dynamodb" {
  role       = aws_iam_role.iam_for_lambda.name
  policy_arn = aws_iam_policy.dynamodb_policy.arn
}

resource "aws_iam_policy" "lambda_dynamodb_users" {
  name   = "LambdaDynamoDBUsersPolicy"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = [
          "dynamodb:Scan",
          "dynamodb:DescribeTable"
        ]
        Resource = "arn:aws:dynamodb:${var.aws_region}:${data.aws_caller_identity.current.account_id}:table/users"
      }
    ]
  })
}

resource "aws_iam_policy_attachment" "attach_lambda_dynamodb_users" {
  name       = "AttachLambdaDynamoDBUsersPolicy"
  roles      = [aws_iam_role.iam_for_lambda.name]
  policy_arn = aws_iam_policy.lambda_dynamodb_users.arn
}

# ==========================
# S3 Bucket
# ==========================
resource "aws_s3_bucket" "transaction_bucket" {
  bucket = "bucket-${var.bucket_name}"

  tags = {
    Service     = "TransactionApi"
    Environment = var.bucket_name
    Team        = "Backend"
    Owner       = "Sebastian"
  }
}

resource "aws_iam_policy" "transaction_bucket_policy" {
  name   = "TransactionBucketPolicy"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect : "Allow"
        Action : [
          "s3:PutObject",
          "s3:DeleteObject"
        ]
        Resource : "${aws_s3_bucket.transaction_bucket.arn}/*"
      }
    ]
  })
}

# Lambda SQS
resource "aws_lambda_function" "lambdas_sqs" {
  for_each      = local.lambda_sqs_config
  function_name = each.value
  filename      = "${path.module}/../../publish/app.zip"
  handler       = "AWSTransactionApi::AWSTransactionApi.CreateRequestCardLambda::Handler"
  runtime       = "dotnet8"
  role          = aws_iam_role.iam_for_lambda.arn
  memory_size   = 512
  timeout       = 900
  publish       = true
  source_code_hash = filebase64sha256("${path.module}/../../publish/app.zip")
}

# ==========================
# Dead Letter Queue (DLQ)
# ==========================
resource "aws_sqs_queue" "transaction_dlq" {
  name = var.dlq_name

  message_retention_seconds = 3600
}

# ==========================
# SQS Queue principal
# ==========================
resource "aws_sqs_queue" "transaction_queue" {
  name                        = var.sqs_name
  fifo_queue                  = false
  visibility_timeout_seconds  = 900
  content_based_deduplication = false
  receive_wait_time_seconds   = 10

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.transaction_dlq.arn
    maxReceiveCount     = 5
  })
}

# ==========================
# Lambda Policy for SQS
# ==========================

resource "aws_iam_policy" "lambda_sqs_policy" {
  name = "LambdaSQSPolicy"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:GetQueueAttributes",
          "sqs:SendMessage"
        ]
        Resource = aws_sqs_queue.transaction_queue.arn
      },
      {
        Effect = "Allow"
        Action = [
          "sqs:SendMessage"
        ]
        Resource = "arn:aws:sqs:us-east-2:346225465782:notification-email-sqs"
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "lambda_sqs_attach" {
  role       = aws_iam_role.iam_for_lambda.name
  policy_arn = aws_iam_policy.lambda_sqs_policy.arn
}

resource "aws_iam_role_policy_attachment" "lambda_s3_attach" {
  role       = aws_iam_role.iam_for_lambda.name
  policy_arn = aws_iam_policy.transaction_bucket_policy.arn
}




# ==========================
# Lambda Event Source Mapping (SQS -> Lambda)
# ==========================
resource "aws_lambda_event_source_mapping" "create_card_sqs_trigger" {
  event_source_arn  = aws_sqs_queue.transaction_queue.arn
  function_name     = aws_lambda_function.lambdas_sqs["create_card_sqs"].arn
  batch_size        = 1
  enabled           = true
}