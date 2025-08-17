#!/bin/bash

# BidOne Integration Platform - Development Environment Setup Script
# This script sets up the local development environment for the BidOne Integration Platform

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
PROJECT_NAME="BidOne Integration Platform"
DOCKER_COMPOSE_VERSION="2.0"
DOTNET_VERSION="8.0"
AZURE_CLI_VERSION="2.40.0"

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check .NET SDK version
check_dotnet() {
    log_info "Checking .NET SDK..."
    
    if command_exists dotnet; then
        CURRENT_DOTNET_VERSION=$(dotnet --version | cut -d. -f1,2)
        if [[ "$CURRENT_DOTNET_VERSION" == "$DOTNET_VERSION" ]]; then
            log_success ".NET $DOTNET_VERSION SDK is installed"
        else
            log_warning ".NET version $CURRENT_DOTNET_VERSION found, but $DOTNET_VERSION is recommended"
        fi
    else
        log_error ".NET SDK not found. Please install .NET $DOTNET_VERSION SDK from https://dotnet.microsoft.com/download"
        exit 1
    fi
}

# Check Docker and Docker Compose
check_docker() {
    log_info "Checking Docker..."
    
    if command_exists docker; then
        if docker info >/dev/null 2>&1; then
            log_success "Docker is installed and running"
        else
            log_error "Docker is installed but not running. Please start Docker Desktop"
            exit 1
        fi
    else
        log_error "Docker not found. Please install Docker Desktop from https://www.docker.com/products/docker-desktop"
        exit 1
    fi
    
    if command_exists docker-compose; then
        log_success "Docker Compose is available"
    else
        log_error "Docker Compose not found. Please ensure Docker Compose is installed"
        exit 1
    fi
}

# Check Azure CLI
check_azure_cli() {
    log_info "Checking Azure CLI..."
    
    if command_exists az; then
        log_success "Azure CLI is installed"
    else
        log_warning "Azure CLI not found. Installing Azure CLI is recommended for Azure operations"
        echo "Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    fi
}

# Setup local configuration files
setup_local_config() {
    log_info "Setting up local configuration files..."
    
    # Create appsettings.Development.json for ExternalOrderApi
    cat > src/ExternalOrderApi/appsettings.Development.json << 'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "ConnectionStrings": {
    "ServiceBus": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake-key-for-development",
    "Redis": "localhost:6379",
    "ApplicationInsights": ""
  },
  "AllowedHosts": "*"
}
EOF

    # Create appsettings.Development.json for InternalSystemApi
    mkdir -p src/InternalSystemApi
    cat > src/InternalSystemApi/appsettings.Development.json << 'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BidOneDB_Dev;Trusted_Connection=True;MultipleActiveResultSets=true",
    "ServiceBus": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake-key-for-development",
    "Redis": "localhost:6379",
    "ApplicationInsights": ""
  },
  "AllowedHosts": "*"
}
EOF

    # Create local.settings.json for Azure Functions
    mkdir -p src/OrderIntegrationFunction
    cat > src/OrderIntegrationFunction/local.settings.json << 'EOF'
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnection": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake-key-for-development",
    "SqlConnectionString": "Server=(localdb)\\mssqllocaldb;Database=BidOneDB_Dev;Trusted_Connection=True;MultipleActiveResultSets=true",
    "CosmosDbConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
  }
}
EOF
    
    log_success "Local configuration files created"
}

# Create Docker Compose for local development
setup_docker_compose() {
    log_info "Setting up Docker Compose for local services..."
    
    cat > docker-compose.dev.yml << 'EOF'
version: '3.8'

services:
  redis:
    image: redis:7-alpine
    container_name: bidone-redis
    ports:
      - "6379:6379"
    command: redis-server --appendonly yes
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 30s
      timeout: 10s
      retries: 3

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: bidone-sqlserver
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=BidOne123!
      - MSSQL_PID=Developer
    ports:
      - "1433:1433"
    volumes:
      - sqlserver_data:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P BidOne123! -Q 'SELECT 1'"]
      interval: 30s
      timeout: 10s
      retries: 3

  cosmosdb:
    image: mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
    container_name: bidone-cosmosdb
    environment:
      - AZURE_COSMOS_EMULATOR_PARTITION_COUNT=10
      - AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=true
    ports:
      - "8081:8081"
      - "10251:10251"
      - "10252:10252"
      - "10253:10253"
      - "10254:10254"
    volumes:
      - cosmos_data:/data/db
    healthcheck:
      test: ["CMD", "curl", "-f", "https://localhost:8081/_explorer/emulator.pem"]
      interval: 30s
      timeout: 10s
      retries: 5

  azurite:
    image: mcr.microsoft.com/azure-storage/azurite:latest
    container_name: bidone-azurite
    ports:
      - "10000:10000"
      - "10001:10001"
      - "10002:10002"
    volumes:
      - azurite_data:/data
    command: azurite --blobHost 0.0.0.0 --queueHost 0.0.0.0 --tableHost 0.0.0.0

volumes:
  redis_data:
  sqlserver_data:
  cosmos_data:
  azurite_data:

networks:
  default:
    name: bidone-dev-network
EOF
    
    log_success "Docker Compose configuration created"
}

