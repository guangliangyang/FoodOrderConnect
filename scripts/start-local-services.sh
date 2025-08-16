#!/bin/bash
set -euo pipefail

echo "🚀 Starting BidOne local development services..."

# Start Docker services
docker-compose -f docker-compose.dev.yml up -d

echo "⏳ Waiting for services to be ready..."

# Wait for Redis
echo "Waiting for Redis..."
until docker exec bidone-redis-dev redis-cli ping > /dev/null 2>&1; do
    sleep 2
done
echo "✅ Redis is ready"

# Wait for SQL Server
echo "Waiting for SQL Server..."
until docker exec bidone-sql-dev /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P BidOne123! -Q "SELECT 1"  -C > /dev/null 2>&1; do
    sleep 5
done
echo "✅ SQL Server is ready"

echo "🎉 All services are ready!"
echo ""
echo "📊 Service endpoints:"
echo "  Redis: localhost:6379"
echo "  SQL Server: localhost:1433 (sa/BidOne123!)"
echo "  Cosmos DB Emulator: https://localhost:8081"
echo "  Azurite: localhost:10000 (blob), localhost:10001 (queue), localhost:10002 (table)"
echo ""
echo "🛑 To stop services: docker-compose -f docker-compose.dev.yml down"
