# BidOne Integration Platform - Port Configuration Guide

## 📋 Port Allocation Table

### 🚀 Application Service Ports
| Service | Local Development Mode | Containerized Mode | HTTPS (Local) | Description |
|---------|----------------------|-------------------|---------------|-------------|
| **External Order API** | http://localhost:5001 | http://localhost:5001 | https://localhost:7001 | External order receipt API |
| **Internal System API** | http://localhost:5002 | http://localhost:5002 | https://localhost:7002 | Internal system API |
| **Order Integration Function** | http://localhost:7071 | N/A (runs locally) | N/A | Order processing function |
| **AI Communication Function** | http://localhost:7072 | N/A (runs locally) | N/A | AI intelligent communication function |

### 🏗️ Infrastructure Service Ports
| Service | Port | Purpose | Access Address |
|---------|------|---------|----------------|
| **SQL Server** | 1433 | Main database | localhost:1433 |
| **Redis** | 6380 | Cache | localhost:6380 |
| **Cosmos DB** | 8081 | Document database | https://localhost:8081 |
| **Service Bus** | 5672 | Message queue | localhost:5672 |
| **Prometheus** | 9090 | Metrics collection | http://localhost:9090 |
| **Grafana** | 3000 | Monitoring dashboard | http://localhost:3000 |
| **Jaeger** | 16686 | Distributed tracing | http://localhost:16686 |
| **Nginx** | 80/443 | Reverse proxy | http://localhost |

## 🔧 Problem Resolution

### Solved Issues: Port Conflicts

#### Issue 1: API Port Conflicts
- **Problem**: Both API projects using `dotnet run` default to port 5000
- **Impact**: Cannot run both APIs locally simultaneously
- **Solution**: Create `launchSettings.json` configuration files, assign dedicated ports

#### Issue 2: Redis Port Conflicts  
- **Problem**: Locally installed Redis occupies port 6379, conflicts with Docker Redis
- **Impact**: Application connects to wrong Redis instance, causing data inconsistency
- **Solution**: Map Docker Redis to port 6380, avoiding conflict with local Redis (6379)

### Port Allocation Strategy
1. **API Service Port Separation**:
   - External Order API: 5001/7001
   - Internal System API: 5002/7002
2. **Redis Port Separation**:
   - Local Redis: 6379 (if installed)
   - Docker Redis: 6380
3. **Unified Container and Local Ports**: Ensure consistent development experience

### Configuration File Locations
```
src/ExternalOrderApi/Properties/launchSettings.json
src/InternalSystemApi/Properties/launchSettings.json
```

## 🚀 Usage

### Hybrid Development Mode (Recommended for daily development)
```bash
# 1. Start infrastructure
./docker-dev.sh infra

# 2. Start API services (different ports)
cd src/ExternalOrderApi && dotnet run     # → http://localhost:5001
cd src/InternalSystemApi && dotnet run    # → http://localhost:5002

# 3. Start Functions (different ports)
cd src/OrderIntegrationFunction && func start              # → http://localhost:7071
cd src/CustomerCommunicationFunction && func start --port 7072  # → http://localhost:7072
```

### Complete Containerized Mode (Recommended for demos)
```bash
# One-click start, ports automatically mapped
./docker-dev.sh start

# API services mapped to same ports through containers
# External Order API: http://localhost:5001
# Internal System API: http://localhost:5002
```

## ✅ Verify Port Configuration

Use test script to verify ports are correctly configured:
```bash
./test-ports.sh
```

## 🎯 Design Advantages

1. **Port Separation**: Avoided default port 5000 conflicts
2. **Consistency**: Local development and containerized modes use same ports
3. **Predictability**: Each service has fixed port allocation
4. **Developer Friendly**: Clear port mapping, convenient for debugging and testing

---

**Note**: Azure Functions always run locally (not containerized) because they require the Azure Functions Runtime.