# Setup development scripts
setup_dev_scripts() {
    log_info "Setting up development scripts..."
    
    # Start local services script
    cat > scripts/start-local-services.sh << 'EOF'
#!/bin/bash
set -euo pipefail

echo "🚀 Starting BidOne local development services..."

# Start Docker services
docker-compose -f docker-compose.dev.yml up -d

echo "⏳ Waiting for services to be ready..."

# Wait for Redis
echo "Waiting for Redis..."
until docker exec bidone-redis redis-cli ping > /dev/null 2>&1; do
    sleep 2
done
echo "✅ Redis is ready"

# Wait for SQL Server
echo "Waiting for SQL Server..."
until docker exec bidone-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P BidOne123! -Q "SELECT 1" > /dev/null 2>&1; do
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
EOF

    # Stop local services script
    cat > scripts/stop-local-services.sh << 'EOF'
#!/bin/bash
set -euo pipefail

echo "🛑 Stopping BidOne local development services..."

docker-compose -f docker-compose.dev.yml down

echo "✅ All services stopped"
EOF

    # Build all projects script
    cat > scripts/build-all.sh << 'EOF'
#!/bin/bash
set -euo pipefail

echo "🔨 Building all BidOne projects..."

# Restore dependencies
echo "📦 Restoring dependencies..."
dotnet restore

# Build all projects
echo "🔧 Building projects..."
dotnet build --configuration Debug --no-restore

# Run tests
echo "🧪 Running tests..."
dotnet test --configuration Debug --no-build --verbosity normal

echo "✅ Build completed successfully!"
EOF

    # Deploy to Azure script
    cat > scripts/deploy-to-azure.sh << 'EOF'
#!/bin/bash
set -euo pipefail

# Parse command line arguments
ENVIRONMENT="dev"
RESOURCE_GROUP=""
SUBSCRIPTION_ID=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --environment|-e)
            ENVIRONMENT="$2"
            shift 2
            ;;
        --resource-group|-g)
            RESOURCE_GROUP="$2"
            shift 2
            ;;
        --subscription|-s)
            SUBSCRIPTION_ID="$2"
            shift 2
            ;;
        --help|-h)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  -e, --environment     Target environment (dev, staging, prod) [default: dev]"
            echo "  -g, --resource-group  Azure resource group name"
            echo "  -s, --subscription    Azure subscription ID"
            echo "  -h, --help           Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option $1"
            exit 1
            ;;
    esac
done

if [[ -z "$RESOURCE_GROUP" ]]; then
    echo "❌ Resource group is required. Use --resource-group or -g option."
    exit 1
fi

echo "🚀 Deploying BidOne Integration Platform to Azure..."
echo "   Environment: $ENVIRONMENT"
echo "   Resource Group: $RESOURCE_GROUP"

# Set subscription if provided
if [[ -n "$SUBSCRIPTION_ID" ]]; then
    echo "🔧 Setting Azure subscription..."
    az account set --subscription "$SUBSCRIPTION_ID"
fi

# Deploy infrastructure
echo "🏗️  Deploying infrastructure..."
az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file infra/main.bicep \
    --parameters infra/parameters.$ENVIRONMENT.json \
    --name "bidone-deployment-$(date +%Y%m%d-%H%M%S)"

