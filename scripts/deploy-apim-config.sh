#!/bin/bash

# API Management Configuration Deployment Script
set -e

# Default values
EXTERNAL_ORDER_API_URL="http://localhost:5001"
INTERNAL_SYSTEM_API_URL="http://localhost:5002"
ENVIRONMENT="dev"
LOCATION="eastus"

# Function to display usage
usage() {
    echo "Usage: $0 -g <resource-group> -n <apim-service-name> [options]"
    echo ""
    echo "Required:"
    echo "  -g, --resource-group      Resource group name"
    echo "  -n, --apim-name          API Management service name"
    echo ""
    echo "Optional:"
    echo "  -e, --external-api-url   External Order API URL (default: $EXTERNAL_ORDER_API_URL)"
    echo "  -i, --internal-api-url   Internal System API URL (default: $INTERNAL_SYSTEM_API_URL)"
    echo "  --environment            Environment name (default: $ENVIRONMENT)"
    echo "  --location               Azure location (default: $LOCATION)"
    echo "  -h, --help               Show this help message"
    echo ""
    echo "Example:"
    echo "  $0 -g bidone-demo-rg -n bidone-apim-dev-abc123"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -g|--resource-group)
            RESOURCE_GROUP="$2"
            shift 2
            ;;
        -n|--apim-name)
            APIM_SERVICE_NAME="$2"
            shift 2
            ;;
        -e|--external-api-url)
            EXTERNAL_ORDER_API_URL="$2"
            shift 2
            ;;
        -i|--internal-api-url)
            INTERNAL_SYSTEM_API_URL="$2"
            shift 2
            ;;
        --environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        --location)
            LOCATION="$2"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            usage
            exit 1
            ;;
    esac
done

# Validate required parameters
if [ -z "$RESOURCE_GROUP" ] || [ -z "$APIM_SERVICE_NAME" ]; then
    echo "❌ Error: Resource group and APIM service name are required"
    usage
    exit 1
fi

echo "🚀 Starting API Management configuration deployment..."

# Check if Azure CLI is logged in
if ! az account show >/dev/null 2>&1; then
    echo "❌ Please login to Azure CLI first: az login"
    exit 1
fi

ACCOUNT=$(az account show --query "user.name" -o tsv)
echo "✅ Logged in as: $ACCOUNT"

# Deploy APIM configuration using Bicep
echo "📦 Deploying API Management configuration..."

DEPLOYMENT_NAME="apim-config-$(date +%Y%m%d-%H%M%S)"

az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file "./infra/apim-config.bicep" \
    --parameters apimServiceName="$APIM_SERVICE_NAME" \
                externalOrderApiUrl="$EXTERNAL_ORDER_API_URL" \
                internalSystemApiUrl="$INTERNAL_SYSTEM_API_URL" \
                environmentName="$ENVIRONMENT" \
                location="$LOCATION" \
    --name "$DEPLOYMENT_NAME" \
    --verbose

echo "✅ API Management configuration deployed successfully"

# Apply policies
echo "📋 Applying API policies..."

# Apply global policy
echo "   - Applying global policy..."
az apim policy create \
    --resource-group "$RESOURCE_GROUP" \
    --service-name "$APIM_SERVICE_NAME" \
    --policy-format "xml" \
    --value "@./infra/policies/global-policy.xml"

# Apply external API policy
echo "   - Applying external API policy..."
az apim api policy create \
    --resource-group "$RESOURCE_GROUP" \
    --service-name "$APIM_SERVICE_NAME" \
    --api-id "external-order-api" \
    --policy-format "xml" \
    --value "@./infra/policies/external-api-policy.xml"

# Apply internal API policy
echo "   - Applying internal API policy..."
az apim api policy create \
    --resource-group "$RESOURCE_GROUP" \
    --service-name "$APIM_SERVICE_NAME" \
    --api-id "internal-system-api" \
    --policy-format "xml" \
    --value "@./infra/policies/internal-api-policy.xml"

echo "✅ All policies applied successfully"

# Get API Management URLs
echo "🔗 Getting API Management URLs..."

APIM_GATEWAY_URL=$(az apim show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$APIM_SERVICE_NAME" \
    --query "gatewayUrl" \
    --output tsv)

if [ -n "$APIM_GATEWAY_URL" ]; then
    echo "📍 API Management Gateway URL: $APIM_GATEWAY_URL"
    echo "📍 External Order API: $APIM_GATEWAY_URL/external"
    echo "📍 Internal System API: $APIM_GATEWAY_URL/internal"
    echo "📍 API Management Portal: https://$APIM_SERVICE_NAME.developer.azure-api.net"
fi

# Test API endpoints
echo "🧪 Testing API endpoints..."

if curl -s -f -m 10 "$APIM_GATEWAY_URL/external/health" >/dev/null 2>&1; then
    echo "✅ External API is responding"
else
    echo "⚠️  External API health check failed (this is normal if services aren't deployed yet)"
fi

echo ""
echo "🎉 API Management configuration completed successfully!"
echo ""
echo "Next steps:"
echo "1. Deploy your API services (ExternalOrderApi, InternalSystemApi)"
echo "2. Update the backend URLs in API Management"
echo "3. Configure OAuth/JWT authentication if needed"
echo "4. Test the APIs through the API Management gateway"