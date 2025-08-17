#!/bin/bash
set -euo pipefail

# BidOne Integration Platform - Health Check Script
# This script checks the health status of all services

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
MAX_WAIT_TIME=300  # 5 minutes
CHECK_INTERVAL=5   # 5 seconds

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[✅]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[⚠️]${NC} $1"
}

log_error() {
    echo -e "${RED}[❌]${NC} $1"
}

# Check if URL is responding
check_url() {
    local url=$1
    local name=$2
    local timeout=${3:-10}
    
    if curl -f -s -m $timeout "$url" > /dev/null 2>&1; then
        log_success "$name is responding"
        return 0
    else
        log_error "$name is not responding at $url"
        return 1
    fi
}

# Check if Docker container is running
check_container() {
    local container_name=$1
    local service_name=$2
    
    if docker ps --format "table {{.Names}}" | grep -q "^$container_name$"; then
        log_success "$service_name container is running"
        return 0
    else
        log_error "$service_name container ($container_name) is not running"
        return 1
    fi
}

# Check container health status
check_container_health() {
    local container_name=$1
    local service_name=$2
    
    local health_status=$(docker inspect --format='{{.State.Health.Status}}' "$container_name" 2>/dev/null || echo "unknown")
    
    case $health_status in
        "healthy")
            log_success "$service_name is healthy"
            return 0
            ;;
        "unhealthy")
            log_error "$service_name is unhealthy"
            return 1
            ;;
        "starting")
            log_warning "$service_name is still starting"
            return 1
            ;;
        "unknown"|"")
            log_warning "$service_name health status unknown (no healthcheck configured)"
            return 0
            ;;
        *)
            log_error "$service_name has unknown health status: $health_status"
            return 1
            ;;
    esac
}

# Wait for service to be ready
wait_for_service() {
    local check_function=$1
    local service_name=$2
    local max_wait=$3
    shift 3
    local args=("$@")
    
    local elapsed=0
    log_info "Waiting for $service_name to be ready..."
    
    while [ $elapsed -lt $max_wait ]; do
        if $check_function "${args[@]}"; then
            return 0
        fi
        
        sleep $CHECK_INTERVAL
        elapsed=$((elapsed + CHECK_INTERVAL))
        
        if [ $((elapsed % 30)) -eq 0 ]; then
            log_info "Still waiting for $service_name... (${elapsed}s elapsed)"
        fi
    done
    
    log_error "$service_name failed to become ready within ${max_wait}s"
    return 1
}

# Check development environment
check_dev_environment() {
    log_info "Checking development environment services..."
    echo ""
    
    local all_healthy=true
    
    # Check Docker containers
    if ! check_container "bidone-redis" "Redis"; then
        all_healthy=false
    fi
    
    if ! check_container "bidone-sqlserver" "SQL Server"; then
        all_healthy=false
    fi
    
    if ! check_container "bidone-cosmosdb" "Cosmos DB"; then
        all_healthy=false
    fi
    
    if ! check_container "bidone-azurite" "Azurite"; then
        all_healthy=false
    fi
    
    # Check optional containers
    if check_container "bidone-servicebus" "Service Bus" 2>/dev/null; then
        :  # Service Bus container exists
    fi
    
    if check_container "bidone-prometheus" "Prometheus" 2>/dev/null; then
        :  # Prometheus container exists
    fi
    
    if check_container "bidone-grafana" "Grafana" 2>/dev/null; then
        :  # Grafana container exists
    fi
    
    echo ""
    
    # Check service endpoints
    log_info "Checking service endpoints..."
    
    if check_url "redis://localhost:6379" "Redis" 5; then
        :
    elif command -v redis-cli >/dev/null && redis-cli -h localhost -p 6379 ping >/dev/null 2>&1; then
        log_success "Redis is responding"
    else
        log_error "Redis is not accessible"
        all_healthy=false
    fi
    
    # Check SQL Server
    if docker exec bidone-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'BidOne123!' -Q "SELECT 1" -C -N >/dev/null 2>&1; then
        log_success "SQL Server is responding"
    else
        log_error "SQL Server is not accessible"
        all_healthy=false
    fi
    
    # Check Cosmos DB
    if check_url "https://localhost:8081/_explorer/emulator.pem" "Cosmos DB" 10; then
        :
    else
        log_warning "Cosmos DB emulator is not responding (may still be starting)"
    fi
    
    # Check Azurite
    if check_url "http://localhost:10000" "Azurite Blob" 5; then
        :
    else
        log_warning "Azurite is not responding"
    fi
    
    # Check optional services
    if check_url "http://localhost:9090/-/ready" "Prometheus" 5 2>/dev/null; then
        :
    fi
    
    if check_url "http://localhost:3000/api/health" "Grafana" 5 2>/dev/null; then
        :
    fi
    
    return $all_healthy
}

