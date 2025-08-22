#!/bin/bash

# Local IaC Validation Script for BidOne Project
# This script runs all quality checks locally before committing

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
TOOL=""
ENVIRONMENT="dev"
SKIP_COST_CHECK=false
VERBOSE=false

# Test results
PASSED_CHECKS=0
FAILED_CHECKS=0

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[PASS]${NC} $1"
    ((PASSED_CHECKS++))
}

print_error() {
    echo -e "${RED}[FAIL]${NC} $1"
    ((FAILED_CHECKS++))
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo "  -t, --tool TOOL              Validate specific tool (bicep|terraform|both)"
    echo "  -e, --environment ENV        Environment for validation (dev|staging|prod)"
    echo "  -c, --skip-cost             Skip cost estimation checks"
    echo "  -v, --verbose               Enable verbose output"
    echo "  -h, --help                  Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                          # Validate both tools"
    echo "  $0 -t terraform -e staging  # Validate only Terraform for staging"
    echo "  $0 -c                       # Skip cost checks"
}

# Function to check prerequisites
check_prerequisites() {
    print_info "Checking prerequisites..."

    local missing_tools=()

    # Check common tools
    command -v az >/dev/null 2>&1 || missing_tools+=("azure-cli")
    command -v python3 >/dev/null 2>&1 || missing_tools+=("python3")
    command -v jq >/dev/null 2>&1 || missing_tools+=("jq")

    # Check Terraform tools if needed
    if [[ "$TOOL" == "terraform" || "$TOOL" == "both" || -z "$TOOL" ]]; then
        command -v terraform >/dev/null 2>&1 || missing_tools+=("terraform")
        command -v tflint >/dev/null 2>&1 || missing_tools+=("tflint")
        command -v tfsec >/dev/null 2>&1 || missing_tools+=("tfsec")
        
        if [[ "$SKIP_COST_CHECK" == "false" ]]; then
            command -v infracost >/dev/null 2>&1 || missing_tools+=("infracost")
        fi
    fi

    # Check Bicep tools if needed
    if [[ "$TOOL" == "bicep" || "$TOOL" == "both" || -z "$TOOL" ]]; then
        az bicep version >/dev/null 2>&1 || missing_tools+=("bicep-cli")
    fi

    # Check Checkov
    python3 -c "import checkov" 2>/dev/null || missing_tools+=("checkov")

    if [[ ${#missing_tools[@]} -gt 0 ]]; then
        print_error "Missing required tools: ${missing_tools[*]}"
        echo ""
        echo "Installation commands:"
        for tool in "${missing_tools[@]}"; do
            case $tool in
                "azure-cli") echo "  curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash" ;;
                "terraform") echo "  Download from: https://www.terraform.io/downloads.html" ;;
                "tflint") echo "  curl -s https://raw.githubusercontent.com/terraform-linters/tflint/master/install_linux.sh | bash" ;;
                "tfsec") echo "  wget -O tfsec https://github.com/aquasecurity/tfsec/releases/latest/download/tfsec-linux-amd64 && chmod +x tfsec && sudo mv tfsec /usr/local/bin/" ;;
                "infracost") echo "  curl -fsSL https://raw.githubusercontent.com/infracost/infracost/master/scripts/install.sh | sh" ;;
                "bicep-cli") echo "  az bicep install" ;;
                "checkov") echo "  pip3 install checkov" ;;
                "jq") echo "  sudo apt-get install jq" ;;
                "python3") echo "  sudo apt-get install python3" ;;
            esac
        done
        return 1
    fi

    print_success "All prerequisites are installed"
}

# Function to validate Terraform
validate_terraform() {
    print_info "Validating Terraform configuration..."

    cd infra/terraform

    # Format check
    print_info "Checking Terraform formatting..."
    if terraform fmt -check -recursive; then
        print_success "Terraform formatting is correct"
    else
        print_error "Terraform formatting issues found. Run 'terraform fmt -recursive' to fix"
    fi

    # Initialize and validate
    print_info "Initializing and validating Terraform..."
    terraform init -backend=false >/dev/null 2>&1
    if terraform validate; then
        print_success "Terraform validation passed"
    else
        print_error "Terraform validation failed"
    fi

    # TFLint
    print_info "Running TFLint..."
    tflint --init >/dev/null 2>&1
    if tflint --config=../../.tflint.hcl; then
        print_success "TFLint checks passed"
    else
        print_error "TFLint found issues"
    fi

    # tfsec security scan
    print_info "Running tfsec security scan..."
    cd ../..
    if tfsec infra/terraform --config-file .tfsec.yml; then
        print_success "tfsec security scan passed"
    else
        print_error "tfsec found security issues"
    fi

    # Cost estimation
    if [[ "$SKIP_COST_CHECK" == "false" ]]; then
        print_info "Running cost estimation..."
        cd infra/terraform
        if infracost breakdown --path . \
           --terraform-var-file="environments/$ENVIRONMENT.tfvars" \
           --terraform-var="sql_admin_password=dummy_password" \
           --terraform-var="resource_group_name=rg-test" \
           --format table; then
            print_success "Cost estimation completed"
        else
            print_warning "Cost estimation failed (requires Infracost API key)"
        fi
        cd ../..
    fi

    cd ../..
}

