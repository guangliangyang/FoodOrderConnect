# BidOne Infrastructure as Code (IaC)

This repository provides infrastructure deployment options using both **Bicep** and **Terraform**, giving DevOps teams the flexibility to choose the tool that best fits their workflow and expertise.

## 🛠️ Available Tools

### Bicep (Azure-native)
- **Location**: `./bicep/`
- **Best for**: Azure-focused teams, ARM template familiarity
- **Advantages**: Native Azure integration, smaller learning curve for Azure teams

### Terraform (Multi-cloud)
- **Location**: `./terraform/`
- **Best for**: Multi-cloud strategies, infrastructure standardization
- **Advantages**: Cloud-agnostic, mature ecosystem, state management

## 🚀 Quick Start

### Prerequisites
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Bicep CLI](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/install) (for Bicep deployments)
- [Terraform](https://www.terraform.io/downloads.html) (for Terraform deployments)

### Authentication
```bash
# Login to Azure
az login

# Set subscription (if needed)
az account set --subscription "your-subscription-id"
```

## 📋 Deployment Options

### Option 1: Using the Unified Deployment Script (Recommended)

The `deploy.sh` script supports both tools and provides a consistent deployment experience:

```bash
# Deploy using Bicep
./deploy.sh -t bicep -e dev -g rg-bidone-dev

# Deploy using Terraform
./deploy.sh -t terraform -e staging -g rg-bidone-staging

# Dry run to see what would be deployed
./deploy.sh -t terraform -e prod -g rg-bidone-prod --dry-run

# Skip confirmation prompts
./deploy.sh -t bicep -e dev -g rg-bidone-dev --yes
```

#### Script Parameters
- `-t, --tool`: Choose `bicep` or `terraform`
- `-e, --environment`: Target environment (`dev`, `staging`, `prod`)
- `-g, --resource-group`: Resource group name
- `-l, --location`: Azure region (default: East US)
- `-s, --subscription`: Azure subscription ID
- `-p, --sql-password`: SQL Server admin password
- `-d, --dry-run`: Preview changes without applying
- `-y, --yes`: Skip confirmation prompts

### Option 2: Direct Tool Usage

#### Bicep Deployment

```bash
cd bicep

# Deploy to development
az deployment group create \
  --resource-group rg-bidone-dev \
  --template-file main.bicep \
  --parameters @parameters/dev.json \
  --parameters sqlAdminPassword="YourSecurePassword123!"

# What-if analysis
az deployment group what-if \
  --resource-group rg-bidone-dev \
  --template-file main.bicep \
  --parameters @parameters/dev.json
```

#### Terraform Deployment

```bash
cd terraform

# Initialize
terraform init

# Plan deployment
terraform plan \
  -var-file="environments/dev.tfvars" \
  -var="sql_admin_password=YourSecurePassword123!" \
  -var="resource_group_name=rg-bidone-dev"

# Apply deployment
terraform apply \
  -var-file="environments/dev.tfvars" \
  -var="sql_admin_password=YourSecurePassword123!" \
  -var="resource_group_name=rg-bidone-dev"
```

## 🏗️ Infrastructure Components

Both tools deploy the same Azure resources:

### Core Services
- **Resource Group**: Container for all resources
- **Service Bus**: Message queuing for order processing
- **SQL Database**: Relational data storage
- **Cosmos DB**: NoSQL document storage
- **Redis Cache**: In-memory caching
- **Key Vault**: Secrets management
- **Storage Account**: Blob storage for functions and logs

### Application Services
- **Function Apps**: Serverless compute for order processing
- **Logic Apps**: Workflow orchestration
- **API Management**: API gateway and management
- **Container Apps**: Modern container hosting
- **Application Insights**: Application monitoring
- **Log Analytics**: Centralized logging

### Networking & Security
- **Virtual Network**: Network isolation
- **Network Security Groups**: Traffic filtering
- **Private Endpoints**: Secure private connectivity

## 🌍 Environment Configuration

### Development (`dev`)
- **Purpose**: Development and testing
- **SKUs**: Lower-cost options (Basic/Standard)
- **Features**: Free tier enabled where available
- **Backup**: Short retention periods

### Staging (`staging`)
- **Purpose**: Pre-production testing
- **SKUs**: Production-like sizing
- **Features**: Enhanced monitoring
- **Backup**: Medium retention periods

### Production (`prod`)
- **Purpose**: Live workloads
- **SKUs**: High-performance options
- **Features**: Zone redundancy, autoscaling
- **Backup**: Long retention periods

## 📁 Directory Structure

```
infra/
├── README.md                    # This file
├── deploy.sh                    # Unified deployment script
├── bicep/                       # Bicep templates
│   ├── main.bicep              # Main template
│   ├── parameters/             # Environment parameters
│   │   ├── dev.json
│   │   ├── staging.json
│   │   └── prod.json
│   ├── apim-config.bicep       # API Management configuration
│   └── logic-app-definition.json
├── terraform/                   # Terraform configuration
│   ├── main.tf                 # Main configuration
│   ├── variables.tf            # Variable definitions
│   ├── outputs.tf              # Output definitions
│   ├── shared/                 # Shared configuration
│   │   ├── variables.tf
│   │   ├── locals.tf
│   │   └── outputs.tf
│   ├── modules/                # Terraform modules
│   │   ├── service-bus/
│   │   ├── sql-server/
│   │   ├── cosmos-db/
│   │   └── redis/
│   └── environments/           # Environment-specific variables
│       ├── dev.tfvars
│       ├── staging.tfvars
│       └── prod.tfvars
```

## 🔐 Security Considerations

### Secrets Management
- SQL passwords should be stored in Azure Key Vault
- Use managed identities where possible
- Never commit secrets to version control

### Access Control
- Use Azure RBAC for resource access
- Implement least privilege principles
- Regular access reviews

### Network Security
- Private endpoints for database access
- Network security groups for traffic filtering
- VNet integration for Function Apps

## 🔄 CI/CD Integration

### Azure DevOps
```yaml
# Example pipeline step for Bicep
- task: AzureResourceManagerTemplateDeployment@3
  inputs:
    deploymentScope: 'Resource Group'
    azureResourceManagerConnection: '$(serviceConnection)'
    subscriptionId: '$(subscriptionId)'
    action: 'Create Or Update Resource Group'
    resourceGroupName: '$(resourceGroupName)'
    location: '$(location)'
    templateLocation: 'Linked artifact'
    csmFile: 'infra/bicep/main.bicep'
    csmParametersFile: 'infra/bicep/parameters/$(environment).json'
```

### GitHub Actions
```yaml
# Example workflow step for Terraform
- name: Terraform Apply
  run: |
    cd infra/terraform
    terraform init
    terraform plan -var-file="environments/${{ env.ENVIRONMENT }}.tfvars"
    terraform apply -auto-approve -var-file="environments/${{ env.ENVIRONMENT }}.tfvars"
```

## 🆘 Troubleshooting

### Common Issues

1. **Resource naming conflicts**
   - Ensure unique suffixes are used
   - Check for existing resources with same names

2. **Permission errors**
   - Verify Azure RBAC permissions
   - Ensure service principal has required roles

3. **Quota limitations**
   - Check Azure subscription quotas
   - Request quota increases if needed

### Getting Help
- Review Azure Activity Log for detailed error messages
- Use `az deployment group show` for Bicep deployment details
- Use `terraform show` for Terraform state information

## 📈 Monitoring and Maintenance

### Monitoring
- Application Insights for application telemetry
- Log Analytics for infrastructure logs
- Azure Monitor for alerts and dashboards

### Maintenance
- Regular backup testing
- Security patch management
- Cost optimization reviews

## 🤝 Contributing

When making infrastructure changes:
1. Update both Bicep and Terraform configurations
2. Test in development environment first
3. Update documentation as needed
4. Follow naming conventions

## 📚 Additional Resources

- [Azure Bicep Documentation](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [Terraform Azure Provider](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs)
- [Azure Architecture Center](https://docs.microsoft.com/en-us/azure/architecture/)
- [Azure Well-Architected Framework](https://docs.microsoft.com/en-us/azure/architecture/framework/)