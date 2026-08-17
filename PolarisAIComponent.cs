using Polaris.Components;

namespace Polaris.AI
{
    /// <summary>自定义 AI 行为能力的组件入口。</summary>
    public sealed class PolarisAIComponent : PolarisComponent
    {
        public override string Id => "PolarisAI";
        public override int Order => 1000;
    }
}
