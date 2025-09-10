variable "aws_region" {
  description = "Región de AWS"
  type        = string
  default     = "us-east-2"
}

variable "lambda_name" {
  description = "Nombre de la Lambda"
  type        = string
  default     = "dotnet-api-lambda"
}

variable "aws_access_key" {
  description = "AWS Access Key"
  type        = string
  default     = ""
}

variable "aws_secret_key" {
  description = "AWS Secret Key"
  type        = string
  default     = ""
}

variable "bucket_name" {
  description = "name of bucket"
  type        = string
  default     = "cardse3013575"
}

variable "sqs_name" {
  description = "name of sqs"
  type        = string
  default     = "create-request-card-sqs"
}

variable "dlq_name" {
  description = "name of dlq"
  type        = string
  default     = "create-request-card-dlq"
}