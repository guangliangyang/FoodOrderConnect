#!/bin/bash

# BidOne Development Environment Manager
# This script helps manage the local development environment using Docker Compose

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check if Docker is running
check_docker() {
    if ! docker info >/dev/null 2>&1; then
        print_error "Docker is not running. Please start Docker Desktop and try again."
        exit 1
    fi
}

# Function to start infrastructure services only (for local development)
start_infrastructure() {
    print_status "Starting infrastructure services for local development..."
    check_docker
    
    # Start only infrastructure services
    print_status "Starting infrastructure services..."
    docker-compose up -d sqlserver redis cosmosdb servicebus otel-collector jaeger prometheus grafana
    
    print_status "Waiting for infrastructure services to be healthy..."
    sleep 30
    
    # Check service health
    check_service_health "sqlserver" "SQL Server"
    check_service_health "redis" "Redis"
    check_service_health "servicebus" "Service Bus"
    
    print_success "Infrastructure services started successfully!"
    print_status "Infrastructure services available at:"
    echo "  - SQL Server: localhost:1433 (sa/BidOne123!)"
    echo "  - Redis: localhost:6379"
    echo "  - Cosmos DB Emulator: https://localhost:8081/_explorer/index.html"
    echo "  - Service Bus: localhost:5672"
    echo "  - Grafana Dashboard: http://localhost:3000 (admin/admin123)"
    echo "  - Prometheus: http://localhost:9090"
    echo "  - Jaeger UI: http://localhost:16686"
    echo ""
    print_status "Now you can run your applications locally:"
    echo "  - External Order API: cd src/ExternalOrderApi && dotnet run"
    echo "  - Internal System API: cd src/InternalSystemApi && dotnet run"
    echo "  - Order Function: cd src/OrderIntegrationFunction && func start"
    echo "  - AI Function: cd src/CustomerCommunicationFunction && func start --port 7072"
}

# Function to start all services (complete containerized environment)
start_services() {
    print_status "Starting complete BidOne development environment..."
    check_docker
    
    # Start infrastructure services first
    print_status "Starting infrastructure services..."
    docker-compose up -d sqlserver redis cosmosdb servicebus otel-collector jaeger prometheus grafana
    
    print_status "Waiting for infrastructure services to be healthy..."
    sleep 30
    
    # Check service health
    check_service_health "sqlserver" "SQL Server"
    check_service_health "redis" "Redis"
    check_service_health "servicebus" "Service Bus"
    
    # Start application services
    print_status "Starting application services..."
    docker-compose up -d external-order-api internal-system-api nginx
    
    print_status "Waiting for application services to start..."
    sleep 20
    
    print_success "Complete development environment started successfully!"
    print_status "All services available at:"
    echo "  - External Order API: http://localhost:5001"
    echo "  - Internal System API: http://localhost:5002"
    echo "  - Grafana Dashboard: http://localhost:3000 (admin/admin123)"
    echo "  - Prometheus: http://localhost:9090"
    echo "  - Jaeger UI: http://localhost:16686"
    echo "  - Cosmos DB Emulator: https://localhost:8081/_explorer/index.html"
    echo ""
    print_warning "Azure Functions are not containerized. To start them locally:"
    echo "  - cd src/OrderIntegrationFunction && func start"
    echo "  - cd src/CustomerCommunicationFunction && func start --port 7072"
}

# Function to stop services
stop_services() {
    print_status "Stopping BidOne development environment..."
    docker-compose down
    print_success "Development environment stopped."
}

# Function to restart services
restart_services() {
    print_status "Restarting BidOne development environment..."
    stop_services
    start_services
}

# Function to rebuild and restart specific service
rebuild_service() {
    local service=${1:-}
    
    if [ -z "$service" ]; then
        print_error "Service name is required for rebuild command"
        echo "Available services: external-order-api, internal-system-api"
        exit 1
    fi
    
    print_status "Rebuilding and restarting $service..."
    
    # Stop the specific service
    docker-compose stop "$service"
    
    # Remove the old container
    docker-compose rm -f "$service"
    
    # Rebuild the image
    print_status "Rebuilding $service image..."
    docker-compose build --no-cache "$service"
    
    # Start the service
    print_status "Starting $service..."
    docker-compose up -d "$service"
    
    # Check health
    sleep 10
    if [ "$service" = "external-order-api" ]; then
        check_endpoint "http://localhost:5001/health" "External Order API"
    elif [ "$service" = "internal-system-api" ]; then
        check_endpoint "http://localhost:5002/health" "Internal System API"
    fi
    
    print_success "$service rebuilt and restarted successfully!"
}

