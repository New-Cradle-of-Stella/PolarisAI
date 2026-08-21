using System.IO;
using System.Reflection;
using BepInEx;
using Polaris.Components;
using UnityEngine;

namespace Polaris.AI;

/// <summary>自定义 AI 行为能力的组件入口。</summary>
public sealed class PolarisAIComponent : PolarisComponent
{
    PaiHotReload? hotReload;

    public override string Id => "PolarisAI";
    public override int Order => 1000;

    public override void Awake()
    {
        AISettings.Resolve();
        BehaviorRepository.LoadEmbedded(Assembly.GetExecutingAssembly());
        string directory = Path.Combine(Paths.ConfigPath, "Polaris", "AI", "trees");
        hotReload = new PaiHotReload(directory);
        hotReload.Initialize();
    }

    public override void Start() => PnpcRegistry.ScanModules();

    public override void Update()
    {
        float deltaTime = Time.deltaTime;
        hotReload?.Tick(deltaTime);
        AIActorRegistry.Tick(deltaTime);
    }

    public override void Shutdown()
    {
        AIActorRegistry.Shutdown();
        PnpcRegistry.Clear();
        hotReload = null;
    }
}
