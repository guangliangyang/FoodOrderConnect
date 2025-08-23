# BidOne 测试指南

本文档提供了 BidOne 项目完整的测试策略、工具和最佳实践指南。

## 目录

- [测试架构](#测试架构)
- [测试类型](#测试类型)
- [测试环境设置](#测试环境设置)
- [运行测试](#运行测试)
- [测试覆盖率](#测试覆盖率)
- [CI/CD 集成](#cicd-集成)
- [最佳实践](#最佳实践)
- [故障排除](#故障排除)

## 测试架构

### 测试金字塔

```
        /\        集成测试
       /  \         (少量)
      /____\      
     /      \     单元测试
    /________\     (大量)
```

- **单元测试 (70%+)**: 测试单个组件的功能
- **集成测试 (20-25%)**: 测试组件间的交互
- **端到端测试 (5-10%)**: 测试完整的业务流程

### 项目结构

```
tests/
├── ExternalOrderApi.Tests/          # 外部订单API测试
│   ├── Controllers/                  # 控制器测试
│   ├── Services/                     # 服务层测试
│   └── Integration/                  # 集成测试
├── InternalSystemApi.Tests/          # 内部系统API测试
│   ├── Controllers/                  # 控制器测试
│   ├── Services/                     # 服务层测试
│   └── Data/                         # 数据层测试
├── OrderIntegrationFunction.Tests/   # 订单集成函数测试
│   └── Functions/                    # 函数测试
├── Shared.Tests/                     # 共享组件测试
│   ├── Domain/                       # 领域模型测试
│   ├── TestHelpers/                  # 测试辅助工具
│   └── TestData/                     # 测试数据
└── CustomerCommunicationFunction.Tests/ # 客户通信函数测试
```

## 测试类型

### 1. 单元测试

**目标**: 测试单个方法或类的功能

**特点**:
- 快速执行 (< 1秒)
- 隔离依赖项
- 使用模拟对象 (Mocks)

**示例**:
```csharp
[Fact]
public async Task CreateOrderAsync_WithValidRequest_ReturnsOrderResponse()
{
    // Arrange
    var request = new CreateOrderRequest
    {
        CustomerId = "CUST-001",
        Items = new List<CreateOrderItemRequest>
        {
            new() { ProductId = "PROD-001", Quantity = 2, UnitPrice = 25.00m }
        }
    };

    // Act
    var result = await _orderService.CreateOrderAsync(request, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.OrderId.Should().StartWith("ORD-");
    result.Status.Should().Be(OrderStatus.Received);
}
```

### 2. 集成测试

**目标**: 测试组件间的交互

**特点**:
- 使用真实数据库（内存数据库）
- 测试完整的请求-响应周期
- 验证数据持久化

**示例**:
```csharp
[Fact]
public async Task CreateOrder_WithValidRequest_ReturnsAccepted()
{
    // Arrange
    var request = new CreateOrderRequest
    {
        CustomerId = "CUST-INT-001",
        Items = new List<CreateOrderItemRequest>
        {
            new() { ProductId = "PROD-001", Quantity = 2, UnitPrice = 25.00m }
        }
    };

    // Act
    var response = await _client.PostAsJsonAsync("/api/orders", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Accepted);
}
```

### 3. 端到端测试

**目标**: 测试完整的业务流程

**特点**:
- 使用真实的外部服务
- 测试用户场景
- 验证系统集成点

## 测试环境设置

### 本地开发环境

1. **安装依赖**:
   ```bash
   # 安装 .NET 8 SDK
   dotnet --version
   
   # 安装 Docker
   docker --version
   ```

2. **设置测试环境**:
   ```bash
   # 自动设置测试服务
   ./scripts/setup-test-environment.sh
   
   # 或者手动设置
   ./scripts/setup-test-environment.sh --clean
   ```

3. **验证环境**:
   ```bash
   # 检查服务状态
   docker-compose -f docker-compose.test.yml ps
   
   # 测试数据库连接
   dotnet ef database update --project src/InternalSystemApi
   ```

### CI/CD 环境

测试在 GitHub Actions 中自动运行：

- **构建和单元测试**: 每次 push 和 PR
- **集成测试**: 主分支合并时
- **覆盖率报告**: 自动生成并上传

## 运行测试

### 命令行运行

```bash
# 运行所有测试
./scripts/run-tests.sh

# 运行单元测试
./scripts/run-tests.sh --unit-only

# 运行集成测试
./scripts/run-tests.sh --integration-only

# 设置覆盖率阈值
./scripts/run-tests.sh --threshold 85

# 详细输出
./scripts/run-tests.sh --verbose
```

### IDE 中运行

**Visual Studio**:
1. 打开测试资源管理器
2. 选择要运行的测试
3. 右键 -> 运行测试

**VS Code**:
1. 安装 C# 扩展
2. 使用 Ctrl+Shift+P -> ".NET: Run Tests"
3. 在测试文件中点击 "Run Test" 链接

### Docker 中运行

```bash
# 构建测试镜像
docker build -t bidone-tests -f Dockerfile.test .

# 运行测试
docker run --rm \
  -v $(pwd)/TestResults:/app/TestResults \
  bidone-tests
```

## 测试覆盖率

### 覆盖率目标

- **最低要求**: 80%
- **推荐目标**: 85%+
- **关键组件**: 90%+

### 覆盖率报告

生成覆盖率报告：

```bash
# 运行测试并生成覆盖率报告
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# 生成 HTML 报告
reportgenerator \
  "-reports:TestResults/**/coverage.opencover.xml" \
  "-targetdir:TestResults/coverage-report" \
  "-reporttypes:Html;Cobertura"
```

### 查看覆盖率报告

```bash
# 打开 HTML 报告
open TestResults/coverage-report/index.html

# 或在浏览器中查看
file://$(pwd)/TestResults/coverage-report/index.html
```

## CI/CD 集成

### GitHub Actions 工作流

1. **build-and-test.yml**: 主要的构建和测试工作流
2. **infrastructure-deploy.yml**: 基础设施部署（包含基础设施测试）

### 工作流触发条件

```yaml
on:
  push:
    branches: [ main, develop ]
    paths:
      - 'src/**'
      - 'tests/**'
  pull_request:
    branches: [ main, develop ]
```

### 测试阶段

1. **代码检出**
2. **依赖安装**
3. **编译构建**
4. **单元测试**
5. **集成测试**
6. **覆盖率分析**
7. **安全扫描**
8. **质量检查**

## 最佳实践

### 测试命名约定

```csharp
// 格式: MethodName_Scenario_ExpectedResult
[Fact]
public void CreateOrder_WithValidRequest_ReturnsOrderResponse()

[Fact]
public void CreateOrder_WithInvalidCustomerId_ThrowsValidationException()

[Theory]
[InlineData(0, "Quantity must be greater than 0")]
[InlineData(-1, "Quantity must be greater than 0")]
public void CreateOrder_WithInvalidQuantity_ThrowsValidationException(int quantity, string expectedError)
```

### AAA 模式

```csharp
[Fact]
public async Task TestMethod()
{
    // Arrange - 准备测试数据和依赖
    var service = new OrderService(_mockPublisher.Object, _mockLogger.Object);
    var request = new CreateOrderRequest { ... };

    // Act - 执行被测试的方法
    var result = await service.CreateOrderAsync(request, CancellationToken.None);

    // Assert - 验证结果
    result.Should().NotBeNull();
    result.OrderId.Should().StartWith("ORD-");
}
```

### 测试数据管理

```csharp
// 使用测试数据工厂
public class OrderTestDataFactory
{
    public static Order CreateValidOrder(string customerId = "CUST-001")
    {
        return new Order
        {
            OrderId = GenerateOrderId(),
            CustomerId = customerId,
            Items = new List<OrderItem>
            {
                new() { ProductId = "PROD-001", Quantity = 1, UnitPrice = 10.00m }
            }
        };
    }
}
```

### 模拟对象使用

```csharp
// 设置模拟行为
_mockMessagePublisher
    .Setup(x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);

// 验证方法调用
_mockMessagePublisher.Verify(
    x => x.PublishMessageAsync(
        "orders.received",
        It.Is<Order>(o => o.OrderId == expectedOrderId),
        It.IsAny<CancellationToken>()),
    Times.Once);
```

### 测试分类

```csharp
[Fact]
[Trait("Category", "Unit")]
public void UnitTest() { }

[Fact]
[Trait("Category", "Integration")]
public void IntegrationTest() { }

[Fact]
[Trait("Category", "E2E")]
public void EndToEndTest() { }
```

## 故障排除

### 常见问题

1. **测试数据库连接失败**
   ```bash
   # 检查 Docker 容器状态
   docker ps
   
   # 重新启动测试环境
   ./scripts/setup-test-environment.sh --clean
   ```

2. **测试超时**
   ```csharp
   // 增加测试超时时间
   [Fact(Timeout = 30000)] // 30 seconds
   public async Task LongRunningTest()
   ```

3. **内存泄漏**
   ```csharp
   // 确保正确释放资源
   public class TestClass : IDisposable
   {
       private readonly DbContext _context;
       
       public void Dispose()
       {
           _context?.Dispose();
       }
   }
   ```

4. **并发测试失败**
   ```csharp
   // 使用不同的测试数据
   var uniqueId = Guid.NewGuid().ToString();
   var customerId = $"CUST-{uniqueId}";
   ```

### 调试技巧

1. **输出测试日志**
   ```csharp
   public class TestClass : ITestOutputHelper
   {
       private readonly ITestOutputHelper _output;
       
       public TestClass(ITestOutputHelper output)
       {
           _output = output;
       }
       
       [Fact]
       public void Test()
       {
           _output.WriteLine($"Testing with value: {value}");
       }
   }
   ```

2. **条件编译**
   ```csharp
   #if DEBUG
       // 仅在调试模式下运行的代码
       Console.WriteLine("Debug information");
   #endif
   ```

3. **测试环境变量**
   ```bash
   export ASPNETCORE_ENVIRONMENT=Testing
   export ConnectionStrings__DefaultConnection="..."
   dotnet test
   ```

### 性能优化

1. **并行测试执行**
   ```xml
   <!-- xunit.runner.json -->
   {
     "parallelizeAssembly": true,
     "parallelizeTestCollections": true,
     "maxParallelThreads": 4
   }
   ```

2. **测试数据缓存**
   ```csharp
   public class TestFixture : IDisposable
   {
       // 共享测试数据
       public static readonly Order SharedOrder = CreateTestOrder();
   }
   ```

3. **选择性测试运行**
   ```bash
   # 仅运行快速测试
   dotnet test --filter Category=Unit
   
   # 排除慢速测试
   dotnet test --filter Category!=E2E
   ```

## 总结

- 保持高测试覆盖率 (≥80%)
- 优先编写单元测试
- 使用 AAA 模式
- 遵循命名约定
- 定期运行完整测试套件
- 监控测试性能
- 及时修复失败的测试

更多信息请参考：
- [开发者指南](developer-guide.md)
- [架构文档](architecture.md)
- [部署指南](deployment-guide.md)
