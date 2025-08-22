#!/bin/bash

# Cost estimation script for pre-commit hook

set -e

echo "Running cost estimation check..."

# Check if infracost is available
if ! command -v infracost &> /dev/null; then
    echo "Warning: Infracost is not installed. Skipping cost check."
    echo "Install from: https://www.infracost.io/docs/"
    exit 0
fi

# Check if Infracost API key is set
if [[ -z "${INFRACOST_API_KEY}" ]]; then
    echo "Warning: INFRACOST_API_KEY environment variable not set. Skipping cost check."
    exit 0
fi

# Check Terraform configuration exists
if [[ ! -d "infra/terraform" ]]; then
    echo "No Terraform configuration found. Skipping cost check."
    exit 0
fi

cd infra/terraform

# Initialize Terraform for cost analysis
terraform init -backend=false > /dev/null 2>&1

# Run cost estimation for dev environment
echo "Estimating costs for dev environment..."
infracost breakdown --path . \
    --terraform-var-file="environments/dev.tfvars" \
    --terraform-var="sql_admin_password=dummy_password" \
    --terraform-var="resource_group_name=rg-test" \
    --format table

# Check if costs exceed thresholds (if configured)
if [[ -f "../../infracost.yml" ]]; then
    echo "Checking cost thresholds..."
    infracost breakdown --path . \
        --terraform-var-file="environments/dev.tfvars" \
        --terraform-var="sql_admin_password=dummy_password" \
        --terraform-var="resource_group_name=rg-test" \
        --format json > cost-estimate.json
    
    # Simple cost threshold check (can be enhanced)
    monthly_cost=$(jq -r '.totalMonthlyCost' cost-estimate.json 2>/dev/null || echo "0")
    
    if [[ "$monthly_cost" != "null" && "$monthly_cost" != "0" ]]; then
        # Convert to integer for comparison (remove decimal)
        cost_int=${monthly_cost%.*}
        if [[ $cost_int -gt 500 ]]; then
            echo "Warning: Estimated monthly cost ($monthly_cost USD) exceeds threshold ($500 USD)"
            echo "Consider optimizing resource configurations"
        fi
    fi
    
    rm -f cost-estimate.json
fi

cd ../..

echo "Cost estimation completed"