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