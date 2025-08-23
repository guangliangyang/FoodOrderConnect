#!/bin/bash

# Test Environment Setup Script for BidOne Project
# Sets up local test environment with all required services

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
REDIS_PORT=6379
SQL_PORT=1433
SQL_PASSWORD="TestPassword123!"
COSMOS_EMULATOR_PORT=8081
SERVICE_BUS_EMULATOR_PORT=5672
CLEAN_START=false
START_SERVICES=true
RUN_MIGRATIONS=true

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo "  --clean                 Clean start (remove existing containers)"
    echo "  --no-services           Skip starting services"
    echo "  --no-migrations         Skip running database migrations"
    echo "  --sql-port PORT         SQL Server port (default: 1433)"
    echo "  --redis-port PORT       Redis port (default: 6379)"
    echo "  --sql-password PASS     SQL Server password (default: TestPassword123!)"
    echo "  -h, --help              Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                      Setup test environment with defaults"
    echo "  $0 --clean              Clean setup (removes existing containers)"
    echo "  $0 --no-services        Setup without starting services"
}

# Function to check prerequisites
check_prerequisites() {
    print_info "Checking prerequisites..."
    
    # Check Docker
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed or not in PATH"
        exit 1
    fi
    
    # Check Docker Compose
    if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
        print_error "Docker Compose is not installed or not available"
        exit 1
    fi
    
    # Check .NET CLI
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET CLI is not installed or not in PATH"
        exit 1
    fi
    
    # Check if we're in the right directory
    if [ ! -f "BidOne.sln" ]; then
        print_error "BidOne.sln not found. Please run this script from the project root directory."
        exit 1
    fi
    
    print_success "All prerequisites met"
}

# Function to clean existing containers
clean_containers() {
    print_info "Cleaning existing test containers..."
    
    # Stop and remove containers
    docker-compose -f docker-compose.test.yml down --volumes --remove-orphans 2>/dev/null || true
    
    # Remove specific test containers
    local containers=("bidone-test-redis" "bidone-test-sql" "bidone-test-cosmos" "bidone-test-servicebus")
    
    for container in "${containers[@]}"; do
        if docker ps -a | grep -q "$container"; then
            docker rm -f "$container" 2>/dev/null || true
        fi
    done
    
    # Clean up volumes
    docker volume prune -f 2>/dev/null || true
    
    print_success "Containers cleaned"
}

# Function to create test docker-compose file
create_test_compose_file() {
    print_info "Creating test Docker Compose configuration..."
    
    cat > docker-compose.test.yml << EOF
version: '3.8'

services:
  redis-test:
    image: redis:7-alpine
    container_name: bidone-test-redis
    ports:
      - "${REDIS_PORT}:6379"
    command: redis-server --appendonly yes
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 3
    volumes:
      - redis-test-data:/data

  sqlserver-test:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: bidone-test-sql
    ports:
      - "${SQL_PORT}:1433"
    environment:
      - SA_PASSWORD=${SQL_PASSWORD}
      - ACCEPT_EULA=Y
      - MSSQL_PID=Express
    healthcheck:
      test: [
        "CMD-SHELL", 
        "/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SQL_PASSWORD} -Q 'SELECT 1'"
      ]
      interval: 10s
      timeout: 10s
      retries: 5
    volumes:
      - sql-test-data:/var/opt/mssql

  cosmos-emulator:
    image: mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
    container_name: bidone-test-cosmos
    ports:
      - "${COSMOS_EMULATOR_PORT}:8081"
      - "10250-10255:10250-10255"
    environment:
      - AZURE_COSMOS_EMULATOR_PARTITION_COUNT=3
      - AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=true
      - AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE=127.0.0.1
    healthcheck:
      test: ["CMD", "curl", "-k", "https://localhost:8081/_explorer/emulator.pem"]
      interval: 30s
      timeout: 10s
      retries: 3
    volumes:
      - cosmos-test-data:/data

volumes:
  redis-test-data:
  sql-test-data:
  cosmos-test-data:
EOF
    
    print_success "Test Docker Compose file created"
}

