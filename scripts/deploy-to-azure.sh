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