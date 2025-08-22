#!/bin/bash

# Infrastructure Testing Script for BidOne Project
# Tests deployed infrastructure for functionality, security, and compliance

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
RESOURCE_GROUP=""
ENVIRONMENT=""
SUBSCRIPTION_ID=""
VERBOSE=false
OUTPUT_FILE=""

# Test results
PASSED_TESTS=0
FAILED_TESTS=0
SKIPPED_TESTS=0

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[PASS]${NC} $1"
    ((PASSED_TESTS++))
}

print_error() {
    echo -e "${RED}[FAIL]${NC} $1"
    ((FAILED_TESTS++))
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_skip() {
    echo -e "${YELLOW}[SKIP]${NC} $1"
    ((SKIPPED_TESTS++))
}

# Function to show usage
show_usage() {
    echo "Usage: $0 -g <resource-group> -e <environment> [options]"
    echo ""
    echo "Required parameters:"
    echo "  -g, --resource-group RG      Resource group name"
    echo "  -e, --environment ENV        Environment (dev|staging|prod)"
    echo ""
    echo "Optional parameters:"
    echo "  -s, --subscription ID        Azure subscription ID"
    echo "  -v, --verbose               Enable verbose output"
    echo "  -o, --output FILE           Output results to file"
    echo "  -h, --help                  Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 -g rg-bidone-dev -e dev"
    echo "  $0 -g rg-bidone-prod -e prod -v -o test-results.txt"
}

# Function to validate parameters
validate_parameters() {
    if [[ -z "$RESOURCE_GROUP" ]]; then
        print_error "Resource group parameter is required"
        show_usage
        exit 1
    fi

    if [[ -z "$ENVIRONMENT" ]]; then
        print_error "Environment parameter is required"
        show_usage
        exit 1
    fi

    if [[ "$ENVIRONMENT" != "dev" && "$ENVIRONMENT" != "staging" && "$ENVIRONMENT" != "prod" ]]; then
        print_error "Environment must be one of: dev, staging, prod"
        exit 1
    fi
}

# Function to check prerequisites
check_prerequisites() {
    print_info "Checking prerequisites..."

    # Check Azure CLI
    if ! command -v az &> /dev/null; then
        print_error "Azure CLI is not installed or not in PATH"
        exit 1
    fi

    # Check Azure login
    if ! az account show &> /dev/null; then
        print_error "Not logged in to Azure. Please run 'az login'"
        exit 1
    fi

    # Set subscription if provided
    if [[ -n "$SUBSCRIPTION_ID" ]]; then
        az account set --subscription "$SUBSCRIPTION_ID"
    fi

    print_success "Prerequisites check passed"
}

# Function to test resource existence
test_resource_existence() {
    print_info "Testing resource existence..."

    # Test Resource Group
    if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
        print_success "Resource group '$RESOURCE_GROUP' exists"
    else
        print_error "Resource group '$RESOURCE_GROUP' does not exist"
        return 1
    fi

    # Get resource list
    local resources=$(az resource list --resource-group "$RESOURCE_GROUP" --query "[].{Name:name, Type:type}" -o tsv)
    
    if [[ -z "$resources" ]]; then
        print_error "No resources found in resource group"
        return 1
    fi

    # Expected resource types
    local expected_types=(
        "Microsoft.Storage/storageAccounts"
        "Microsoft.KeyVault/vaults"
        "Microsoft.Sql/servers"
        "Microsoft.DocumentDB/databaseAccounts"
        "Microsoft.Cache/Redis"
        "Microsoft.ServiceBus/namespaces"
        "Microsoft.OperationalInsights/workspaces"
        "Microsoft.Insights/components"
    )

    # Check for expected resources
    for expected_type in "${expected_types[@]}"; do
        if echo "$resources" | grep -q "$expected_type"; then
            print_success "Found resource type: $expected_type"
        else
            print_error "Missing resource type: $expected_type"
        fi
    done
}

# Function to test network connectivity
test_network_connectivity() {
    print_info "Testing network connectivity..."

    # Test SQL Server connectivity
    local sql_servers=$(az sql server list --resource-group "$RESOURCE_GROUP" --query "[].fullyQualifiedDomainName" -o tsv)
    
    for sql_server in $sql_servers; do
        if timeout 10 bash -c "nc -z $sql_server 1433" &> /dev/null; then
            print_success "SQL Server '$sql_server' is reachable on port 1433"
        else
            print_error "SQL Server '$sql_server' is not reachable on port 1433"
        fi
    done

    # Test Redis connectivity
    local redis_servers=$(az redis list --resource-group "$RESOURCE_GROUP" --query "[].hostName" -o tsv)
    
    for redis_server in $redis_servers; do
        if timeout 10 bash -c "nc -z $redis_server 6380" &> /dev/null; then
            print_success "Redis server '$redis_server' is reachable on port 6380 (SSL)"
        else
            print_error "Redis server '$redis_server' is not reachable on port 6380 (SSL)"
        fi
    done
}

# Function to test security configuration
test_security_configuration() {
    print_info "Testing security configuration..."

    # Test Key Vault security
    local key_vaults=$(az keyvault list --resource-group "$RESOURCE_GROUP" --query "[].name" -o tsv)
    
    for kv in $key_vaults; do
        # Check soft delete
        local soft_delete=$(az keyvault show --name "$kv" --query "properties.enableSoftDelete" -o tsv)
        if [[ "$soft_delete" == "true" ]]; then
            print_success "Key Vault '$kv' has soft delete enabled"
        else
            print_error "Key Vault '$kv' does not have soft delete enabled"
        fi

        # Check access policies
        local policies_count=$(az keyvault show --name "$kv" --query "length(properties.accessPolicies)" -o tsv)
        if [[ "$policies_count" -gt 0 ]]; then
            print_success "Key Vault '$kv' has access policies configured"
        else
            print_error "Key Vault '$kv' has no access policies configured"
        fi
    done

    # Test SQL Server security
    local sql_servers=$(az sql server list --resource-group "$RESOURCE_GROUP" --query "[].name" -o tsv)
    
    for sql_server in $sql_servers; do
        # Check firewall rules
        local firewall_rules=$(az sql server firewall-rule list --resource-group "$RESOURCE_GROUP" --server "$sql_server" --query "length([])" -o tsv)
        if [[ "$firewall_rules" -gt 0 ]]; then
            print_success "SQL Server '$sql_server' has firewall rules configured"
        else
            print_warning "SQL Server '$sql_server' has no firewall rules configured"
        fi

        # Check Azure Services access
        local azure_access=$(az sql server firewall-rule show --resource-group "$RESOURCE_GROUP" --server "$sql_server" --name "AllowAllWindowsAzureIps" --query "name" -o tsv 2>/dev/null || echo "")
        if [[ -n "$azure_access" ]]; then
            print_success "SQL Server '$sql_server' allows Azure services access"
        else
            print_warning "SQL Server '$sql_server' does not allow Azure services access"
        fi
    done

    # Test Storage Account security
    local storage_accounts=$(az storage account list --resource-group "$RESOURCE_GROUP" --query "[].name" -o tsv)
    
    for storage_account in $storage_accounts; do
        # Check HTTPS only
        local https_only=$(az storage account show --name "$storage_account" --resource-group "$RESOURCE_GROUP" --query "enableHttpsTrafficOnly" -o tsv)
        if [[ "$https_only" == "true" ]]; then
            print_success "Storage Account '$storage_account' enforces HTTPS only"
        else
            print_error "Storage Account '$storage_account' does not enforce HTTPS only"
        fi
    done
}

# Function to test service health
test_service_health() {
    print_info "Testing service health..."

    # Test Service Bus namespace
    local sb_namespaces=$(az servicebus namespace list --resource-group "$RESOURCE_GROUP" --query "[].name" -o tsv)
    
    for sb_namespace in $sb_namespaces; do
        local status=$(az servicebus namespace show --resource-group "$RESOURCE_GROUP" --name "$sb_namespace" --query "status" -o tsv)
        if [[ "$status" == "Active" ]]; then
            print_success "Service Bus namespace '$sb_namespace' is active"
        else
            print_error "Service Bus namespace '$sb_namespace' status: $status"
        fi

        # Test queues
        local queues=$(az servicebus queue list --resource-group "$RESOURCE_GROUP" --namespace-name "$sb_namespace" --query "[].name" -o tsv)
        local queue_count=$(echo "$queues" | wc -w)
        if [[ "$queue_count" -gt 0 ]]; then
            print_success "Service Bus namespace '$sb_namespace' has $queue_count queues configured"
        else
            print_error "Service Bus namespace '$sb_namespace' has no queues configured"
        fi
    done

    # Test Cosmos DB
    local cosmos_accounts=$(az cosmosdb list --resource-group "$RESOURCE_GROUP" --query "[].name" -o tsv)
    
    for cosmos_account in $cosmos_accounts; do
        local status=$(az cosmosdb show --resource-group "$RESOURCE_GROUP" --name "$cosmos_account" --query "properties.provisioningState" -o tsv)
        if [[ "$status" == "Succeeded" ]]; then
            print_success "Cosmos DB account '$cosmos_account' is ready"
        else
            print_error "Cosmos DB account '$cosmos_account' status: $status"
        fi
    done

    # Test Redis Cache
    local redis_caches=$(az redis list --resource-group "$RESOURCE_GROUP" --query "[].name" -o tsv)
    
    for redis_cache in $redis_caches; do
        local status=$(az redis show --resource-group "$RESOURCE_GROUP" --name "$redis_cache" --query "provisioningState" -o tsv)
        if [[ "$status" == "Succeeded" ]]; then
            print_success "Redis Cache '$redis_cache' is ready"
        else
            print_error "Redis Cache '$redis_cache' status: $status"
        fi
    done
}

# Function to test monitoring and logging
test_monitoring_logging() {
    print_info "Testing monitoring and logging configuration..."

    # Test Log Analytics Workspace
    local law_workspaces=$(az monitor log-analytics workspace list --resource-group "$RESOURCE_GROUP" --query "[].name" -o tsv)
    
    for law in $law_workspaces; do
        local state=$(az monitor log-analytics workspace show --resource-group "$RESOURCE_GROUP" --workspace-name "$law" --query "provisioningState" -o tsv)
        if [[ "$state" == "Succeeded" ]]; then
            print_success "Log Analytics Workspace '$law' is ready"
        else
            print_error "Log Analytics Workspace '$law' state: $state"
        fi
    done

    # Test Application Insights
    local app_insights=$(az monitor app-insights component list --resource-group "$RESOURCE_GROUP" --query "[].name" -o tsv)
    
    for ai in $app_insights; do
        local state=$(az monitor app-insights component show --resource-group "$RESOURCE_GROUP" --app "$ai" --query "provisioningState" -o tsv)
        if [[ "$state" == "Succeeded" ]]; then
            print_success "Application Insights '$ai' is ready"
        else
            print_error "Application Insights '$ai' state: $state"
        fi
    done
}

# Function to test compliance and tags
test_compliance_tags() {
    print_info "Testing compliance and tags..."

    # Required tags
    local required_tags=("Environment" "Project" "ManagedBy")
    
    # Get all resources
    local resources=$(az resource list --resource-group "$RESOURCE_GROUP" --query "[].{Name:name, Type:type, Tags:tags}" -o json)
    
    # Check each resource for required tags
    local resource_count=$(echo "$resources" | jq length)
    
    for ((i=0; i<resource_count; i++)); do
        local resource_name=$(echo "$resources" | jq -r ".[$i].Name")
        local resource_type=$(echo "$resources" | jq -r ".[$i].Type")
        local tags=$(echo "$resources" | jq -r ".[$i].Tags")
        
        if [[ "$tags" == "null" ]]; then
            print_error "Resource '$resource_name' ($resource_type) has no tags"
            continue
        fi
        
        local missing_tags=()
        for required_tag in "${required_tags[@]}"; do
            if ! echo "$tags" | jq -e ".$required_tag" &> /dev/null; then
                missing_tags+=("$required_tag")
            fi
        done
        
        if [[ ${#missing_tags[@]} -eq 0 ]]; then
            print_success "Resource '$resource_name' has all required tags"
        else
            print_error "Resource '$resource_name' missing tags: ${missing_tags[*]}"
        fi
    done
}

# Function to generate test report
generate_report() {
    print_info "Generating test report..."
    
    local total_tests=$((PASSED_TESTS + FAILED_TESTS + SKIPPED_TESTS))
    
    echo ""
    echo "=================================="
    echo "INFRASTRUCTURE TEST REPORT"
    echo "=================================="
    echo "Resource Group: $RESOURCE_GROUP"
    echo "Environment: $ENVIRONMENT"
    echo "Test Date: $(date)"
    echo ""
    echo "Test Results:"
    echo "  Total Tests: $total_tests"
    echo "  Passed: $PASSED_TESTS"
    echo "  Failed: $FAILED_TESTS"
    echo "  Skipped: $SKIPPED_TESTS"
    echo ""
    
    if [[ $FAILED_TESTS -eq 0 ]]; then
        echo -e "${GREEN}✓ All tests passed!${NC}"
        local exit_code=0
    else
        echo -e "${RED}✗ $FAILED_TESTS test(s) failed${NC}"
        local exit_code=1
    fi
    
    # Save to file if requested
    if [[ -n "$OUTPUT_FILE" ]]; then
        {
            echo "INFRASTRUCTURE TEST REPORT"
            echo "Resource Group: $RESOURCE_GROUP"
            echo "Environment: $ENVIRONMENT"
            echo "Test Date: $(date)"
            echo "Total Tests: $total_tests"
            echo "Passed: $PASSED_TESTS"
            echo "Failed: $FAILED_TESTS"
            echo "Skipped: $SKIPPED_TESTS"
        } > "$OUTPUT_FILE"
        print_info "Test report saved to: $OUTPUT_FILE"
    fi
    
    return $exit_code
}

# Main function
main() {
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            -g|--resource-group)
                RESOURCE_GROUP="$2"
                shift 2
                ;;
            -e|--environment)
                ENVIRONMENT="$2"
                shift 2
                ;;
            -s|--subscription)
                SUBSCRIPTION_ID="$2"
                shift 2
                ;;
            -v|--verbose)
                VERBOSE=true
                shift
                ;;
            -o|--output)
                OUTPUT_FILE="$2"
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

    # Validate parameters
    validate_parameters

    print_info "Starting infrastructure tests for BidOne project"
    print_info "Resource Group: $RESOURCE_GROUP"
    print_info "Environment: $ENVIRONMENT"
    echo ""

    # Run tests
    check_prerequisites
    test_resource_existence
    test_network_connectivity
    test_security_configuration
    test_service_health
    test_monitoring_logging
    test_compliance_tags

    # Generate report
    generate_report
}

# Run main function
main "$@"