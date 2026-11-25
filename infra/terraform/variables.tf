variable "subscription_id" {
  type        = string
  description = "ID de la suscripción de Azure."
}

variable "environment" {
  type        = string
  default     = "dev"
  description = "Ambiente: dev, qa o prod. La misma plantilla sirve para los tres."
  validation {
    condition     = contains(["dev", "qa", "prod"], var.environment)
    error_message = "environment debe ser dev, qa o prod."
  }
}

variable "location" {
  type    = string
  default = "eastus2"
}

variable "api_image" {
  type    = string
  default = "ghcr.io/jeisonlondono35-cloud/corteza-api:latest"
}

variable "web_image" {
  type    = string
  default = "ghcr.io/jeisonlondono35-cloud/corteza-web:latest"
}

variable "sql_admin_login" {
  type    = string
  default = "cortezaadmin"
}

# Los secretos NUNCA van en el repositorio: se pasan por variable de entorno (TF_VAR_...) o por GitHub Secrets.
variable "sql_admin_password" {
  type      = string
  sensitive = true
}

variable "jwt_key" {
  type        = string
  sensitive   = true
  description = "Llave de firma de los JWT, de al menos 32 caracteres."
  validation {
    condition     = length(var.jwt_key) >= 32
    error_message = "jwt_key debe tener al menos 32 caracteres."
  }
}

variable "seed_password" {
  type        = string
  sensitive   = true
  description = "Contraseña de los usuarios de demostración."
}