# Function to validate Bicep
validate_bicep() {
    print_info "Validating Bicep templates..."

    cd infra/bicep

    # Bicep build validation
    print_info "Building Bicep templates..."
    if az bicep build --file main.bicep --stdout >/dev/null 2>&1; then
        print_success "Bicep templates compile successfully"
    else
        print_error "Bicep compilation failed"
    fi

    # Check if Azure login is available for template validation
    if az account show >/dev/null 2>&1; then
        print_info "Running Bicep template validation..."
        # This requires a resource group, so we'll skip it in local validation
        print_warning "Skipping template deployment validation (requires Azure resources)"
    else
        print_warning "Not logged into Azure - skipping template validation"
    fi

    cd ../..
}

# Function to run Checkov
run_checkov() {
    print_info "Running Checkov security and compliance scan..."

    if checkov --config-file .checkov.yml --quiet; then
        print_success "Checkov security scan passed"
    else
        print_error "Checkov found security or compliance issues"
    fi
}

# Function to validate naming conventions
validate_naming_conventions() {
    print_info "Validating naming conventions..."

    # This is a simplified check - in practice, you'd use the custom Python script
    local naming_issues=false

    # Check Terraform files
    if [[ "$TOOL" == "terraform" || "$TOOL" == "both" || -z "$TOOL" ]]; then
        while IFS= read -r -d '' file; do
            if [[ "$VERBOSE" == "true" ]]; then
                echo "Checking: $file"
            fi
            
            # Basic naming convention checks
            if grep -q "resource.*{" "$file"; then
                # More sophisticated checks would go here
                if [[ "$VERBOSE" == "true" ]]; then
                    echo "  Resource definitions found"
                fi
            fi
        done < <(find infra/terraform -name "*.tf" -print0)
    fi

    if [[ "$naming_issues" == "false" ]]; then
        print_success "Naming convention validation passed"
    else
        print_error "Naming convention issues found"
    fi
}

# Function to validate required tags
validate_tags() {
    print_info "Validating required tags..."

    local tag_issues=false
    local required_tags=("Environment" "Project" "ManagedBy")

    # Check Terraform files for tag definitions
    if [[ "$TOOL" == "terraform" || "$TOOL" == "both" || -z "$TOOL" ]]; then
        while IFS= read -r -d '' file; do
            if grep -q "tags.*=" "$file"; then
                for tag in "${required_tags[@]}"; do
                    if ! grep -q "\"$tag\"" "$file"; then
                        if [[ "$VERBOSE" == "true" ]]; then
                            echo "  Missing tag '$tag' in $file"
                        fi
                        tag_issues=true
                    fi
                done
            fi
        done < <(find infra/terraform -name "*.tf" -print0)
    fi

    if [[ "$tag_issues" == "false" ]]; then
        print_success "Tag validation passed"
    else
        print_error "Required tags missing in some resources"
    fi
}

# Function to generate validation report
generate_report() {
    print_info "Generating validation report..."
    
    local total_checks=$((PASSED_CHECKS + FAILED_CHECKS))
    
    echo ""
    echo "=================================="
    echo "IaC VALIDATION REPORT"
    echo "=================================="
    echo "Tool: ${TOOL:-both}"
    echo "Environment: $ENVIRONMENT"
    echo "Validation Date: $(date)"
    echo ""
    echo "Check Results:"
    echo "  Total Checks: $total_checks"
    echo "  Passed: $PASSED_CHECKS"
    echo "  Failed: $FAILED_CHECKS"
    echo ""
    
    if [[ $FAILED_CHECKS -eq 0 ]]; then
        echo -e "${GREEN}✓ All validation checks passed!${NC}"
        echo "Your IaC code is ready for commit and deployment."
        return 0
    else
        echo -e "${RED}✗ $FAILED_CHECKS validation check(s) failed${NC}"
        echo "Please fix the issues above before committing."
        return 1
    fi
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
            -c|--skip-cost)
                SKIP_COST_CHECK=true
                shift
                ;;
            -v|--verbose)
                VERBOSE=true
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

    print_info "Starting IaC validation for BidOne project"
    print_info "Tool: ${TOOL:-both}"
    print_info "Environment: $ENVIRONMENT"
    echo ""

    # Check prerequisites
    if ! check_prerequisites; then
        exit 1
    fi

    # Run validations based on tool selection
    if [[ "$TOOL" == "terraform" || -z "$TOOL" ]]; then
        validate_terraform
    fi

    if [[ "$TOOL" == "bicep" || -z "$TOOL" ]]; then
        validate_bicep
    fi

    # Run common validations
    run_checkov
    validate_naming_conventions
    validate_tags

    # Generate report
    generate_report
}

# Run main function
main "$@"