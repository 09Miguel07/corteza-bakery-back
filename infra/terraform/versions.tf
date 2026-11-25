terraform {
  required_version = ">= 1.6"

  required_providers {
    azurerm = { source = "hashicorp/azurerm", version = "~> 4.0" }
    random  = { source = "hashicorp/random", version = "~> 3.6" }
  }

  # Estado remoto compartido. Los valores se pasan con: terraform init -backend-config=backend.hcl
  backend "azurerm" {}
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}
