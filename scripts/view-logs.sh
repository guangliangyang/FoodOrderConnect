#!/bin/bash
set -euo pipefail

# BidOne Integration Platform - Log Viewer Script
# This script provides easy access to service logs

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
DEFAULT_LINES=50

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

# Show usage
usage() {
    echo "Usage: $0 [OPTIONS] [SERVICE]"
    echo ""
    echo "View logs for BidOne Integration Platform services"
    echo ""
    echo "Services:"
    echo "  all                    Show logs for all services"
    echo "  external-api          External Order API"
    echo "  internal-api          Internal System API"
    echo "  order-function        Order Integration Function"
    echo "  ai-function           Customer Communication Function (AI)"
    echo "  sqlserver             SQL Server database"
    echo "  redis                 Redis cache"
    echo "  cosmosdb              Cosmos DB emulator"
    echo "  servicebus            Service Bus emulator"
    echo "  prometheus            Prometheus metrics"
    echo "  grafana               Grafana dashboards"
    echo "  nginx                 Nginx reverse proxy"
    echo "  jaeger                Jaeger tracing"
    echo ""
    echo "Options:"
    echo "  -f, --follow          Follow log output (like tail -f)"
    echo "  -n, --lines NUMBER    Number of lines to show (default: $DEFAULT_LINES)"
    echo "  --dev                 Use development environment containers"
    echo "  --full                Use full environment containers"
    echo "  --since DURATION      Show logs since duration (e.g., 1h, 30m, 2h30m)"
    echo "  --grep PATTERN        Filter logs by pattern"
    echo "  -h, --help           Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 external-api                    # Show last 50 lines of external API logs"
    echo "  $0 -f external-api                # Follow external API logs"
    echo "  $0 -n 100 internal-api           # Show last 100 lines of internal API logs"
    echo "  $0 --since 1h all                # Show all logs from last hour"
    echo "  $0 --grep 'ERROR' external-api   # Show only error logs from external API"
    echo "  $0 --dev redis                   # Show Redis logs from dev environment"
}

# Get container name for service
get_container_name() {
    local service=$1
    local environment=$2
    
    case $service in
        "external-api")
            if [ "$environment" = "dev" ]; then
                echo ""  # No container in dev mode - runs locally
            else
                echo "bidone-external-api"
            fi
            ;;
        "internal-api")
            if [ "$environment" = "dev" ]; then
                echo ""  # No container in dev mode - runs locally
            else
                echo "bidone-internal-api"
            fi
            ;;
        "order-function")
            echo ""  # Azure Functions run locally in dev mode
            ;;
        "ai-function")
            if [ "$environment" = "dev" ]; then
                echo ""  # Runs locally in dev mode
            else
                echo "customer-communication-function"
            fi
            ;;
        "sqlserver")
            if [ "$environment" = "dev" ]; then
                echo "bidone-sql-dev"
            else
                echo "bidone-sqlserver"
            fi
            ;;
        "redis")
            if [ "$environment" = "dev" ]; then
                echo "bidone-redis-dev"
            else
                echo "bidone-redis"
            fi
            ;;
        "cosmosdb")
            if [ "$environment" = "dev" ]; then
                echo "bidone-cosmos-dev"
            else
                echo "bidone-cosmosdb"
            fi
            ;;
        "servicebus")
            if [ "$environment" = "dev" ]; then
                echo "bidone-servicebus-dev"
            else
                echo "bidone-servicebus"
            fi
            ;;
        "prometheus")
            if [ "$environment" = "dev" ]; then
                echo "bidone-prometheus-dev"
            else
                echo "bidone-prometheus"
            fi
            ;;
        "grafana")
            if [ "$environment" = "dev" ]; then
                echo "bidone-grafana-dev"
            else
                echo "bidone-grafana"
            fi
            ;;
        "nginx")
            echo "bidone-nginx"
            ;;
        "jaeger")
            echo "bidone-jaeger"
            ;;
        "otel-collector")
            echo "bidone-otel"
            ;;
        *)
            echo ""
            ;;
    esac
}

