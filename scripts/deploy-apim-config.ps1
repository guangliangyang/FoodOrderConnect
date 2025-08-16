# API Management Configuration Deployment Script
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$ApimServiceName,
    
    [Parameter(Mandatory=$false)]
    [string]$ExternalOrderApiUrl = "http://localhost:5001",
    
    [Parameter(Mandatory=$false)]
    [string]$InternalSystemApiUrl = "http://localhost:5002",
    
    [Parameter(Mandatory=$false)]
    [string]$Environment = "dev",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus"
)

Write-Host "🚀 Starting API Management configuration deployment..." -ForegroundColor Green

try {
    # Check if Azure CLI is logged in
    $account = az account show --query "user.name" -o tsv 2>$null
    if (-not $account) {
        Write-Host "❌ Please login to Azure CLI first: az login" -ForegroundColor Red
        exit 1
    }

    Write-Host "✅ Logged in as: $account" -ForegroundColor Green

    # Deploy APIM configuration using Bicep
    Write-Host "📦 Deploying API Management configuration..." -ForegroundColor Yellow
    
    $deploymentName = "apim-config-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    
    az deployment group create `
        --resource-group $ResourceGroupName `
        --template-file "./infra/apim-config.bicep" `
        --parameters apimServiceName=$ApimServiceName `
                    externalOrderApiUrl=$ExternalOrderApiUrl `
                    internalSystemApiUrl=$InternalSystemApiUrl `
                    environmentName=$Environment `
                    location=$Location `
        --name $deploymentName `
        --verbose

    if ($LASTEXITCODE -ne 0) {
        throw "Bicep deployment failed"
    }

    Write-Host "✅ API Management configuration deployed successfully" -ForegroundColor Green

    # Apply policies
    Write-Host "📋 Applying API policies..." -ForegroundColor Yellow

    # Apply global policy
    Write-Host "   - Applying global policy..." -ForegroundColor Cyan
    az apim policy create `
        --resource-group $ResourceGroupName `
        --service-name $ApimServiceName `
        --policy-format "xml" `
        --value "./infra/policies/global-policy.xml"

    # Apply external API policy
    Write-Host "   - Applying external API policy..." -ForegroundColor Cyan
    az apim api policy create `
        --resource-group $ResourceGroupName `
        --service-name $ApimServiceName `
        --api-id "external-order-api" `
        --policy-format "xml" `
        --value "./infra/policies/external-api-policy.xml"

    # Apply internal API policy
    Write-Host "   - Applying internal API policy..." -ForegroundColor Cyan
    az apim api policy create `
        --resource-group $ResourceGroupName `
        --service-name $ApimServiceName `
        --api-id "internal-system-api" `
        --policy-format "xml" `
        --value "./infra/policies/internal-api-policy.xml"

    Write-Host "✅ All policies applied successfully" -ForegroundColor Green

    # Get API Management URLs
    Write-Host "🔗 Getting API Management URLs..." -ForegroundColor Yellow
    
    $apimGatewayUrl = az apim show `
        --resource-group $ResourceGroupName `
        --name $ApimServiceName `
        --query "gatewayUrl" `
        --output tsv

    if ($apimGatewayUrl) {
        Write-Host "📍 API Management Gateway URL: $apimGatewayUrl" -ForegroundColor Green
        Write-Host "📍 External Order API: $apimGatewayUrl/external" -ForegroundColor Green
        Write-Host "📍 Internal System API: $apimGatewayUrl/internal" -ForegroundColor Green
        Write-Host "📍 API Management Portal: https://$ApimServiceName.developer.azure-api.net" -ForegroundColor Green
    }

    # Test API endpoints
    Write-Host "🧪 Testing API endpoints..." -ForegroundColor Yellow
    
    try {
        $externalApiHealth = Invoke-WebRequest -Uri "$apimGatewayUrl/external/health" -Method GET -UseBasicParsing -TimeoutSec 10
        if ($externalApiHealth.StatusCode -eq 200) {
            Write-Host "✅ External API is responding" -ForegroundColor Green
        } else {
            Write-Host "⚠️  External API returned status: $($externalApiHealth.StatusCode)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "⚠️  External API health check failed (this is normal if services aren't deployed yet)" -ForegroundColor Yellow
    }

    Write-Host "🎉 API Management configuration completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Deploy your API services (ExternalOrderApi, InternalSystemApi)" -ForegroundColor White
    Write-Host "2. Update the backend URLs in API Management" -ForegroundColor White
    Write-Host "3. Configure OAuth/JWT authentication if needed" -ForegroundColor White
    Write-Host "4. Test the APIs through the API Management gateway" -ForegroundColor White

} catch {
    Write-Host "❌ Deployment failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}