# Function to rebuild all application services
rebuild_all() {
    print_status "Rebuilding all application services..."
    
    # Stop application services
    docker-compose stop external-order-api internal-system-api
    
    # Remove containers
    docker-compose rm -f external-order-api internal-system-api
    
    # Rebuild images
    print_status "Rebuilding application images..."
    docker-compose build --no-cache external-order-api internal-system-api
    
    # Start services
    print_status "Starting application services..."
    docker-compose up -d external-order-api internal-system-api nginx
    
    # Wait and check health
    sleep 20
    check_endpoint "http://localhost:5001/health" "External Order API"
    check_endpoint "http://localhost:5002/health" "Internal System API"
    
    print_success "All application services rebuilt and restarted successfully!"
}

# Function to check service health
check_service_health() {
    local service_name=$1
    local display_name=$2
    local max_attempts=30
    local attempt=1
    
    print_status "Checking health of $display_name..."
    
    while [ $attempt -le $max_attempts ]; do
        # Get container status using docker-compose ps
        container_status=$(docker-compose ps --services --filter "name=$service_name" -q 2>/dev/null)
        
        if [ -n "$container_status" ]; then
            # Check if container is running
            if docker-compose ps $service_name | grep -q "running"; then
                print_success "$display_name is running and healthy"
                return 0
            fi
        fi
        
        print_status "Waiting for $display_name to be running (attempt $attempt/$max_attempts)..."
        sleep 2
        ((attempt++))
    done
    
    print_warning "$display_name is not running after $max_attempts attempts, but continuing..."
    return 0
}

# Function to show logs
show_logs() {
    local service=${1:-}
    
    if [ -z "$service" ]; then
        print_status "Showing logs for all services..."
        docker-compose logs -f
    else
        print_status "Showing logs for $service..."
        docker-compose logs -f "$service"
    fi
}

# Function to show status
show_status() {
    print_status "Service Status:"
    docker-compose ps
    
    echo ""
    print_status "Service Health:"
    
    # Check if services are responding
    check_endpoint "http://localhost:5001/health" "External Order API"
    check_endpoint "http://localhost:5002/health" "Internal System API"
    check_endpoint "http://localhost:3000" "Grafana"
    check_endpoint "http://localhost:9090" "Prometheus"
    check_endpoint "http://localhost:16686" "Jaeger"
}

# Function to check endpoint availability
check_endpoint() {
    local url=$1
    local name=$2
    
    if curl -s -o /dev/null -w "%{http_code}" "$url" | grep -q "200\|302"; then
        print_success "$name is accessible at $url"
    else
        print_warning "$name is not accessible at $url"
    fi
}

# Function to clean up
cleanup() {
    print_status "Cleaning up BidOne development environment..."
    docker-compose down -v --remove-orphans
    docker system prune -f
    print_success "Cleanup completed."
}

# Function to show help
show_help() {
    echo "BidOne Development Environment Manager"
    echo ""
    echo "Usage: $0 [COMMAND]"
    echo ""
    echo "🚀 Development Modes:"
    echo "  infra     Start infrastructure services only (for local IDE development)"
    echo "  start     Start complete environment (APIs + Infrastructure)"
    echo ""
    echo "📋 Management Commands:"
    echo "  stop      Stop the development environment"
    echo "  restart   Restart the development environment"
    echo "  status    Show service status and health"
    echo "  logs      Show logs for all services"
    echo "  logs [service]  Show logs for specific service"
    echo "  rebuild [service]  Rebuild and restart specific service"
    echo "  rebuild-all       Rebuild all application services"
    echo "  cleanup   Stop services and remove volumes"
    echo "  help      Show this help message"
    echo ""
    echo "🎯 Usage Scenarios:"
    echo "  📝 Local Development (Mode 2): ./docker-dev.sh infra"
    echo "     Then run APIs in your IDE or with 'dotnet run'"
    echo ""
    echo "  🏗️ Complete Demo (Mode 1): ./docker-dev.sh start"
    echo "     Everything runs in containers"
    echo ""
    echo "Note: Azure Functions always run locally (not containerized):"
    echo "  func start  # in src/OrderIntegrationFunction/"
    echo "  func start --port 7072  # in src/CustomerCommunicationFunction/"
    echo ""
    echo "Examples:"
    echo "  $0 infra                    # Start infrastructure for local development"
    echo "  $0 start                    # Start complete containerized environment"
    echo "  $0 logs external-order-api  # View API logs"
    echo "  $0 rebuild external-order-api  # Rebuild after code changes"
    echo "  $0 status                   # Check all service health"
}

# Main script logic
case "${1:-}" in
    infra)
        start_infrastructure
        ;;
    start)
        start_services
        ;;
    stop)
        stop_services
        ;;
    restart)
        restart_services
        ;;
    status)
        show_status
        ;;
    logs)
        show_logs "${2:-}"
        ;;
    rebuild)
        rebuild_service "${2:-}"
        ;;
    rebuild-all)
        rebuild_all
        ;;
    cleanup)
        cleanup
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        if [ -z "${1:-}" ]; then
            print_warning "No command specified. Showing help:"
            echo ""
            show_help
        else
            print_error "Unknown command: ${1:-}"
            echo ""
            show_help
            exit 1
        fi
        ;;
esac