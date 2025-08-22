#!/bin/bash

# BidOne Infrastructure Deployment Script
# Supports both Bicep and Terraform deployments

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
TOOL=""
ENVIRONMENT=""
RESOURCE_GROUP=""
LOCATION="East US"
SUBSCRIPTION_ID=""
SQL_PASSWORD=""
DRY_RUN=false
SKIP_CONFIRMATION=false

# Function to print colored output
print_info() {
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

# Function to show usage
show_usage() {
    echo "Usage: $0 -t <tool> -e <environment> -g <resource-group> [options]"
    echo ""
    echo "Required parameters:"
    echo "  -t, --tool TOOL              Infrastructure tool to use (bicep|terraform)"
    echo "  -e, --environment ENV        Target environment (dev|staging|prod)"
    echo "  -g, --resource-group RG      Resource group name"
    echo ""
    echo "Optional parameters:"
    echo "  -l, --location LOCATION      Azure region (default: East US)"
    echo "  -s, --subscription ID        Azure subscription ID"
    echo "  -p, --sql-password PWD       SQL Server admin password"
    echo "  -d, --dry-run               Show what would be deployed without executing"
    echo "  -y, --yes                   Skip confirmation prompts"
    echo "  -h, --help                  Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 -t bicep -e dev -g rg-bidone-dev"
    echo "  $0 -t terraform -e prod -g rg-bidone-prod -s 12345678-1234-1234-1234-123456789012"
    echo "  $0 --tool terraform --environment staging --resource-group rg-bidone-staging --dry-run"
}

# Function to validate parameters
validate_parameters() {
    if [[ -z "$TOOL" ]]; then
        print_error "Tool parameter is required"
        show_usage
        exit 1
    fi

    if [[ "$TOOL" != "bicep" && "$TOOL" != "terraform" ]]; then
        print_error "Tool must be either 'bicep' or 'terraform'"
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

    if [[ -z "$RESOURCE_GROUP" ]]; then
        print_error "Resource group parameter is required"
        show_usage
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

    if [[ "$TOOL" == "bicep" ]]; then
        # Check Bicep CLI
        if ! az bicep version &> /dev/null; then
            print_error "Bicep CLI is not installed. Please run 'az bicep install'"
            exit 1
        fi
    elif [[ "$TOOL" == "terraform" ]]; then
        # Check Terraform
        if ! command -v terraform &> /dev/null; then
            print_error "Terraform is not installed or not in PATH"
            exit 1
        fi
    fi

    print_success "Prerequisites check passed"
}

# Function to set Azure subscription
set_subscription() {
    if [[ -n "$SUBSCRIPTION_ID" ]]; then
        print_info "Setting Azure subscription to $SUBSCRIPTION_ID"
        az account set --subscription "$SUBSCRIPTION_ID"
    fi

    # Display current subscription
    CURRENT_SUB=$(az account show --query "{name:name, id:id}" -o tsv)
    print_info "Using Azure subscription: $CURRENT_SUB"
}

# Function to create resource group if it doesn't exist
ensure_resource_group() {
    print_info "Checking if resource group '$RESOURCE_GROUP' exists..."
    
    if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
        print_info "Resource group '$RESOURCE_GROUP' already exists"
    else
        print_info "Creating resource group '$RESOURCE_GROUP' in '$LOCATION'"
        if [[ "$DRY_RUN" == "false" ]]; then
            az group create --name "$RESOURCE_GROUP" --location "$LOCATION"
            print_success "Resource group created successfully"
        else
            print_info "[DRY RUN] Would create resource group '$RESOURCE_GROUP'"
        fi
    fi
}

# Function to prompt for SQL password if not provided
get_sql_password() {
    if [[ -z "$SQL_PASSWORD" ]]; then
        print_info "SQL Server admin password is required"
        read -s -p "Enter SQL Server admin password: " SQL_PASSWORD
        echo ""
        
        if [[ -z "$SQL_PASSWORD" ]]; then
            print_error "SQL password cannot be empty"
            exit 1
        fi
    fi
}

# Function to deploy using Bicep
deploy_bicep() {
    print_info "Deploying infrastructure using Bicep..."
    
    local bicep_dir="./bicep"
    local main_template="$bicep_dir/main.bicep"
    local parameters_file="$bicep_dir/parameters/${ENVIRONMENT}.json"
    
    if [[ ! -f "$main_template" ]]; then
        print_error "Bicep main template not found: $main_template"
        exit 1
    fi
    
    if [[ ! -f "$parameters_file" ]]; then
        print_error "Parameters file not found: $parameters_file"
        exit 1
    fi
    
    local deployment_name="bidone-infra-$(date +%Y%m%d-%H%M%S)"
    
    print_info "Deployment name: $deployment_name"
    print_info "Template: $main_template"
    print_info "Parameters: $parameters_file"
    
    if [[ "$DRY_RUN" == "true" ]]; then
        print_info "[DRY RUN] Running Bicep what-if analysis..."
        az deployment group what-if \
            --resource-group "$RESOURCE_GROUP" \
            --template-file "$main_template" \
            --parameters "@$parameters_file" \
            --parameters sqlAdminPassword="$SQL_PASSWORD"
    else
        az deployment group create \
            --resource-group "$RESOURCE_GROUP" \
            --name "$deployment_name" \
            --template-file "$main_template" \
            --parameters "@$parameters_file" \
            --parameters sqlAdminPassword="$SQL_PASSWORD"
        
        print_success "Bicep deployment completed successfully"
    fi
}

# Function to deploy using Terraform
deploy_terraform() {
    print_info "Deploying infrastructure using Terraform..."
    
    local terraform_dir="./terraform"
    local tfvars_file="$terraform_dir/environments/${ENVIRONMENT}.tfvars"
    
    if [[ ! -d "$terraform_dir" ]]; then
        print_error "Terraform directory not found: $terraform_dir"
        exit 1
    fi
    
    if [[ ! -f "$tfvars_file" ]]; then
        print_error "Terraform variables file not found: $tfvars_file"
        exit 1
    fi
    
    # Change to terraform directory
    cd "$terraform_dir"
    
    # Initialize Terraform
    print_info "Initializing Terraform..."
    if [[ "$DRY_RUN" == "false" ]]; then
        terraform init
    else
        print_info "[DRY RUN] Would run: terraform init"
    fi
    
    # Validate configuration
    print_info "Validating Terraform configuration..."
    if [[ "$DRY_RUN" == "false" ]]; then
        terraform validate
    else
        print_info "[DRY RUN] Would run: terraform validate"
    fi
    
    # Plan deployment
    print_info "Planning Terraform deployment..."
    local plan_file="terraform-${ENVIRONMENT}.tfplan"
    
    if [[ "$DRY_RUN" == "true" ]]; then
        print_info "[DRY RUN] Would run: terraform plan"
        terraform plan \
            -var-file="$tfvars_file" \
            -var="sql_admin_password=$SQL_PASSWORD" \
            -var="resource_group_name=$RESOURCE_GROUP"
    else
        terraform plan \
            -var-file="$tfvars_file" \
            -var="sql_admin_password=$SQL_PASSWORD" \
            -var="resource_group_name=$RESOURCE_GROUP" \
            -out="$plan_file"
        
        # Apply deployment
        if [[ "$SKIP_CONFIRMATION" == "false" ]]; then
            read -p "Do you want to apply this Terraform plan? (y/N): " -n 1 -r
            echo ""
            if [[ ! $REPLY =~ ^[Yy]$ ]]; then
                print_info "Deployment cancelled by user"
                exit 0
            fi
        fi
        
        print_info "Applying Terraform configuration..."
        terraform apply "$plan_file"
        
        print_success "Terraform deployment completed successfully"
    fi
    
    # Return to original directory
    cd - > /dev/null
}

# Function to show deployment summary
show_summary() {
    print_info "=== Deployment Summary ==="
    echo "Tool: $TOOL"
    echo "Environment: $ENVIRONMENT"
    echo "Resource Group: $RESOURCE_GROUP"
    echo "Location: $LOCATION"
    echo "Dry Run: $DRY_RUN"
    echo ""
}

# Main function
main() {
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            -t|--tool)
                TOOL="$2"
                shift 2
                ;;
            -e|--environment)
                ENVIRONMENT="$2"
                shift 2
                ;;
            -g|--resource-group)
                RESOURCE_GROUP="$2"
                shift 2
                ;;
            -l|--location)
                LOCATION="$2"
                shift 2
                ;;
            -s|--subscription)
                SUBSCRIPTION_ID="$2"
                shift 2
                ;;
            -p|--sql-password)
                SQL_PASSWORD="$2"
                shift 2
                ;;
            -d|--dry-run)
                DRY_RUN=true
                shift
                ;;
            -y|--yes)
                SKIP_CONFIRMATION=true
                shift
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

    # Show summary
    show_summary

    # Check prerequisites
    check_prerequisites

    # Set subscription
    set_subscription

    # Ensure resource group exists
    ensure_resource_group

    # Get SQL password
    get_sql_password

    # Confirm deployment (unless skip confirmation is set)
    if [[ "$SKIP_CONFIRMATION" == "false" && "$DRY_RUN" == "false" ]]; then
        read -p "Continue with deployment? (y/N): " -n 1 -r
        echo ""
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            print_info "Deployment cancelled by user"
            exit 0
        fi
    fi

    # Deploy using selected tool
    if [[ "$TOOL" == "bicep" ]]; then
        deploy_bicep
    elif [[ "$TOOL" == "terraform" ]]; then
        deploy_terraform
    fi

    if [[ "$DRY_RUN" == "false" ]]; then
        print_success "Infrastructure deployment completed successfully!"
        print_info "You can now use either Bicep or Terraform for future deployments"
    else
        print_info "Dry run completed. Use without --dry-run to execute the deployment"
    fi
}

# Run main function
main "$@"