output "web_url" {
  description = "URL pública del front (el enlace de despliegue en Azure)."
  value       = "https://${azurerm_container_app.web.ingress[0].fqdn}"
}

output "api_url" {
  value = "https://${azurerm_container_app.api.ingress[0].fqdn}"
}

output "sql_server" {
  value = azurerm_mssql_server.sql.fully_qualified_domain_name
}