echo "✅ Deployment completed successfully!"
EOF

    # Make scripts executable
    chmod +x scripts/*.sh
    
    log_success "Development scripts created and made executable"
}

# Install development tools
install_dev_tools() {
    log_info "Installing development tools..."
    
    # Install Azure Functions Core Tools if not present
    if ! command_exists func; then
        log_warning "Azure Functions Core Tools not found"
        echo "Please install from: https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local"
    else
        log_success "Azure Functions Core Tools is installed"
    fi
    
    # Install EF Core tools
    log_info "Installing Entity Framework Core tools..."
    dotnet tool install --global dotnet-ef 2>/dev/null || log_info "EF Core tools already installed"
    
    # Install user-secrets tool
    log_info "Installing User Secrets tool..."
    dotnet tool install --global dotnet-user-secrets 2>/dev/null || log_info "User Secrets tool already installed"
    
    log_success "Development tools setup completed"
}

# Setup Git hooks
setup_git_hooks() {
    log_info "Setting up Git hooks..."
    
    # Pre-commit hook
    cat > .git/hooks/pre-commit << 'EOF'
#!/bin/bash

echo "🔍 Running pre-commit checks..."

# Run dotnet format
echo "📝 Formatting code..."
dotnet format --no-restore --verbosity quiet

# Run tests
echo "🧪 Running tests..."
dotnet test --configuration Debug --no-build --verbosity quiet

if [ $? -ne 0 ]; then
    echo "❌ Tests failed. Commit aborted."
    exit 1
fi

echo "✅ Pre-commit checks passed!"
EOF

    chmod +x .git/hooks/pre-commit
    
    log_success "Git hooks setup completed"
}

# Create .env file for local development
create_env_file() {
    log_info "Creating .env file for local development..."
    
    cat > .env << 'EOF'
# BidOne Integration Platform - Local Development Environment

# Database Configuration
SQL_CONNECTION_STRING=Server=localhost,1433;Database=BidOneDB_Dev;User Id=sa;Password=BidOne123!;TrustServerCertificate=True;
COSMOS_CONNECTION_STRING=AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==

# Cache Configuration
REDIS_CONNECTION_STRING=localhost:6379

# Storage Configuration
AZURE_STORAGE_CONNECTION_STRING=UseDevelopmentStorage=true

# Service Bus Configuration (for local development - use Azure Service Bus emulator or Azure instance)
SERVICE_BUS_CONNECTION_STRING=Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake-key

# Application Insights (leave empty for local development)
APPLICATION_INSIGHTS_CONNECTION_STRING=

# Environment
ASPNETCORE_ENVIRONMENT=Development
DOTNET_ENVIRONMENT=Development

# Logging
SERILOG_MINIMUM_LEVEL=Information

# API URLs
EXTERNAL_ORDER_API_URL=https://localhost:7001
INTERNAL_SYSTEM_API_URL=https://localhost:7002
AZURE_FUNCTION_URL=http://localhost:7071
EOF
    
    log_success ".env file created"
}

# Main setup function
main() {
    echo ""
    echo "======================================"
    echo "  $PROJECT_NAME"
    echo "  Development Environment Setup"
    echo "======================================"
    echo ""
    
    log_info "Starting development environment setup..."
    
    # Check prerequisites
    check_dotnet
    check_docker
    check_azure_cli
    
    # Setup local development environment
    setup_local_config
    setup_docker_compose
    setup_dev_scripts
    install_dev_tools
    create_env_file
    
    # Setup Git hooks if .git directory exists
    if [ -d ".git" ]; then
        setup_git_hooks
    else
        log_warning "Not a Git repository. Skipping Git hooks setup."
    fi
    
    echo ""
    log_success "🎉 Development environment setup completed!"
    echo ""
    echo "📋 Next steps:"
    echo "  1. Start local services: ./scripts/start-local-services.sh"
    echo "  2. Build all projects: ./scripts/build-all.sh"
    echo "  3. Configure Azure connection strings in appsettings.Development.json"
    echo "  4. Run the applications using your preferred IDE or dotnet run"
    echo ""
    echo "📚 Useful commands:"
    echo "  • View service logs: docker-compose -f docker-compose.dev.yml logs -f [service-name]"
    echo "  • Stop services: ./scripts/stop-local-services.sh"
    echo "  • Deploy to Azure: ./scripts/deploy-to-azure.sh --environment dev --resource-group <rg-name>"
    echo ""
    echo "📖 For more information, see docs/deployment-guide.md"
    echo ""
}

# Run main function
main "$@"