# Function to start services
start_services() {
    print_info "Starting test services..."
    
    # Start services
    docker-compose -f docker-compose.test.yml up -d
    
    print_info "Waiting for services to be ready..."
    
    # Wait for Redis
    local redis_ready=false
    for i in {1..30}; do
        if docker exec bidone-test-redis redis-cli ping &>/dev/null; then
            redis_ready=true
            break
        fi
        sleep 2
    done
    
    if [ "$redis_ready" = true ]; then
        print_success "Redis is ready"
    else
        print_error "Redis failed to start within timeout"
        exit 1
    fi
    
    # Wait for SQL Server
    local sql_ready=false
    for i in {1..60}; do
        if docker exec bidone-test-sql /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SQL_PASSWORD" -Q "SELECT 1" &>/dev/null; then
            sql_ready=true
            break
        fi
        sleep 3
    done
    
    if [ "$sql_ready" = true ]; then
        print_success "SQL Server is ready"
    else
        print_error "SQL Server failed to start within timeout"
        exit 1
    fi
    
    print_success "All services are ready"
}

# Function to setup test databases
setup_test_databases() {
    print_info "Setting up test databases..."
    
    local connection_string="Server=localhost,${SQL_PORT};Database=master;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=true;"
    
    # Create test databases
    local databases=("BidOneTest" "BidOneIntegrationTest")
    
    for db in "${databases[@]}"; do
        print_info "Creating database: $db"
        
        docker exec bidone-test-sql /opt/mssql-tools/bin/sqlcmd \
            -S localhost -U sa -P "$SQL_PASSWORD" \
            -Q "IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = '$db') CREATE DATABASE [$db];"
        
        if [ $? -eq 0 ]; then
            print_success "Database $db created successfully"
        else
            print_error "Failed to create database $db"
            exit 1
        fi
    done
}

# Function to run database migrations
run_migrations() {
    print_info "Running database migrations..."
    
    local connection_string="Server=localhost,${SQL_PORT};Database=BidOneTest;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=true;"
    
    # Update connection string in appsettings
    export ConnectionStrings__DefaultConnection="$connection_string"
    
    # Run migrations for InternalSystemApi
    if [ -d "src/InternalSystemApi" ]; then
        print_info "Running migrations for InternalSystemApi..."
        
        cd src/InternalSystemApi
        
        # Apply migrations
        dotnet ef database update --connection "$connection_string" --verbose
        
        if [ $? -eq 0 ]; then
            print_success "InternalSystemApi migrations applied successfully"
        else
            print_error "Failed to apply InternalSystemApi migrations"
            exit 1
        fi
        
        cd ../..
    fi
}

