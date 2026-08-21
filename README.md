# PolarisAI

Polaris 的自定义 AI 行为能力组件。依赖同级 `PolarisCore`，并由 [Polaris](https://github.com/New-Cradle-of-Stella/Polaris) 聚合仓库作为 Git submodule 引用。

## 最小用法

```csharp
AINpc helper = AIAPI.Npcs.Spawn(
    NpcSpawnRequest.At("custom.basic", map, position)
        .WithBehavior("polaris.helper_combat")
        .WithBehaviorAttribute("speed", 0.16f)
        .WithBehaviorAttribute("attackRange", 2.5f)
        .WithBehaviorAttribute("damage", 12));

helper.SetTarget(enemy);
```

内置 NPC 定义为 `custom.basic`、`citycaster.default`、`citycaster.td` 和 `shadow.noel`。行为树以严格 JSON 的单文件 `.pai` 保存；内置示例是 `Defaults/helper_combat.pai`，外部文件放在配置目录的 `AI` 子目录中，可在运行时热重载。

自定义外观 NPC 使用 `.pnpc` 声明。`CharacterResource` 指向项目中带 `[PolarisResource]` 的公开静态 `PxlsCharacterHandle` 字段；PolarisTools 保存文件时校验并生成延迟注册器，PolarisAI 在运行时从 PolarisRes 取得已经解析完成的 `PxlCharacter`。生成时仍使用 `.pnpc` 的 `Id`：

```csharp
[PolarisResourceFolder("NPC")]
public static class NpcResources
{
    [PolarisResource("helper")]
    public static PxlsCharacterHandle Helper = null!; // NPC/helper.pxls
}
```

```xml
<PNpc Version="1" Id="example.helper" CharacterResource="Example.NpcResources.Helper"
      InitialPose="stand" Width="0.5" Height="1" MaxHp="100" MaxMp="100"
      HitType="Player" Faction="player" DefaultBehavior="polaris.helper_combat" />
```

资源 Ready 后即可沿用统一入口：`AIAPI.Npcs.Spawn("example.helper", map, position)`。调用请求中的 `Faction`、`BehaviorId` 和行为属性会覆盖 `.pnpc` 默认值。

启用 Polaris 行为后，目标 NPC 的原生 AI 会被暂停；禁用或移除行为后恢复原生逻辑。`shadow.noel` 仅支持已校验的 v0.29 游戏程序集，版本不匹配会直接拒绝创建，避免修改未知版本的玩家控制路径。

全局紧急回退开关位于 `BepInEx/config/Polaris/AI/PolarisAI.cfg` 的 `Runtime.Enabled`；关闭时会中止当前行为并立即恢复原生 AI 决策。
