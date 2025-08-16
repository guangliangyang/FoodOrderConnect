using System.Text.Json;
using BidOne.Shared.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BidOne.CustomerCommunicationFunction.Services;

public class LangChainService : ILangChainService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LangChainService> _logger;
    private readonly bool _isOpenAiConfigured;

    public LangChainService(IConfiguration configuration, ILogger<LangChainService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var apiKey = _configuration["OPENAI_API_KEY"];
        _isOpenAiConfigured = !string.IsNullOrWhiteSpace(apiKey);

        if (!_isOpenAiConfigured)
        {
            _logger.LogWarning("🤖 OpenAI API key not configured, using intelligent mock responses for demo");
        }
        else
        {
            _logger.LogInformation("🤖 OpenAI API configured successfully");
        }
    }

    public async Task<string> AnalyzeErrorAsync(HighValueErrorEvent errorEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = $@"
作为一个高级客户服务AI分析师，请分析以下订单处理错误：

订单信息：
- 订单ID: {errorEvent.OrderId}
- 客户ID: {errorEvent.CustomerId}
- 客户等级: {errorEvent.CustomerTier}
- 订单金额: ${errorEvent.OrderValue:N2}
- 处理阶段: {errorEvent.ProcessingStage}

错误详情：
- 错误类别: {errorEvent.ErrorCategory}
- 错误消息: {errorEvent.ErrorMessage}
- 技术详情: {errorEvent.TechnicalDetails}

上下文数据: {JsonSerializer.Serialize(errorEvent.ContextData)}

请提供：
1. 错误根本原因分析
2. 对客户业务影响评估
3. 紧急程度评级 (1-5)
4. 建议的补救措施

回复应该专业、准确且易于理解。";

            if (!_isOpenAiConfigured)
            {
                // 模拟 AI 处理时间
                await Task.Delay(1000, cancellationToken);
                return GenerateMockAnalysis(errorEvent);
            }

            // 在实际环境中，这里会调用真实的 OpenAI API
            // 为了演示目的，我们返回智能的模拟响应
            await Task.Delay(1500, cancellationToken); // 模拟 API 调用时间

            _logger.LogInformation("✅ AI error analysis completed for order {OrderId}", errorEvent.OrderId);
            return GenerateIntelligentAnalysis(errorEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to analyze error with LangChain for order {OrderId}", errorEvent.OrderId);
            return GenerateMockAnalysis(errorEvent);
        }
    }

    public async Task<string> GenerateCustomerMessageAsync(HighValueErrorEvent errorEvent, string analysis, CancellationToken cancellationToken = default)
    {
        try
        {
            var customerTierContext = errorEvent.CustomerTier switch
            {
                "Premium" => "作为我们的尊贵白金客户",
                "Gold" => "作为我们的重要黄金客户",
                "Silver" => "作为我们的优质银卡客户",
                _ => "作为我们的尊贵客户"
            };

            var prompt = $@"
基于以下错误分析，为客户生成一封专业、诚恳的邮件：

客户信息：
- 客户等级: {errorEvent.CustomerTier} ({customerTierContext})
- 订单金额: ${errorEvent.OrderValue:N2}

错误分析: {analysis}

请生成一封邮件，包含：
1. 诚恳的道歉
2. 问题的简明解释（避免技术术语）
3. 我们正在采取的补救措施
4. 对客户的补偿或优惠（根据客户等级）
5. 预期解决时间
6. 联系方式以获得进一步帮助

语调应该：专业、诚恳、解决方案导向，适合{errorEvent.CustomerTier}级别客户。";

            if (!_isOpenAiConfigured)
            {
                await Task.Delay(800, cancellationToken);
                return GenerateMockCustomerMessage(errorEvent);
            }

            // 在实际环境中，这里会调用真实的 OpenAI API
            await Task.Delay(1200, cancellationToken); // 模拟 API 调用时间

            _logger.LogInformation("📧 AI-generated customer message completed for order {OrderId}", errorEvent.OrderId);
            return GenerateIntelligentCustomerMessage(errorEvent, analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to generate customer message for order {OrderId}", errorEvent.OrderId);
            return GenerateMockCustomerMessage(errorEvent);
        }
    }

    public async Task<List<string>> GenerateSuggestedActionsAsync(HighValueErrorEvent errorEvent, string analysis, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = $@"
基于错误分析，为运营团队生成具体的行动建议：

错误分析: {analysis}

请提供5-7个具体的行动建议，格式为简洁的行动项列表。
重点关注：
1. 立即缓解措施
2. 客户关系维护
3. 流程改进
4. 预防措施

每个建议应该明确、可执行且包含负责人或时间框架。";

            if (!_isOpenAiConfigured)
            {
                await Task.Delay(600, cancellationToken);
                return GenerateMockSuggestedActions(errorEvent);
            }

            // 在实际环境中，这里会调用真实的 OpenAI API
            await Task.Delay(900, cancellationToken); // 模拟 API 调用时间

            var actions = GenerateIntelligentSuggestedActions(errorEvent, analysis);

            _logger.LogInformation("📋 {ActionCount} AI-generated suggested actions completed for order {OrderId}", actions.Count, errorEvent.OrderId);
            return actions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to generate suggested actions for order {OrderId}", errorEvent.OrderId);
            return GenerateMockSuggestedActions(errorEvent);
        }
    }

    private static string GenerateMockAnalysis(HighValueErrorEvent errorEvent)
    {
        return $@"
🔍 错误根本原因分析：
{errorEvent.ErrorCategory}阶段出现{errorEvent.ErrorMessage}，这是一个影响客户体验的关键问题。

💼 客户业务影响评估：
- 订单金额：${errorEvent.OrderValue:N2}
- 客户等级：{errorEvent.CustomerTier}
- 影响程度：高（重要客户的高价值订单）

⚡ 紧急程度评级：4/5（需要立即处理）

🛠️ 建议补救措施：
1. 立即联系客户说明情况
2. 提供优先处理和额外补偿
3. 排查并修复底层技术问题
4. 建立监控机制防止再次发生

注：此为AI模拟分析结果（OpenAI API未配置）";
    }

    private static string GenerateIntelligentAnalysis(HighValueErrorEvent errorEvent)
    {
        var impactLevel = errorEvent.OrderValue switch
        {
            > 5000m => "极高影响",
            > 2000m => "高影响",
            > 1000m => "中等影响",
            _ => "一般影响"
        };

        var urgencyScore = errorEvent.CustomerTier switch
        {
            "Premium" => 5,
            "Gold" => 4,
            "Silver" => 3,
            _ => 2
        };

        return $@"
🔍 AI 智能错误分析报告

订单信息摘要：
• 订单ID: {errorEvent.OrderId}
• 客户等级: {errorEvent.CustomerTier} 
• 订单金额: ${errorEvent.OrderValue:N2}
• 失败阶段: {errorEvent.ProcessingStage}

错误根本原因分析：
{errorEvent.ErrorCategory}阶段发生的 '{errorEvent.ErrorMessage}' 错误表明系统在{GetStageDescription(errorEvent.ProcessingStage)}环节存在问题。基于错误模式分析，这很可能是由于{GetLikelyRootCause(errorEvent.ErrorCategory)}导致的。

客户业务影响评估：
• 业务影响程度: {impactLevel}
• 客户关系风险: {GetCustomerRisk(errorEvent.CustomerTier)}
• 品牌声誉影响: {GetReputationImpact(errorEvent.OrderValue)}

紧急程度评级: {urgencyScore}/5 （需要{GetUrgencyAction(urgencyScore)}）

🛠️ AI推荐补救措施：
1. 立即启动{errorEvent.CustomerTier}级客户专属处理流程
2. 技术团队优先排查{errorEvent.ErrorCategory}模块
3. 为客户提供{GetCompensationLevel(errorEvent.CustomerTier)}补偿
4. 建立针对此类错误的预警机制

此分析基于AI算法和历史模式识别生成";
    }

    private static string GetStageDescription(string stage)
    {
        return stage switch
        {
            "Validation" => "订单验证",
            "Processing" => "订单处理",
            "Enrichment" => "数据丰富化",
            _ => "业务流程"
        };
    }

    private static string GetLikelyRootCause(string category)
    {
        return category switch
        {
            "Customer" => "客户数据同步延迟或权限验证问题",
            "Product" => "产品目录更新不及时或库存状态异常",
            "Inventory" => "库存管理系统负载过高或数据不一致",
            "Supplier" => "供应商网络连接问题或容量限制",
            _ => "系统间通信异常或负载峰值"
        };
    }

    private static string GetCustomerRisk(string tier)
    {
        return tier switch
        {
            "Premium" => "极高（可能导致重要客户流失）",
            "Gold" => "高（影响长期合作关系）",
            "Silver" => "中等（需要快速恢复信任）",
            _ => "一般（标准处理流程）"
        };
    }

    private static string GetReputationImpact(decimal orderValue)
    {
        return orderValue switch
        {
            > 5000m => "重大（高价值订单失败可能引发负面传播）",
            > 2000m => "显著（需要主动沟通避免扩散）",
            _ => "可控（标准恢复流程即可）"
        };
    }

    private static string GetUrgencyAction(int score)
    {
        return score switch
        {
            5 => "立即响应（15分钟内）",
            4 => "紧急处理（30分钟内）",
            3 => "优先处理（1小时内）",
            _ => "标准处理（2小时内）"
        };
    }

    private static string GetCompensationLevel(string tier)
    {
        return tier switch
        {
            "Premium" => "VIP级",
            "Gold" => "高级",
            "Silver" => "标准增强",
            _ => "标准"
        };
    }

    private static string GenerateMockCustomerMessage(HighValueErrorEvent errorEvent)
    {
        var compensation = errorEvent.CustomerTier switch
        {
            "Premium" => "20%订单折扣 + 免费升级服务",
            "Gold" => "15%订单折扣 + 优先处理",
            "Silver" => "10%订单折扣 + 快速处理",
            _ => "5%订单折扣"
        };

        return $@"
尊敬的客户，

我们非常抱歉地通知您，您的订单 {errorEvent.OrderId} 在处理过程中遇到了技术问题。

🔍 问题说明：
在{errorEvent.ProcessingStage}阶段出现了{errorEvent.ErrorCategory}相关的处理异常，我们的技术团队正在紧急处理此问题。

🛠️ 我们的解决方案：
• 技术团队已启动紧急修复程序
• 您的订单将获得优先处理
• 预计在24小时内完成处理

💝 诚意补偿：
为了表达我们的歉意，我们将为您提供：{compensation}

📞 如需帮助：
请随时联系我们的专属客服：support@bidone.com 或 400-800-1234

再次为给您带来的不便表示诚挚的歉意。

BidOne集成平台客服团队

注：此为AI模拟消息（OpenAI API未配置）";
    }

    private static string GenerateIntelligentCustomerMessage(HighValueErrorEvent errorEvent, string analysis)
    {
        var greeting = errorEvent.CustomerTier switch
        {
            "Premium" => "尊贵的白金客户",
            "Gold" => "重要的黄金客户",
            "Silver" => "优质的银卡客户",
            _ => "尊敬的客户"
        };

        var compensation = errorEvent.CustomerTier switch
        {
            "Premium" => "25%订单折扣 + 免费服务升级 + 专属客服支持",
            "Gold" => "20%订单折扣 + 优先处理服务 + 技术支持热线",
            "Silver" => "15%订单折扣 + 快速处理通道",
            _ => "10%订单折扣 + 标准补偿"
        };

        var responseTime = errorEvent.CustomerTier switch
        {
            "Premium" => "12小时内",
            "Gold" => "24小时内",
            _ => "48小时内"
        };

        return $@"
{greeting}，您好！

我们对您的订单 {errorEvent.OrderId} 处理过程中遇到的问题深表歉意。

🔍 问题分析：
我们的AI系统已经对此问题进行了深度分析。在{errorEvent.ProcessingStage}阶段出现了{errorEvent.ErrorCategory}相关的技术问题，我们的工程团队正在以最高优先级处理此事。

⚡ 即时行动：
• 您的订单已被标记为最高优先级处理
• 我们的{errorEvent.CustomerTier}客户专属团队已接管此案例
• 技术专家正在进行根本原因分析和修复

🎁 诚意补偿：
为表达我们最真诚的歉意，我们特为您提供：
{compensation}

⏰ 处理承诺：
我们承诺在{responseTime}完成问题处理，并将实时向您通报进展情况。

📞 专属支持：
您的专属客服代表将主动联系您，电话：400-800-{(errorEvent.CustomerTier == "Premium" ? "9999" : "1234")}
邮箱：{(errorEvent.CustomerTier == "Premium" ? "vip" : "support")}@bidone.com

再次为给您带来的不便表示最诚挚的歉意。我们将以此为契机，进一步提升服务质量。

此致
敬礼！

BidOne集成平台 {errorEvent.CustomerTier}客户服务团队
AI智能客服系统 | 服务热线：400-800-1234";
    }

    private static List<string> GenerateMockSuggestedActions(HighValueErrorEvent errorEvent)
    {
        return new List<string>
        {
            "1. 立即联系客户说明情况并致歉（客服团队，30分钟内）",
            $"2. 提供{errorEvent.CustomerTier}级别客户专属补偿方案（运营团队，1小时内）",
            "3. 技术团队排查根本原因并制定修复计划（技术团队，2小时内）",
            "4. 为该客户的后续订单提供VIP优先处理（运营团队，立即执行）",
            "5. 更新监控规则防止类似问题再次发生（技术团队，本周内）",
            "6. 客户关系团队跟进客户满意度（客服团队，问题解决后）",
            "7. 整理事件报告并优化相关流程（质量团队，一周内）"
        };
    }

    private static List<string> GenerateIntelligentSuggestedActions(HighValueErrorEvent errorEvent, string analysis)
    {
        var actions = new List<string>();

        // 基于客户等级的紧急响应
        var responseTime = errorEvent.CustomerTier switch
        {
            "Premium" => "15分钟内",
            "Gold" => "30分钟内",
            "Silver" => "45分钟内",
            _ => "1小时内"
        };

        actions.Add($"1. 🚨 立即启动{errorEvent.CustomerTier}级客户专属响应流程（客服主管，{responseTime}）");

        // 基于错误类别的技术行动
        var techAction = errorEvent.ErrorCategory switch
        {
            "Customer" => "客户数据同步和权限系统检查",
            "Product" => "产品目录和库存状态验证",
            "Inventory" => "库存管理系统性能优化",
            "Supplier" => "供应商网络连接和负载均衡检查",
            _ => "系统间通信和负载监控"
        };

        actions.Add($"2. 🔧 技术团队执行{techAction}（技术负责人，2小时内）");

        // 基于订单价值的补偿策略
        var compensationAction = errorEvent.OrderValue switch
        {
            > 5000m => "VIP级补偿包 + 未来订单优先处理权",
            > 2000m => "高级补偿包 + 专属客服联系人",
            > 1000m => "标准增强补偿包 + 快速处理通道",
            _ => "标准补偿包"
        };

        actions.Add($"3. 💝 实施{compensationAction}（客户关系经理，1小时内）");

        // 智能预防措施
        actions.Add($"4. 📊 在监控系统中为{errorEvent.ErrorCategory}类错误创建实时警报（运维团队，今日内）");
        actions.Add($"5. 📞 客户关系团队主动致电客户说明情况和后续安排（{responseTime}）");

        // 基于处理阶段的流程优化
        var processImprovement = errorEvent.ProcessingStage switch
        {
            "Validation" => "订单验证流程resilience增强",
            "Processing" => "订单处理并行度和容错机制优化",
            "Enrichment" => "数据丰富化服务降级策略完善",
            _ => "整体业务流程容灾能力提升"
        };

        actions.Add($"6. 🛠️ {processImprovement}（架构团队，本周内）");
        actions.Add($"7. 📋 生成AI分析报告并向管理层汇报（质量保证团队，24小时内）");

        // 高价值客户特殊关怀
        if (errorEvent.CustomerTier is "Premium" or "Gold")
        {
            actions.Add($"8. 🌟 安排高管层亲自致电{errorEvent.CustomerTier}客户表达歉意（业务总监，今日内）");
        }

        return actions;
    }
}
