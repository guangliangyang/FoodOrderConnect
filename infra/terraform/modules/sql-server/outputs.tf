output "server_id" {
  description = "The ID of the SQL Server"
  value       = azurerm_mssql_server.main.id
}

output "server_name" {
  description = "The name of the SQL Server"
  value       = azurerm_mssql_server.main.name
}

output "server_fqdn" {
  description = "The fully qualified domain name of the SQL Server"
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "database_id" {
  description = "The ID of the SQL Database"
  value       = azurerm_mssql_database.main.id
}

output "database_name" {
  description = "The name of the SQL Database"
  value       = azurerm_mssql_database.main.name
}

output "connection_string" {
  description = "ADO.NET connection string for the database"
  value       = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Persist Security Info=False;User ID=${var.administrator_login};Password=${var.administrator_login_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  sensitive   = true
}

output "jdbc_connection_string" {
  description = "JDBC connection string for the database"
  value       = "jdbc:sqlserver://${azurerm_mssql_server.main.fully_qualified_domain_name}:1433;database=${azurerm_mssql_database.main.name};user=${var.administrator_login};password=${var.administrator_login_password};encrypt=true;trustServerCertificate=false;hostNameInCertificate=*.database.windows.net;loginTimeout=30;"
  sensitive   = true
}