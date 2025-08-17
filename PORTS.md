# BidOne Integration Platform - 端口配置说明

## 📋 端口分配表

### 🚀 应用服务端口
| 服务 | 本地开发模式 | 容器化模式 | HTTPS (本地) | 说明 |
|------|-------------|-----------|-------------|------|
| **External Order API** | http://localhost:5001 | http://localhost:5001 | https://localhost:7001 | 外部订单接收API |
| **Internal System API** | http://localhost:5002 | http://localhost:5002 | https://localhost:7002 | 内部系统API |
| **Order Integration Function** | http://localhost:7071 | N/A (本地运行) | N/A | 订单处理函数 |
| **AI Communication Function** | http://localhost:7072 | N/A (本地运行) | N/A | AI智能沟通函数 |

### 🏗️ 基础设施服务端口
| 服务 | 端口 | 用途 | 访问地址 |
|------|------|------|----------|
| **SQL Server** | 1433 | 主数据库 | localhost:1433 |
| **Redis** | 6379 | 缓存 | localhost:6379 |
| **Cosmos DB** | 8081 | 文档数据库 | https://localhost:8081 |
| **Service Bus** | 5672 | 消息队列 | localhost:5672 |
| **Prometheus** | 9090 | 指标收集 | http://localhost:9090 |
| **Grafana** | 3000 | 监控仪表板 | http://localhost:3000 |
| **Jaeger** | 16686 | 链路追踪 | http://localhost:16686 |
| **Nginx** | 80/443 | 反向代理 | http://localhost |

## 🔧 问题解决

### 原问题：端口冲突
- **问题**: 两个API项目 `dotnet run` 都默认使用5000端口
- **影响**: 无法同时在本地运行两个API

### 解决方案：端口分离
1. **创建 `launchSettings.json`** 配置文件
2. **分配专用端口**:
   - External Order API: 5001/7001
   - Internal System API: 5002/7002
3. **统一容器和本地端口**: 确保开发体验一致

### 配置文件位置
```
src/ExternalOrderApi/Properties/launchSettings.json
src/InternalSystemApi/Properties/launchSettings.json
```

## 🚀 使用方式

### 混合开发模式 (推荐日常开发)
```bash
# 1. 启动基础设施
./docker-dev.sh infra

# 2. 启动API服务 (不同端口)
cd src/ExternalOrderApi && dotnet run     # → http://localhost:5001
cd src/InternalSystemApi && dotnet run    # → http://localhost:5002

# 3. 启动Functions (不同端口)
cd src/OrderIntegrationFunction && func start              # → http://localhost:7071
cd src/CustomerCommunicationFunction && func start --port 7072  # → http://localhost:7072
```

### 完全容器化模式 (推荐演示)
```bash
# 一键启动，端口自动映射
./docker-dev.sh start

# API服务通过容器映射到相同端口
# External Order API: http://localhost:5001
# Internal System API: http://localhost:5002
```

## ✅ 验证端口配置

使用测试脚本验证端口是否正确配置：
```bash
./test-ports.sh
```

## 🎯 设计优势

1. **端口分离**: 避免了默认5000端口冲突
2. **一致性**: 本地开发和容器化模式使用相同端口
3. **可预测性**: 每个服务都有固定的端口分配
4. **开发友好**: 清晰的端口映射，便于调试和测试

---

**注意**: Azure Functions 始终在本地运行（不容器化），因为它们需要 Azure Functions Runtime。