# Function to create test data
create_test_data() {
    print_info "Creating test data..."
    
    local connection_string="Server=localhost,${SQL_PORT};Database=BidOneTest;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=true;"
    
    # SQL script for test data
    local test_data_sql="
INSERT INTO Customers (Id, CustomerId, Name, Email, Phone, Address, IsActive, CreatedAt)
VALUES 
    (NEWID(), 'CUST-TEST-001', 'Test Customer 1', 'test1@example.com', '+1234567890', '123 Test St, Test City', 1, GETUTCDATE()),
    (NEWID(), 'CUST-TEST-002', 'Test Customer 2', 'test2@example.com', '+1234567891', '456 Test Ave, Test City', 1, GETUTCDATE()),
    (NEWID(), 'CUST-TEST-003', 'Test Customer 3', 'test3@example.com', '+1234567892', '789 Test Blvd, Test City', 1, GETUTCDATE());

INSERT INTO Products (Id, ProductId, Name, Description, Price, Category, IsActive, CreatedAt)
VALUES
    (NEWID(), 'PROD-TEST-001', 'Test Product 1', 'Test product for unit testing', 25.00, 'Test Category', 1, GETUTCDATE()),
    (NEWID(), 'PROD-TEST-002', 'Test Product 2', 'Another test product', 50.00, 'Test Category', 1, GETUTCDATE()),
    (NEWID(), 'PROD-TEST-003', 'Test Product 3', 'Third test product', 75.00, 'Test Category', 1, GETUTCDATE());

INSERT INTO Inventory (Id, ProductId, ProductName, QuantityAvailable, QuantityReserved, ReorderLevel, LastUpdated)
VALUES
    (NEWID(), 'PROD-TEST-001', 'Test Product 1', 100, 0, 20, GETUTCDATE()),
    (NEWID(), 'PROD-TEST-002', 'Test Product 2', 150, 5, 30, GETUTCDATE()),
    (NEWID(), 'PROD-TEST-003', 'Test Product 3', 75, 10, 15, GETUTCDATE());

INSERT INTO Suppliers (Id, SupplierId, Name, ContactEmail, ContactPhone, Address, IsActive, CreatedAt)
VALUES
    (NEWID(), 'SUPP-TEST-001', 'Test Supplier 1', 'supplier1@example.com', '+1234567800', '100 Supplier St, Supplier City', 1, GETUTCDATE()),
    (NEWID(), 'SUPP-TEST-002', 'Test Supplier 2', 'supplier2@example.com', '+1234567801', '200 Supplier Ave, Supplier City', 1, GETUTCDATE());
"
    
    # Create temporary SQL file
    echo "$test_data_sql" > /tmp/test_data.sql
    
    # Execute test data script
    docker exec -i bidone-test-sql /opt/mssql-tools/bin/sqlcmd \
        -S localhost -U sa -P "$SQL_PASSWORD" \
        -d BidOneTest \
        -i /tmp/test_data.sql
    
    if [ $? -eq 0 ]; then
        print_success "Test data created successfully"
    else
        print_warning "Failed to create some test data (this might be expected if data already exists)"
    fi
    
    # Clean up temporary file
    rm -f /tmp/test_data.sql
}

# Function to display connection information
display_connection_info() {
    print_info "Test Environment Connection Information:"
    echo ""
    echo "SQL Server:"
    echo "  Host: localhost"
    echo "  Port: $SQL_PORT"
    echo "  Username: sa"
    echo "  Password: $SQL_PASSWORD"
    echo "  Test Database: BidOneTest"
    echo "  Integration Test Database: BidOneIntegrationTest"
    echo "  Connection String: Server=localhost,$SQL_PORT;Database=BidOneTest;User Id=sa;Password=$SQL_PASSWORD;TrustServerCertificate=true;"
    echo ""
    echo "Redis:"
    echo "  Host: localhost"
    echo "  Port: $REDIS_PORT"
    echo "  Connection String: localhost:$REDIS_PORT"
    echo ""
    echo "Cosmos DB Emulator:"
    echo "  Endpoint: https://localhost:$COSMOS_EMULATOR_PORT"
    echo "  Primary Key: C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
    echo ""
    echo "To stop the test environment:"
    echo "  docker-compose -f docker-compose.test.yml down"
    echo ""
    echo "To run tests:"
    echo "  ./scripts/run-tests.sh"
}

# Main function
main() {
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --clean)
                CLEAN_START=true
                shift
                ;;
            --no-services)
                START_SERVICES=false
                shift
                ;;
            --no-migrations)
                RUN_MIGRATIONS=false
                shift
                ;;
            --sql-port)
                SQL_PORT="$2"
                shift 2
                ;;
            --redis-port)
                REDIS_PORT="$2"
                shift 2
                ;;
            --sql-password)
                SQL_PASSWORD="$2"
                shift 2
                ;;
            -h|--help)
                show_usage
                exit 0
                ;;
            *)
                print_error "Unknown parameter: $1"
                show_usage
                exit 1
                ;;
        esac
    done

    print_info "Setting up BidOne test environment"
    print_info "Clean start: $CLEAN_START"
    print_info "Start services: $START_SERVICES"
    print_info "Run migrations: $RUN_MIGRATIONS"
    echo ""

    # Run setup steps
    check_prerequisites
    
    if [ "$CLEAN_START" = true ]; then
        clean_containers
    fi
    
    if [ "$START_SERVICES" = true ]; then
        create_test_compose_file
        start_services
        setup_test_databases
        
        if [ "$RUN_MIGRATIONS" = true ]; then
            run_migrations
        fi
        
        create_test_data
    fi
    
    display_connection_info
    
    print_success "Test environment setup completed!"
}

# Run main function
main "$@"
