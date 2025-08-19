# BidOne Integration Platform - Troubleshooting Guide

## 🎯 Overview

This document provides diagnosis and solutions for common issues with the BidOne Integration Platform, based on actual implemented components and configurations.

## 🔧 Quick Diagnostic Tools

### System Health Check

```bash
#!/bin/bash
# health-check.sh - Quick health status check

echo "=== BidOne Integration Platform Health Check ==="
echo "Check time: $(date)"

# Check local service status
echo "1. Local service status..."
docker-compose ps

# Check API endpoints
echo -e "\n2. API health check..."
curl -f http://localhost:8080/health || echo "External Order API: Failed"
curl -f http://localhost:8081/health || echo "Internal System API: Failed"

echo -e "\n=== Check completed ==="
```

## 🐛 Common Issues

### 1. Container Startup Failures

**Symptoms**: Docker containers fail to start or restart repeatedly

**Diagnostic Steps**:
```bash
# View container logs
docker-compose logs external-order-api
docker-compose logs internal-system-api

# Check container status
docker-compose ps
```

**Common Causes**:

#### a) Port Conflicts
```bash
# Check port usage
netstat -tulpn | grep :8080
netstat -tulpn | grep :8081

# Solution: Modify port mapping in docker-compose.yml
```

#### b) Database Connection Failures
```bash
# Ensure database service is started
docker-compose up sqlserver -d

# Check connection string configuration
cat src/InternalSystemApi/appsettings.json
```

### 2. Service Bus Connection Issues

**Symptoms**: Azure Functions cannot receive Service Bus messages

**Solution**:
```bash
# Check connection string configuration
cat src/OrderIntegrationFunction/local.settings.json

# Local development environment uses emulator
# Ensure Service Bus emulator is enabled in docker-compose.yml
```

### 3. AI Functionality Unresponsive

**Symptoms**: CustomerCommunicationFunction does not respond when processing high-value errors

**Diagnostic Steps**:
```csharp
// Check configuration in LangChainService.cs
// If no OpenAI API Key, system will use intelligent simulation mode
```

**Solution**:
- Local development: Use built-in intelligent simulation responses
- Production environment: Configure OpenAI API Key

### 4. Database Migration Issues

**Symptoms**: Entity Framework migration failures

**Solution**:
```bash
# Recreate database
docker-compose down
docker-compose up sqlserver -d
sleep 10

# Run migration
cd src/InternalSystemApi
dotnet ef database update
```

### 5. Grafana Dashboard No Data

**Symptoms**: Grafana shows "No data"

**Solution**:
```bash
# Ensure Prometheus is collecting metrics
curl http://localhost:9090/metrics

# Check Grafana data source configuration
# Access http://localhost:3000
# Default account: admin/admin
```

## 📊 Monitoring Queries

### Prometheus Query Examples

```promql
# API request rate
rate(http_requests_total[5m])

# Error rate
rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m])

# Response time
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
```

### Application Log Queries

```bash
# View recent error logs
docker-compose logs external-order-api | grep -i error | tail -20

# View AI processing logs
docker-compose logs customer-communication-function | grep -i "high-value"
```

## 🔧 Development Environment Reset

### Complete Environment Reset

```bash
#!/bin/bash
# reset-environment.sh - Complete development environment reset

echo "Stopping all services..."
docker-compose down -v

echo "Cleaning Docker resources..."
docker system prune -f

echo "Rebuilding images..."
docker-compose build --no-cache

echo "Starting services..."
docker-compose up -d

echo "Waiting for services to be ready..."
sleep 30

echo "Running health check..."
./scripts/health-check.sh
```

## 📝 Debugging Tips

### 1. Enable Detailed Logging

```csharp
// In appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information",
      "BidOne": "Debug"
    }
  }
}
```

### 2. Use Breakpoint Debugging

```bash
# Use VS Code debugging
# Ensure launch.json is configured correctly
# Can directly attach to running containers
```

### 3. Message Tracing

```csharp
// Add trace logs at key processing points
_logger.LogInformation("🔍 Processing order: {OrderId} at stage: {Stage}", 
    order.Id, "Validation");
```

## 🆘 Getting Help

### Log Collection Script

```bash
#!/bin/bash
# collect-logs.sh - Collect diagnostic information

LOG_DIR="./logs/$(date +%Y%m%d_%H%M%S)"
mkdir -p $LOG_DIR

echo "Collecting system information..."
docker-compose ps > $LOG_DIR/containers.txt
docker-compose logs > $LOG_DIR/all-logs.txt

echo "Collecting configuration information..."
cp docker-compose.yml $LOG_DIR/
cp -r config/ $LOG_DIR/

echo "Logs collected to: $LOG_DIR"
```

### Support Contact Information

- **Project Maintainer**: Ricky Yang
- **Email**: guangliang.yang@hotmail.com
- **Documentation**: See other documents in `/docs` directory

---

## 💡 Best Practices

1. **Regular Checks**: Use health check scripts to regularly verify system status
2. **Log Monitoring**: Pay attention to ERROR and WARNING level logs
3. **Performance Monitoring**: Use Grafana dashboards to monitor key metrics
4. **Version Control**: Keep configuration files in version control
5. **Documentation Updates**: Update this document promptly when encountering new issues

If you encounter issues not covered in this document, please contact the project maintainer or create a GitHub Issue.