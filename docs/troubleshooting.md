# BidOne Integration Platform - 故障排除指南

## 🎯 概述

本文档提供 BidOne Integration Platform 常见问题的诊断和解决方案，基于实际实现的组件和配置。

## 🔧 快速诊断工具

### 系统健康检查

```bash
#!/bin/bash
# health-check.sh - 快速健康状态检查

echo "=== BidOne Integration Platform 健康检查 ==="
echo "检查时间: $(date)"

# 检查本地服务状态
echo "1. 本地服务状态..."
docker-compose ps

# 检查API端点
echo -e "\n2. API健康检查..."
curl -f http://localhost:8080/health || echo "External Order API: 失败"
curl -f http://localhost:8081/health || echo "Internal System API: 失败"

echo -e "\n=== 检查完成 ==="
```

## 🐛 常见问题

### 1. 容器启动失败

**症状**: Docker容器无法启动或反复重启

**诊断步骤**:
```bash
# 查看容器日志
docker-compose logs external-order-api
docker-compose logs internal-system-api

# 检查容器状态
docker-compose ps
```

**常见原因**:

#### a) 端口冲突
```bash
# 检查端口占用
netstat -tulpn | grep :8080
netstat -tulpn | grep :8081

# 解决方案：修改 docker-compose.yml 中的端口映射
```

#### b) 数据库连接失败
```bash
# 确保数据库服务已启动
docker-compose up sqlserver -d

# 检查连接字符串配置
cat src/InternalSystemApi/appsettings.json
```

### 2. Service Bus 连接问题

**症状**: Azure Functions 无法接收 Service Bus 消息

**解决方案**:
```bash
# 检查连接字符串配置
cat src/OrderIntegrationFunction/local.settings.json

# 本地开发环境使用模拟器
# 确保在 docker-compose.yml 中启用了 Service Bus 模拟器
```

### 3. AI 功能无响应

**症状**: CustomerCommunicationFunction 处理高价值错误时无响应

**诊断步骤**:
```csharp
// 检查 LangChainService.cs 中的配置
// 如果没有 OpenAI API Key，系统会使用智能模拟模式
```

**解决方案**:
- 本地开发：使用内置的智能模拟响应
- 生产环境：配置 OpenAI API Key

### 4. 数据库迁移问题

**症状**: Entity Framework 迁移失败

**解决方案**:
```bash
# 重新创建数据库
docker-compose down
docker-compose up sqlserver -d
sleep 10

# 运行迁移
cd src/InternalSystemApi
dotnet ef database update
```

### 5. Grafana 仪表板无数据

**症状**: Grafana 显示 "No data" 

**解决方案**:
```bash
# 确保 Prometheus 正在收集指标
curl http://localhost:9090/metrics

# 检查 Grafana 数据源配置
# 访问 http://localhost:3000
# 默认账号: admin/admin
```

## 📊 监控查询

### Prometheus 查询示例

```promql
# API 请求率
rate(http_requests_total[5m])

# 错误率
rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m])

# 响应时间
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
```

### 应用日志查询

```bash
# 查看最近的错误日志
docker-compose logs external-order-api | grep -i error | tail -20

# 查看 AI 处理日志
docker-compose logs customer-communication-function | grep -i "high-value"
```

## 🔧 开发环境重置

### 完全重置环境

```bash
#!/bin/bash
# reset-environment.sh - 完全重置开发环境

echo "停止所有服务..."
docker-compose down -v

echo "清理 Docker 资源..."
docker system prune -f

echo "重新构建镜像..."
docker-compose build --no-cache

echo "启动服务..."
docker-compose up -d

echo "等待服务就绪..."
sleep 30

echo "运行健康检查..."
./scripts/health-check.sh
```

## 📝 调试技巧

### 1. 启用详细日志

```csharp
// 在 appsettings.Development.json 中
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

### 2. 使用断点调试

```bash
# 使用 VS Code 调试
# 确保 launch.json 配置正确
# 可以直接附加到运行中的容器
```

### 3. 消息追踪

```csharp
// 在关键处理点添加追踪日志
_logger.LogInformation("🔍 Processing order: {OrderId} at stage: {Stage}", 
    order.Id, "Validation");
```

## 🆘 获取帮助

### 日志收集脚本

```bash
#!/bin/bash
# collect-logs.sh - 收集诊断信息

LOG_DIR="./logs/$(date +%Y%m%d_%H%M%S)"
mkdir -p $LOG_DIR

echo "收集系统信息..."
docker-compose ps > $LOG_DIR/containers.txt
docker-compose logs > $LOG_DIR/all-logs.txt

echo "收集配置信息..."
cp docker-compose.yml $LOG_DIR/
cp -r config/ $LOG_DIR/

echo "日志已收集到: $LOG_DIR"
```

### 支持联系方式

- **项目维护者**: Ricky Yang
- **邮箱**: guangliang.yang@hotmail.com
- **文档**: 查看 `/docs` 目录中的其他文档

---

## 💡 最佳实践

1. **定期检查**: 使用健康检查脚本定期验证系统状态
2. **日志监控**: 关注 ERROR 和 WARNING 级别的日志
3. **性能监控**: 使用 Grafana 仪表板监控关键指标
4. **版本控制**: 保持配置文件在版本控制中
5. **文档更新**: 遇到新问题时及时更新此文档

如果遇到此文档未涵盖的问题，请联系项目维护者或创建 GitHub Issue。