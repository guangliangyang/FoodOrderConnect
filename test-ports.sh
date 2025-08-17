#!/bin/bash

# Test script to verify port configuration
echo "🧪 Testing port configuration for BidOne APIs..."
echo ""

check_port() {
    local port=$1
    local name=$2
    
    if curl -s -o /dev/null -w "%{http_code}" "http://localhost:$port/health" 2>/dev/null | grep -q "200"; then
        echo "✅ $name is running on port $port"
    else
        echo "❌ $name is not accessible on port $port"
    fi
}

echo "🔍 Checking API endpoints:"
check_port 5001 "External Order API"
check_port 5002 "Internal System API"
check_port 7071 "Order Integration Function"
check_port 7072 "Customer Communication Function"
echo ""

echo "📋 Expected port mapping:"
echo "  - External Order API: http://localhost:5001 (HTTPS: 7001)"
echo "  - Internal System API: http://localhost:5002 (HTTPS: 7002)"
echo "  - Order Function: http://localhost:7071"
echo "  - AI Function: http://localhost:7072"
echo ""

echo "🚀 To start in mixed development mode:"
echo "  1. ./docker-dev.sh infra"
echo "  2. cd src/ExternalOrderApi && dotnet run"
echo "  3. cd src/InternalSystemApi && dotnet run"
echo "  4. cd src/OrderIntegrationFunction && func start"
echo "  5. cd src/CustomerCommunicationFunction && func start --port 7072"