# Show logs for a single service
show_service_logs() {
    local service=$1
    local container_name=$2
    local follow=$3
    local lines=$4
    local since=$5
    local grep_pattern=$6
    local environment=$7
    
    if [ -z "$container_name" ]; then
        log_warning "$service is not containerized in $environment environment"
        log_info "For local services, check your IDE console or use:"
        case $service in
            "external-api"|"internal-api")
                echo "  dotnet run --project src/${service//-/}/"
                ;;
            "order-function"|"ai-function")
                echo "  func logs (if running locally with Azure Functions Core Tools)"
                ;;
        esac
        return 1
    fi
    
    # Check if container exists
    if ! docker ps -a --format "table {{.Names}}" | grep -q "^$container_name$"; then
        log_error "Container '$container_name' not found"
        log_info "Available containers:"
        docker ps --format "table {{.Names}}\t{{.Status}}"
        return 1
    fi
    
    # Build docker logs command
    local cmd="docker logs"
    
    if [ "$follow" = true ]; then
        cmd="$cmd -f"
    fi
    
    if [ -n "$lines" ]; then
        cmd="$cmd --tail $lines"
    fi
    
    if [ -n "$since" ]; then
        cmd="$cmd --since $since"
    fi
    
    cmd="$cmd $container_name"
    
    # Add grep filter if specified
    if [ -n "$grep_pattern" ]; then
        cmd="$cmd | grep --color=always '$grep_pattern'"
    fi
    
    log_info "Showing logs for $service ($container_name)"
    if [ "$follow" = true ]; then
        log_info "Press Ctrl+C to stop following logs"
    fi
    echo ""
    
    # Execute command
    eval $cmd
}

# Show logs for all services
show_all_logs() {
    local follow=$1
    local lines=$2
    local since=$3
    local grep_pattern=$4
    local environment=$5
    
    if [ "$follow" = true ]; then
        log_error "Follow mode not supported for all services"
        log_info "Use specific service name for follow mode"
        return 1
    fi
    
    local services=()
    
    if [ "$environment" = "dev" ]; then
        services=("redis" "sqlserver" "cosmosdb" "servicebus" "prometheus" "grafana")
    else
        services=("external-api" "internal-api" "sqlserver" "redis" "cosmosdb" "servicebus" "prometheus" "grafana" "nginx")
    fi
    
    for service in "${services[@]}"; do
        local container_name=$(get_container_name "$service" "$environment")
        
        if [ -n "$container_name" ] && docker ps -a --format "table {{.Names}}" | grep -q "^$container_name$"; then
            echo ""
            echo "======================================"
            echo "  $service ($container_name)"
            echo "======================================"
            
            show_service_logs "$service" "$container_name" false "$lines" "$since" "$grep_pattern" "$environment"
        fi
    done
}

# Auto-detect environment
detect_environment() {
    if docker ps --format "table {{.Names}}" | grep -q "bidone-external-api"; then
        echo "full"
    elif docker ps --format "table {{.Names}}" | grep -q "bidone-.*-dev"; then
        echo "dev"
    else
        echo "unknown"
    fi
}

# Main function
main() {
    local service=""
    local follow=false
    local lines=$DEFAULT_LINES
    local since=""
    local grep_pattern=""
    local environment=""
    
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            -f|--follow)
                follow=true
                shift
                ;;
            -n|--lines)
                lines="$2"
                shift 2
                ;;
            --since)
                since="$2"
                shift 2
                ;;
            --grep)
                grep_pattern="$2"
                shift 2
                ;;
            --dev)
                environment="dev"
                shift
                ;;
            --full)
                environment="full"
                shift
                ;;
            -h|--help)
                usage
                exit 0
                ;;
            -*)
                log_error "Unknown option: $1"
                usage
                exit 1
                ;;
            *)
                if [ -z "$service" ]; then
                    service="$1"
                else
                    log_error "Multiple services specified. Use 'all' to view all services."
                    exit 1
                fi
                shift
                ;;
        esac
    done
    
    # Default service if none specified
    if [ -z "$service" ]; then
        service="all"
    fi
    
    # Auto-detect environment if not specified
    if [ -z "$environment" ]; then
        environment=$(detect_environment)
        if [ "$environment" = "unknown" ]; then
            log_error "Could not detect running environment"
            log_info "Use --dev or --full to specify environment, or start services first"
            exit 1
        fi
        log_info "Auto-detected $environment environment"
        echo ""
    fi
    
    # Validate lines parameter
    if ! [[ "$lines" =~ ^[0-9]+$ ]]; then
        log_error "Lines parameter must be a number"
        exit 1
    fi
    
    # Show logs
    if [ "$service" = "all" ]; then
        show_all_logs "$follow" "$lines" "$since" "$grep_pattern" "$environment"
    else
        local container_name=$(get_container_name "$service" "$environment")
        show_service_logs "$service" "$container_name" "$follow" "$lines" "$since" "$grep_pattern" "$environment"
    fi
}

# Run main function
main "$@"