# Check full environment (docker-compose.yml)
check_full_environment() {
    log_info "Checking full environment services..."
    echo ""
    
    local all_healthy=true
    
    # Check core services
    local services=(
        "bidone-sqlserver:SQL Server"
        "bidone-redis:Redis"
        "bidone-cosmosdb:Cosmos DB"
        "bidone-external-api:External Order API"
        "bidone-internal-api:Internal System API"
        "bidone-nginx:Nginx"
        "bidone-prometheus:Prometheus"
        "bidone-grafana:Grafana"
    )
    
    for service in "${services[@]}"; do
        IFS=':' read -r container_name service_name <<< "$service"
        if ! check_container "$container_name" "$service_name"; then
            all_healthy=false
        fi
    done
    
    echo ""
    
    # Check API endpoints
    log_info "Checking API endpoints..."
    
    if ! check_url "http://localhost:8080/health" "External Order API" 10; then
        all_healthy=false
    fi
    
    if ! check_url "http://localhost:8081/health" "Internal System API" 10; then
        all_healthy=false
    fi
    
    # Check monitoring endpoints
    if ! check_url "http://localhost:3000/api/health" "Grafana" 5; then
        log_warning "Grafana is not responding"
    fi
    
    if ! check_url "http://localhost:9090/-/ready" "Prometheus" 5; then
        log_warning "Prometheus is not responding"
    fi
    
    return $all_healthy
}

# Main health check function
main() {
    echo ""
    echo "======================================"
    echo "  BidOne Integration Platform"
    echo "  Health Check"
    echo "======================================"
    echo ""
    
    # Determine which environment to check
    local check_full=false
    local wait_mode=false
    
    while [[ $# -gt 0 ]]; do
        case $1 in
            --full)
                check_full=true
                shift
                ;;
            --wait)
                wait_mode=true
                shift
                ;;
            --help|-h)
                echo "Usage: $0 [OPTIONS]"
                echo ""
                echo "Options:"
                echo "  --full    Check full environment (docker-compose.yml)"
                echo "  --wait    Wait for services to become ready"
                echo "  --help    Show this help message"
                echo ""
                echo "Default: Check complete environment (docker-compose.yml)"
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                exit 1
                ;;
        esac
    done
    
    # Auto-detect environment if not specified
    if [ "$check_full" = false ]; then
        if docker ps --format "table {{.Names}}" | grep -q "bidone-external-api"; then
            check_full=true
            log_info "Detected full environment running"
        else
            log_info "Checking development environment"
        fi
    fi
    
    local all_healthy=true
    
    if [ "$wait_mode" = true ]; then
        log_info "Wait mode enabled - will wait for services to become ready"
        echo ""
        
        if [ "$check_full" = true ]; then
            # Wait for APIs to be ready
            if ! wait_for_service check_url "External Order API" $MAX_WAIT_TIME "http://localhost:8080/health" "External Order API" 10; then
                all_healthy=false
            fi
            
            if ! wait_for_service check_url "Internal System API" $MAX_WAIT_TIME "http://localhost:8081/health" "Internal System API" 10; then
                all_healthy=false
            fi
        else
            # Wait for development services
            if ! wait_for_service check_container "Redis" $MAX_WAIT_TIME "bidone-redis" "Redis"; then
                all_healthy=false
            fi
            
            if ! wait_for_service check_container "SQL Server" $MAX_WAIT_TIME "bidone-sqlserver" "SQL Server"; then
                all_healthy=false
            fi
        fi
        
        echo ""
    fi
    
    # Perform health checks
    if [ "$check_full" = true ]; then
        check_full_environment
    else
        check_dev_environment
    fi
    
    all_healthy=$?
    
    echo ""
    echo "======================================"
    
    if [ $all_healthy -eq 0 ]; then
        log_success "All services are healthy! 🎉"
        echo ""
        log_info "Service endpoints:"
        if [ "$check_full" = true ]; then
            echo "  🌐 External Order API: http://localhost:8080"
            echo "  🔧 Internal System API: http://localhost:8081"
            echo "  📊 Grafana Dashboard: http://localhost:3000 (admin/admin123)"
            echo "  📈 Prometheus: http://localhost:9090"
            echo "  🔍 Jaeger Tracing: http://localhost:16686"
        else
            echo "  🗃️  Redis: localhost:6379"
            echo "  🗄️  SQL Server: localhost:1433 (sa/BidOne123!)"
            echo "  🌍 Cosmos DB: https://localhost:8081"
            echo "  📦 Azurite: localhost:10000 (blob), localhost:10001 (queue)"
            if docker ps --format "table {{.Names}}" | grep -q "bidone-grafana"; then
                echo "  📊 Grafana: http://localhost:3000 (admin/admin123)"
            fi
            if docker ps --format "table {{.Names}}" | grep -q "bidone-prometheus"; then
                echo "  📈 Prometheus: http://localhost:9090"
            fi
        fi
        echo ""
        exit 0
    else
        log_error "Some services are not healthy! ❌"
        echo ""
        log_info "Troubleshooting tips:"
        echo "  1. Check Docker Desktop is running"
        echo "  2. Review service logs: docker-compose logs <service-name>"
        echo "  3. Restart services: docker-compose restart <service-name>"
        echo "  4. See troubleshooting guide: docs/troubleshooting.md"
        echo ""
        exit 1
    fi
}

# Run main function
main "$@"