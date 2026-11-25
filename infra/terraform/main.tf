# Corteza en Azure: Azure SQL (serverless) + Azure Container Apps (API y front como contenedores).
locals {
  prefix = "corteza-${var.environment}"
  tags = {
    proyecto = "corteza"
    ambiente = var.environment
  }
}

resource "random_string" "suffix" {
  length  = 5
  upper   = false
  special = false
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.prefix}"
  location = var.location
  tags     = local.tags
}

# ---------- Base de datos ----------
resource "azurerm_mssql_server" "sql" {
  name                         = "sql-${local.prefix}-${random_string.suffix.result}"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"
  tags                         = local.tags
}

resource "azurerm_mssql_database" "corteza" {
  name        = "Corteza"
  server_id   = azurerm_mssql_server.sql.id
  collation   = "Modern_Spanish_CI_AI"
  sku_name    = "GP_S_Gen5_1" # serverless: se pausa solo y cuesta casi nada sin uso
  max_size_gb = 2

  min_capacity                = 0.5
  auto_pause_delay_in_minutes = 60
  tags                        = local.tags
}

# Regla especial 0.0.0.0: permite que los servicios de Azure (los Container Apps) lleguen a la base.
resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.sql.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# ---------- Contenedores ----------
resource "azurerm_log_analytics_workspace" "logs" {
  name                = "log-${local.prefix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

resource "azurerm_container_app_environment" "env" {
  name                       = "cae-${local.prefix}"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.logs.id
  tags                       = local.tags
}

resource "azurerm_container_app" "api" {
  name                         = "ca-${local.prefix}-api"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.env.id
  revision_mode                = "Single"
  tags                         = local.tags

  secret {
    name  = "sql-connection"
    value = "Server=tcp:${azurerm_mssql_server.sql.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.corteza.name};User ID=${var.sql_admin_login};Password=${var.sql_admin_password};Encrypt=True;TrustServerCertificate=False;"
  }
  secret {
    name  = "jwt-key"
    value = var.jwt_key
  }
  secret {
    name  = "seed-password"
    value = var.seed_password
  }

  template {
    min_replicas = 1
    max_replicas = 2

    container {
      name   = "api"
      image  = var.api_image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name        = "ConnectionStrings__Corteza"
        secret_name = "sql-connection"
      }
      env {
        name        = "Jwt__Key"
        secret_name = "jwt-key"
      }
      env {
        name        = "Seed__DefaultPassword"
        secret_name = "seed-password"
      }
      env {
        name  = "Database__InitialiseOnStartup"
        value = "true"
      }

      liveness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health"
      }
      readiness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health"
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

resource "azurerm_container_app" "web" {
  name                         = "ca-${local.prefix}-web"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.env.id
  revision_mode                = "Single"
  tags                         = local.tags

  template {
    min_replicas = 1
    max_replicas = 2

    container {
      name   = "web"
      image  = var.web_image
      cpu    = 0.25
      memory = "0.5Gi"

      # nginx reenvía /api a la API; el Host debe ser el del destino (lo resuelve $proxy_host en nginx.conf.template).
      env {
        name  = "API_UPSTREAM"
        value = "https://${azurerm_container_app.api.ingress[0].fqdn}"
      }

      liveness_probe {
        transport = "HTTP"
        port      = 80
        path      = "/healthz"
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 80
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}
