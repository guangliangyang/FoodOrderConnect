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

# Function to start services
start_services() {
    print_status "Starting BidOne development environment..."
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
    
    print_success "Development environment started successfully!"
    print_status "Services available at:"
    echo "  - API Gateway: http://localhost"
    echo "  - External Order API: http://localhost:5001"
    echo "  - Internal System API: http://localhost:5002"
    echo "  - Grafana Dashboard: http://localhost:3000 (admin/admin123)"
    echo "  - Prometheus: http://localhost:9090"
    echo "  - Jaeger UI: http://localhost:16686"
    echo "  - Cosmos DB Emulator: https://localhost:8081/_explorer/index.html"
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

# Function to check service health
check_service_health() {
    local service_name=$1
    local display_name=$2
    local max_attempts=30
    local attempt=1
    
    print_status "Checking health of $display_name..."
    
    while [ $attempt -le $max_attempts ]; do

        container_status=$(docker-compose ps --filter "name=$service_name" --format "{{.State}}")

        if [ "$container_status" = "exited" ]; then
            print_error "Error: Container '$service_name' has exited. Check logs for details."
            return 1
        elif [ "$container_status" = "restarting" ]; then
            print_warning "Warning: Container '$service_name' is restarting."
        elif [ "$container_status" = "unhealthy" ]; then
            print_error "Error: Container '$service_name' is marked as unhealthy by its healthcheck."
            return 1
        else
            print_status "Waiting for $display_name to be healthy (attempt $attempt/$max_attempts)..."
        fi
        
        if docker-compose exec -T $service_name echo "healthy" >/dev/null 2>&1; then
            print_success "$display_name is healthy"
            return 0
        fi
        



        print_status "Waiting for $display_name to be healthy (attempt $attempt/$max_attempts)..."
        sleep 2
        ((attempt++))
    done
    
    print_error "$display_name health check failed after $max_attempts attempts"
    return 1
}

# Function to check service health
check_service_health() {
    local service_name=$1
    local display_name=$2
    local max_attempts=30
    local attempt=1

    print_status "Checking health of $display_name..."

    while [ $attempt -le $max_attempts ]; do



        print_status "Waiting for $display_name to be healthy (attempt $attempt/$max_attempts)..."
        sleep 2
        ((attempt++))
    done
    
    print_warning "$display_name health check timed out, but continuing..."
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
    echo "Commands:"
    echo "  start     Start the development environment"
    echo "  stop      Stop the development environment"
    echo "  restart   Restart the development environment"
    echo "  status    Show service status and health"
    echo "  logs      Show logs for all services"
    echo "  logs [service]  Show logs for specific service"
    echo "  cleanup   Stop services and remove volumes"
    echo "  help      Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 start"
    echo "  $0 logs external-order-api"
    echo "  $0 status"
}

# Main script logic
case "${1:-}" in
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
    cleanup)
        cleanup
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        print_error "Unknown command: ${1:-}"
        echo ""
        show_help
        exit 1
        ;;
esac