# PolarisAI 通用 Character 外置行为树与 Groot2 / PolarisTools 接入计划

## 1. 文档目的

本计划用于在 Alice In Cradle v029 上实现一个外置、可配置、可热重载的行为树系统，使不同类型的可战斗 Character 能挂载统一的行为树，同时继续复用原版角色动作、动画、物理、伤害和特殊机制。

首要应用场景是改进“保卫战”中 `M2CityCaster` 友军 NPC 的战斗与协同能力，随后覆盖 `NelEnemy` 敌人，并为 `PR` 玩家角色提供显式启用的辅助或自动控制能力。

本文分析基于：

- 游戏目录：`D:\AliceInCradle Win ver029\AliceInCradle_ver029`
- Unity：2022.3.62f2，Mono 脚本后端
- `Assembly-CSharp.dll` SHA256：`C15AE0207DE38ACC80F055C219411B855BF8AE76B395234AEA046AAADB0248D9`
- 当前 `aic_path.txt` 指向的 BepInEx 6 游戏副本与原版程序集 Hash 一致

## 2. 核心结论

原版不存在一套可覆盖所有角色的统一 AI：

| 角色类别 | 原版决策器 | 原版执行方式 | 推荐接入点 |
|---|---|---|---|
| `NelEnemy` 敌人 | `NAI` | `NaTicket` 队列和 `readTicket()` 动作状态机 | 包装 `NAI` 的决策委托，保留 Ticket 执行 |
| `M2CityCaster` 友军 NPC | `CityCasterAI` / `CityCasterAITD` | 每帧直接控制移动、索敌、吟唱和状态 | Prefix 接管 `CityCasterAITD.consider()`，保留实例和特殊机制 |
| `PR` 玩家角色 | 输入系统 / `M2PrSkill` | 输入状态驱动角色行为 | 可选虚拟输入适配器，默认不接管真人输入 |

因此行为树运行时不能依赖 `NAI` 或 `NaTicket`。正确架构是：

1. 与游戏类型解耦的行为树核心。
2. 通用 Character 宿主和黑板。
3. 每类角色独立的原版控制器适配器。
4. 保卫战额外增加队伍级 Team Director 和共享黑板。
5. 所有替换都必须保留原版回退路径。

## 3. 原版 AI 结构摘要

### 3.1 敌人

敌人主调用链：

```text
NelEnemy.runPre()
  -> NAI.consider()
  -> fnSleepLogic / fnAwakeLogic / fnOverDriveLogic
  -> 每种敌人的 considerNormal() 等硬编码决策
  -> NAI.AddTicket(type, priority)
  -> NelEnemy.readTicket(ticket)
  -> 动画、移动、攻击和魔法状态机
```

`NAI` 同时承担感知、目标搜索、距离计算、随机数、Flag、冷却、危险区域和 Ticket 调度等职责。每个敌人的 `considerNormal()` 本质上是硬编码的反应式优先选择器。

`NAI.TYPE.PUNCH`、`MAG` 等只代表敌人自己的动作槽位，不同敌人对同一枚举的解释不同。因此外置配置不应直接以这些枚举作为面向用户的通用动作名。

### 3.2 保卫战 NPC

保卫战 NPC 为 `M2CityCaster`，其 `switchAI("td")` 创建 `CityCasterAITD`。`M2CityCaster.runPre()` 在普通状态下每帧调用 `CityCasterAITD.consider()`。

`CityCasterAITD` 使用内部 `MSTATE` 管理：

- 普通战斗
- 开始吟唱
- 持续吟唱
- 前往魔力塔
- 治疗员待机
- 治疗员逃跑
- 搜索和搬运伤员

它不使用 Ticket，而是直接设置行走速度、目标、朝向、跳跃、魔法种类和角色状态。

`TDCasterAssignManager` 负责：

- 将 NPC 分配到城墙、升降台和治疗帐篷。
- 管理城墙容量和 NPC 站位。
- 管理治疗员与担架搬运任务。
- 维护城墙、魔力塔、帐篷和敌人锁定信息。

### 3.3 保卫战 AI 的主要不足

1. NPC 分配主要依赖站位容量、距离和原位置，缺少基于实时敌群威胁的动态调度。
2. 单个 NPC 独立索敌，基础锁定半径较小，没有共享集火目标。
3. 普通战斗员大多是“走到固定点后周期施法”，主动拉扯和转线能力有限。
4. 魔法选择主要局限于 `WHITEARROW` 和 `FIREBALL` 的少量条件与随机选择。
5. MP 低于固定阈值后前往魔力塔，并恢复到固定比例才返回，不考虑当前波次压力和同时回蓝人数。
6. 治疗与搬运机制较完整，但普通战斗员缺少相同深度的战术决策。
7. `TDCasterAssignManager` 保存了敌人锁定和权重数据，但现有分配过程没有形成完整的队伍威胁模型。

## 4. 目标与非目标

### 4.1 第一阶段目标

- 使用 Groot2 编辑、以 BehaviorTree.CPP 4 风格 XML 保存的外置行为树。
- 配置热重载，失败时保留最后一份有效配置。
- `Success`、`Failure`、`Running`、`Suspended` 节点状态。
- 支持敌人、保卫战 NPC 和后续玩家角色的统一宿主。
- 保卫战战斗员可以读取队伍共享目标和防线任务。
- 所有角色类型均支持逐角色、逐配置关闭和原版回退。
- 可查看“角色为什么选择了这个动作”。

### 4.2 第一阶段非目标

- 不重写原版动画、物理、伤害、吸收、死亡和 OverDrive 系统。
- 不允许 XML 直接执行任意反射调用、C# 表达式或 BehaviorTree.CPP `Script`。
- 不强制替换所有敌人的原版 AI。
- 不默认接管当前真人玩家。
- 不在第一版重写保卫战治疗员的完整担架搬运状态机。

## 5. 总体架构

```text
Groot2-compatible XML Behavior Tree
  -> GrootXmlLoader / Validator / Compiler
  -> Immutable CompiledTree
  -> CharacterBehaviorHost
       -> CharacterBlackboard
       -> CharacterRuntimeState
       -> ICharacterAdapter
            -> EnemyAdapter
            -> CityCasterTdAdapter
            -> PlayerInputAdapter

TowerDefenceDirector
  -> TeamBlackboard
       -> 防线威胁
       -> 角色分工
       -> 集火目标
       -> 回蓝名额
       -> 救援任务
```

### 5.1 通用角色身份

不要将所有 `M2Attackable` 都当作 Character。城墙、机关、靶子和诱饵也可能继承该类型。

角色仅在存在已注册适配器时挂载：

```text
NelEnemy      -> EnemyAdapter
M2CityCaster  -> CityCasterTdAdapter
PR            -> PlayerInputAdapter
其他对象      -> 默认不挂载
```

建议身份模型：

```csharp
public enum CharacterKind
{
    Enemy,
    CityCaster,
    Player,
    EventActor
}

public sealed class CharacterIdentity
{
    public CharacterKind Kind { get; init; }
    public string RuntimeType { get; init; }
    public string CharacterKey { get; init; }
    public string Team { get; init; }
    public IReadOnlySet<string> Tags { get; init; }
}
```

`CharacterKey` 的来源：

- 敌人：`ENEMYID` 和具体运行时类型。
- 城市 NPC：`CityMobgEntry.key`。
- 玩家：玩家角色类型或稳定角色 ID。

### 5.2 适配器契约

```csharp
public interface ICharacterAdapter
{
    CharacterIdentity Identity { get; }
    CharacterCapabilities Capabilities { get; }
    object NativeCharacter { get; }

    bool CanThink();
    void CaptureBlackboard(CharacterBlackboard target);

    ActionStatus StartAction(ActionRequest request);
    ActionStatus TickAction(float deltaTime);
    void AbortAction(AbortReason reason);
}
```

行为树使用语义动作，不直接认识原版枚举或私有状态：

```text
move.to
move.keepDistance
target.acquire
target.focusTeamTarget
combat.primary
combat.cast
combat.guard
resource.restoreMana
td.defendLane
td.retreatBehindWall
td.rescueAlly
native.fallback
```

## 6. 行为树运行时

### 6.1 节点类型

第一版实现：

- `Selector`
- `Sequence`
- `ReactiveSelector`
- `Parallel`
- `Condition`
- `Action`
- `Inverter`
- `Succeeder`
- `Cooldown`
- `Timeout`
- `Repeat`
- `Chance`
- `WeightedSelector`
- `Subtree`
- `NativeFallback`

### 6.2 节点状态

```csharp
public enum NodeStatus
{
    Success,
    Failure,
    Running,
    Suspended
}
```

- `Running`：动作已开始，需要后续 Tick。
- `Suspended`：角色处于伤害、吸收、事件或其他暂时不可思考状态，保留运行上下文但不推进。
- 强制状态切换、角色销毁、配置替换时调用 `Abort()`。

### 6.3 Tick 策略

不同适配器可配置不同 Tick 策略：

- 敌人：以原版 `NAI` 思考时机为主，Running Action 监控 Ticket。
- CityCaster：在 `CityCasterAITD.consider()` 中逐帧 Tick。
- 玩家：在输入处理前生成本帧虚拟输入租约。
- Team Director：低频 Tick，例如每 10～30 个逻辑帧重算一次战术任务。

### 6.4 随机数

- 敌人优先复用 `NAI.RANtk/RANa`，维持原版随机序列特征。
- 其他 Character 使用按角色和会话初始化的独立确定性随机源。
- XML 中每个随机节点应具有稳定 `name` 或 `channel`，避免节点重排导致所有随机行为改变。

## 7. 黑板设计

建议按命名空间组织，避免形成无边界的大字典：

### `self.*`

- 位置、速度、朝向
- HP/MP 及比例
- 当前状态
- 是否落地、是否可移动、是否可施法
- 当前动作和动作运行时间
- 状态效果

### `target.*`

- 目标身份、阵营、位置
- 水平、垂直和直线距离
- HP/MP
- 是否存活、是否可达、是否在视线内
- 威胁值和目标类型标签

### `world.*`

- 游戏模式
- 地图与区域
- 可用路径、危险区域
- 当前波次或召唤器状态

### `team.*`

- 队伍集火目标
- 当前角色任务
- 每条防线威胁
- 各角色位置和状态
- 回蓝中的角色数量
- 救援队列
- 最后一道墙和关键设施状态

### `native.*`

只读暴露适配器特有值，例如：

- `native.enemy.ticketType`
- `native.enemy.overdrive`
- `native.td.assignedWall`
- `native.td.isTentHealer`
- `native.player.hasPhysicalInput`

## 8. 保卫战 Team Director

仅增强单体 NPC 不足以解决协同问题。Team Director 是保卫战纵切的必要组成。

### 8.1 输入

- 所有存活 `M2CityCaster`。
- 城墙、升降台、治疗帐篷和魔力塔。
- 当前存活敌人及其位置、类型、速度、目标和 `lockon_weight`。
- 当前玩家位置和角色状态。
- 各 NPC 的 HP、MP、当前动作和分配位置。

### 8.2 输出

- `AssignedRole`：fighter、healer、carrier、reserve。
- `AssignedLane` 或 `AssignedWall`。
- `AssignedPosition`。
- `FocusTarget`。
- `MayRestoreMana`。
- `RetreatRequired`。
- `RescueTarget`。

### 8.3 第一版威胁模型

```text
Threat = enemy.lockonWeight
       * enemy.hpRatioFactor
       * distanceToCriticalWallFactor
       * movementSpeedFactor
       * enemyTypeFactor
       * targetUrgencyFactor
```

第一版无需追求复杂预测，但必须做到：

- 越接近关键防线的敌人优先级越高。
- 高威胁或特殊敌人可配置额外权重。
- 同一目标的集火人数有上限。
- 不允许大量 NPC 同时离开防线回蓝。
- 城墙濒临失守时可提前撤退到下一条防线。

## 9. 原版接入方案

### 9.1 EnemyAdapter

建议方式：

1. Harmony 拦截 `NAI.consider()`。
2. 首次遇到匹配敌人时保存原版 `fnSleepLogic`、`fnAwakeLogic`、`fnOverDriveLogic`。
3. 安装代理委托。
4. 行为树成功处理时不执行原版决策。
5. 行为树返回 `Pass`、抛出异常或无有效配置时调用保存的原版委托。
6. 动作通过 `NAI.AddTicket()` 创建，并监控 Ticket 生命周期。

第一版不修改 `NelEnemy.readTicket()`。

### 9.2 CityCasterTdAdapter

不能替换 `CityCasterAITD` 实例，因为 `TDCasterAssignManager` 多处将 `GetAI()` 强制转换为 `CityCasterAITD`。替换实例会破坏防线分配、帐篷治疗和担架搬运。

建议方式：

1. Harmony Prefix 接管 `CityCasterAITD.consider()`。
2. 树返回 `Handled` 时设置 `__result` 并跳过本帧原版 `consider()`。
3. 树返回 `Pass` 时运行原版。
4. 保留原 `CityCasterAITD` 对象以及 `abort()`、索敌、吟唱和帐篷逻辑。
5. 第一版对治疗员的 `HEALER_*` 状态默认执行 `native.fallback`。
6. 普通战斗员优先使用外置树。

建议的原生动作适配：

- `td.moveToAssignedPosition`
- `td.acquireConfiguredTarget`
- `td.beginNativeChant`
- `td.waitForChantComplete`
- `td.goToManaTower`
- `td.retreat`
- `td.nativeHealerLogic`

### 9.3 PlayerInputAdapter

`M2MoverPr.simulateKeyDown()` 在正常游戏输入路径中并不总会生效，因此需要在输入查询层合并虚拟输入。

第一版支持：

- `Assist`：检测到真实移动、攻击或魔法输入时立即让权。
- `Autopilot`：行为树独占游戏角色输入，但必须显式启用。

需要管理的输入包括：

- 左、右、上、下
- 跳跃
- 普通攻击
- 魔法
- 瞄准
- 闪避

菜单、事件和文本输入期间必须强制释放虚拟输入。

## 10. Groot2 配置与 PolarisTools 接入

### 10.1 工具职责边界

只保留一套图形化行为树编辑器：Groot2。PolarisTools 不实现自研树画布，其职责限定为：

1. 为 Visual Studio 项目创建 PolarisAI 树和绑定文件。
2. 定位并启动外部 `Groot2` 进程打开树文件。
3. 从 PolarisAI 的共享节点目录生成 Groot2 `TreeNodesModel`。
4. 监听 Groot2 保存结果并进行静态校验，将错误送入 Visual Studio Error List。
5. 设置构建项属性、部署内置树，并通过命名管道向运行中的游戏推送调试快照。

Groot2 只负责编辑 XML。游戏内不加载 BehaviorTree.CPP 的 C++ 运行库，也不调用其节点实现；PolarisAI 使用自己的 C# 执行器解释同一棵树。第一版不实现 Groot2 的 BT.CPP 实时监控协议，避免依赖未公开稳定的协议和 Pro 功能。

调研依据：

- Groot2 官方当前提供完整树编辑、多文件项目和 XML 预览；实时监控面向 BehaviorTree.CPP executor，黑板、断点等完整能力属于 Pro：[Groot2](https://www.behaviortree.dev/groot/)。
- Groot2 需要节点类型以及输入/输出端口组成的 `TreeNodesModel`：[Connect to Groot2](https://www.behaviortree.dev/docs/tutorial-basics/tutorial_11_groot2/)。
- 标准 XML 用 `<root>`、`<BehaviorTree ID>`、节点属性端口和 `{blackboardKey}` 映射表达树，也支持 SubTree/include：[XML schema](https://www.behaviortree.dev/docs/3.8/learn-the-basics/xml_format/)。
- PolarisTools 已有可复用接入样板：`PolarisToolsPackage.cs` 的编辑器/模板注册、`PEffectDebugClient.cs` 的命名管道客户端，以及 PolarisParticles 的版本化调试协议。

### 10.2 文件职责

采用三种 XML 文档，树结构和 Polaris 特有的挂载信息严格分离：

```text
AI/
├─ Trees/
│  └─ TdCityCasterFighter.pbt.xml       # Groot2 直接编辑的纯行为树
├─ Bindings/
│  └─ TdCityCasterFighter.pbtbind.xml   # 角色匹配、控制模式和优先级
├─ Directors/
│  └─ TowerDefence.paidirector.xml      # Team Director 参数
└─ Authoring/
   └─ polaris-ai-nodes.xml              # 自动生成的 Groot2 节点模型
```

- `*.pbt.xml` 必须保持 BehaviorTree.CPP 4 风格，供 Groot2 无损打开和保存。
- `*.pbtbind.xml` 只由 PolarisAI/PolarisTools 读取，Groot2 不接触它。
- `*.paidirector.xml` 是普通配置文档，不再为它开发另一套节点编辑器。
- `polaris-ai-nodes.xml` 是生成物，不手工维护，也不打进最终模组。

使用独立绑定文件是刻意选择：不要把 `characterKind`、游戏模式和 fallback 等私有属性塞进 Groot2 树节点，避免 Groot2 保存时丢弃或改写未知元数据。

### 10.3 Groot2 兼容子集

PolarisAI 读取标准结构：

- `<root BTCPP_format="4" main_tree_to_execute="...">`
- 一个或多个 `<BehaviorTree ID="...">`
- `<SubTree ID="...">`、输入/输出端口和黑板 `{key}` 映射
- Groot2/BT.CPP 常见的 Sequence、Fallback、ReactiveSequence、ReactiveFallback 和受支持装饰器
- `<TreeNodesModel>` 中由 PolarisAI 声明的 Action、Condition、Decorator 及其端口

第一版明确拒绝：

- BehaviorTree.CPP `Script` 及前置/后置脚本属性。
- 未登记的节点 ID、任意反射方法名或 C# 代码。
- 绝对路径 include、逃逸树根目录的 `..` include 和循环 include。
- PolarisAI 没有声明语义的 BT.CPP 扩展节点。

即使 Groot2 能画出某个节点，也必须通过 PolarisAI 校验器才能部署。

### 10.4 树与绑定示例

`TdCityCasterFighter.pbt.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<root BTCPP_format="4" main_tree_to_execute="TdCityCasterFighter">
  <BehaviorTree ID="TdCityCasterFighter">
    <Fallback name="Root">
      <Sequence name="Retreat">
        <TeamRetreatRequired />
        <TdRetreatBehindWall />
      </Sequence>
      <Sequence name="Combat">
        <TeamHasFocusTarget />
        <SelfCanCast />
        <TargetFocusTeamTarget />
        <CombatCastBestMagic />
      </Sequence>
      <TdExecuteAssignedRole />
      <NativeFallback />
    </Fallback>
  </BehaviorTree>
</root>
```

`TdCityCasterFighter.pbtbind.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<PolarisAIBindings version="1">
  <Binding id="td.city-caster.fighter"
           treeFile="../Trees/TdCityCasterFighter.pbt.xml"
           treeId="TdCityCasterFighter"
           priority="100">
    <Match characterKind="CityCaster"
           runtimeType="nel.M2CityCaster"
           characterKey="*"
           team="player"
           mode="towerDefence"
           tags="fighter" />
    <Control mode="beforeOriginal" fallback="original" />
  </Binding>
</PolarisAIBindings>
```

匹配优先级：

```text
个体配置 > CharacterKey > 运行时类型 > CharacterKind > 全局默认
```

控制模式：

- `replace`：完全由行为树负责决策。
- `beforeOriginal`：行为树优先，无决策时执行原版。
- `afterOriginal`：原版无决策时执行行为树。
- `observe`：只运行和记录，不实际执行动作。
- `disabled`：不挂载。

默认使用 `beforeOriginal`。

### 10.5 共享 Authoring 核心

新增无 Unity/BepInEx/游戏程序集依赖的 `PolarisAI.Authoring`（`netstandard2.0`）：

- Groot XML、绑定 XML 和 Director XML 的 DTO 与读取器。
- 节点目录、端口类型、默认值和适配器能力声明。
- include 展开、结构校验、绑定校验和诊断编号。
- `TreeNodesModel` 生成器。

`PolarisAI` 运行时和 `PolarisTools` VSIX 必须引用同一 Authoring 项目，不能各写一套 XML 解析器。运行时动作注册表还需通过测试证明与 Authoring 节点目录一一对应。

### 10.6 PolarisTools VSIX 改动

PolarisTools 当前为 `net472` WPF VSIX，已经具备 Custom Editor、项目项模板、保存事件、共享源码/项目引用以及 PEffect 命名管道调试模式。新增功能沿用这些现有模式：

1. 增加 `PBehaviorTreeFile` 项目项模板，一次创建配对的 `*.pbt.xml` 和 `*.pbtbind.xml`。
2. 在 VSIX 清单登记模板；新增 `Polaris > Open AI Tree in Groot2`、`Validate AI Tree`、`Debug AI Tree in Game` 命令。
3. `Groot2Locator` 按“用户设置路径 → PATH → 已知安装位置”查找；找不到时给出设置入口和官方安装页，不把 Groot2 二进制打进 VSIX。
4. `Groot2Launcher` 使用参数数组/安全引号启动选中的树。实现前先做安装尖峰，验证 Groot2 1.9.x 是否支持直接以文件或项目路径启动；若无稳定 CLI 契约，命令只启动 Groot2 并复制文件路径，不依赖未文档化参数。
5. `AiTreeProjectWatcher` 监听解决方案内 `*.pbt.xml`、`*.pbtbind.xml`，200～500ms 防抖后调用共享校验器，并刷新 Error List。
6. 新建/添加/保存 AI 文件时确保其构建项为 `EmbeddedResource`；发布时作为内置默认树，运行时允许配置目录中的同 ID 外置树覆盖。
7. `AiDebugClient` 复用 PEffect 的命名管道思路，但使用独立的版本化协议 `Polaris.AI.Debug`，推送当前项目的树、绑定和 Director 文本；游戏端完成二次校验后才原子替换调试快照。

不把 Groot2 嵌进 Visual Studio ToolWindow。双击 XML 仍可使用 VS 文本编辑器，图形编辑通过显式命令交给外部 Groot2，以避免外部进程与 VS Running Document Table 争夺同一文档所有权。

### 10.7 运行时目录和覆盖规则

```text
BepInEx/plugins/Polaris/PolarisAI/defaults/
    框架内置示例和默认树

模组程序集 EmbeddedResource
    模组作者随包分发的树、绑定和 Director 配置

BepInEx/config/Polaris/AI/trees/
    用户外置 *.pbt.xml

BepInEx/config/Polaris/AI/bindings/
    用户外置 *.pbtbind.xml

BepInEx/config/Polaris/AI/directors/
    用户外置 *.paidirector.xml

BepInEx/Polaris/ai/
    编译缓存、运行状态和诊断输出
```

覆盖优先级：用户外置文件 > VSIX 调试快照 > 模组嵌入资源 > PolarisAI 内置默认。相同层级出现重复 Binding ID 或 Tree ID 时拒绝该冲突集合，而不是依赖文件枚举顺序。

### 10.8 必须先验证的 Groot2 尖峰

本机当前未安装 Groot2，官方网页也没有承诺稳定的命令行打开参数。因此实现 VSIX 前先做一个不超过半天的安装尖峰：

1. 安装当前 Windows x86_64 版 Groot2，记录可执行文件位置和版本发现方式。
2. 验证 `Groot2.exe <tree-or-project>` 是否受支持，以及路径含空格、中文时的行为。
3. 验证导入 `TreeNodesModel`、打开 `*.pbt.xml`、保存、子树和多文件工程的实际文件变化。
4. 验证 Groot2 是否保留 XML 声明、节点 `name`、端口、注释和节点模型；任何不能稳定保留的 Polaris 数据继续放在绑定侧车文件。
5. 固化为自动化 fixture：Groot2 保存后的样例必须仍能被共享 Authoring 核心读取并通过语义校验。

尖峰失败不阻塞运行时：VSIX 退化为“启动 Groot2 + 复制文件路径”，树文件仍可用任意 XML 编辑器修改。

## 11. 热重载和失败隔离

### 11.1 文件监听

- `FileSystemWatcher` 只负责产生变更通知。
- 使用 200～500ms 防抖合并连续写入事件。
- 定时扫描文件时间戳，弥补 watcher 丢事件。
- XML 解析、include 展开、结构校验和树编译不触碰 Unity 对象。
- 在主线程 `PolarisAIComponent.Update()` 中原子替换不可变树仓库快照。

### 11.2 Last Known Good

- 新配置校验失败时继续使用上一份有效树。
- 日志包含文件名、树 ID、节点路径和具体错误。
- 单棵树失败不能禁用其他树。
- 单个 Character 执行异常时仅将该 Character 切回原版。

### 11.3 当前动作处理

默认策略：

- 当前 Running Action 仍存在：继续到完成，再使用新树。
- 当前 Action 已删除或签名变化：调用 `Abort(ConfigReload)`。
- 强制状态变化：调用 `Abort(NativeStateChanged)`。
- 角色销毁：调用 `Abort(CharacterDestroyed)` 并移除运行时状态。

## 12. 可观察性与调试

第一版至少提供：

- 当前挂载树 ID 和来源文件。
- 当前运行节点路径。
- 当前动作、目标和持续时间。
- 上一次 Success/Failure 原因。
- 是否执行了原版 fallback。
- Team Director 当前任务分配和集火目标。
- 每棵树 Tick 次数、平均耗时、最大耗时和异常数。

决策日志默认关闭，开启后按角色采样，避免每帧刷屏。

建议提供只读调试覆盖层：

```text
[CityCaster:alice_01]
Tree: td.city-caster.fighter
Role: Fighter @ Wall-2
Target: NelNSlime#14
Node: Root/Combat/CastBestMagic
Action: Chant(FIREBALL) [Running]
Fallback: false
```

## 13. 性能预算

- 不允许每个节点通过反射读取游戏字段。
- 配置加载时编译条件和动作绑定。
- 黑板以强类型快照或预分配结构存储常用字段。
- Team Director 低频计算，结果供所有 NPC 共享。
- 未挂载行为树的角色只承受一次快速匹配检查。
- 单角色普通 Tick 目标预算：平均低于 0.05ms。
- 全部 PolarisAI 在典型保卫战中的总预算：平均低于 1ms/帧。

## 14. 建议项目结构

```text
PolarisAI/
├─ BehaviorTree/
│  ├─ NodeStatus.cs
│  ├─ BehaviorNode.cs
│  ├─ CompiledTree.cs
│  ├─ CompositeNodes.cs
│  ├─ DecoratorNodes.cs
│  └─ ActionNode.cs
├─ Characters/
│  ├─ CharacterIdentity.cs
│  ├─ CharacterCapabilities.cs
│  ├─ CharacterBlackboard.cs
│  ├─ CharacterBehaviorHost.cs
│  └─ ICharacterAdapter.cs
├─ Adapters/
│  ├─ EnemyAdapter.cs
│  ├─ CityCasterTdAdapter.cs
│  └─ PlayerInputAdapter.cs
├─ Actions/
│  ├─ ActionRegistry.cs
│  ├─ CommonActions.cs
│  ├─ EnemyActions.cs
│  ├─ CityCasterActions.cs
│  └─ PlayerActions.cs
├─ Conditions/
│  ├─ ConditionRegistry.cs
│  ├─ CommonConditions.cs
│  └─ TowerDefenceConditions.cs
├─ TowerDefence/
│  ├─ TowerDefenceDirector.cs
│  ├─ TeamBlackboard.cs
│  ├─ ThreatEvaluator.cs
│  └─ AssignmentPlanner.cs
├─ Config/
│  ├─ TreeCompiler.cs
│  ├─ TreeRepository.cs
│  ├─ EmbeddedTreeSource.cs
│  ├─ ExternalTreeSource.cs
│  └─ ConfigWatcher.cs
├─ Runtime/
│  ├─ CharacterRuntime.cs
│  ├─ BehaviorRuntime.cs
│  ├─ HotReloadCoordinator.cs
│  └─ Diagnostics.cs
├─ Patch/
│  ├─ Patch_NAI_consider.cs
│  ├─ Patch_CityCasterAITD_consider.cs
│  ├─ Patch_CityCasterAITD_abort.cs
│  └─ Patch_PlayerVirtualInput.cs
└─ PolarisAIComponent.cs

PolarisAI.Authoring/
├─ Documents/
│  ├─ GrootXmlDocument.cs
│  ├─ BindingDocument.cs
│  └─ DirectorDocument.cs
├─ Catalog/
│  ├─ NodeDescriptor.cs
│  └─ BuiltInNodeCatalog.cs
├─ Validation/
│  ├─ TreeValidator.cs
│  ├─ BindingValidator.cs
│  └─ AuthoringDiagnostic.cs
└─ Groot/
   └─ TreeNodesModelWriter.cs
```

## 15. 分阶段实施

### P0：兼容性基线

任务：

- 固定 v029 程序集 Hash。
- 建立运行时版本检查。
- 完成 Groot2 Windows 安装/启动参数尖峰，并固定首个受支持版本。
- 导出敌人、CityCaster 和玩家角色识别信息。
- 建立原版行为开关和全局 Kill Switch。

验收：

- 版本不匹配时不安装高风险控制补丁。
- 关闭 PolarisAI 后角色行为与原版一致。
- 形成可重复的 Groot2 保存样例和 VSIX 启动降级策略。

预计：1～2 人日。

### P1：纯行为树核心

任务：

- 节点状态、组合节点、装饰器、Running Action。
- 建立 `PolarisAI.Authoring`，读取和校验 Groot2/BT.CPP 4 风格 XML。
- 生成 `TreeNodesModel`，编译受支持节点、端口、SubTree 和黑板映射。
- 无 Unity 依赖的测试上下文。

验收：

- 所有节点具有确定的 Start/Tick/Abort 行为。
- 非法树在加载阶段被拒绝。
- 纯逻辑测试覆盖主要节点组合。

预计：4～6 人日。

### P2：通用 Character 宿主

任务：

- `CharacterBehaviorHost`、适配器注册表和身份匹配。
- 强类型黑板和动作注册表。
- 运行时状态生命周期。
- `observe`、`beforeOriginal`、`replace` 控制模式。

验收：

- 模拟适配器可挂树、执行 Running Action 和安全 Abort。
- 没有适配器的游戏对象不会被错误挂载。

预计：2～3 人日。

### P3：CityCaster 保卫战纵切

任务：

- Prefix 接入 `CityCasterAITD.consider()`。
- 实现移动、索敌、吟唱、施法、回蓝和原版 fallback 动作。
- 治疗员状态默认交还原版。
- 实现一棵普通战斗员示例树。

验收场景：

- NPC 能移动到配置位置。
- NPC 能选取配置目标并完成一次原版施法流程。
- 受伤、倒地、吸收、死亡时安全中断。
- 治疗员搬运机制不退化。
- 无配置或树异常时逐帧回退原版。

预计：5～8 人日。

### P4：Team Director

任务：

- 采集防线、设施、NPC 和敌人快照。
- 威胁计算和角色任务分配。
- 共享集火目标。
- 回蓝并发限制和撤退条件。

验收场景：

- 多名 NPC 不再随机分散攻击低价值目标。
- 高威胁敌人进入关键区域时能触发集火或转线。
- 不会出现全部战斗员同时回蓝。
- 城墙失守前后能重新分配防线。

预计：4～7 人日。

### P5：EnemyAdapter

任务：

- 包装 `NAI` 决策委托。
- 语义动作到 Ticket 的映射。
- 监控 Ticket 生命周期。
- 先以史莱姆完成纵切。

验收：

- 睡眠、唤醒、移动、攻击和 OverDrive 可配置。
- 原版 `readTicket()`、动画和攻击判定不变。
- 配置异常时恢复原版委托。

预计：2～4 人日。

### P6：Groot2 / PolarisTools、热重载和调试工具

任务：

- PolarisTools 项目模板、Groot2 定位/启动、节点模型生成和 Error List 诊断。
- 运行时 watcher、防抖、定时补扫和 Last Known Good。
- `Polaris.AI.Debug` 命名管道与 VSIX `Debug AI Tree in Game` 命令。
- 决策路径、角色状态和性能指标。
- 游戏内只读调试覆盖层。

验收：

- Groot2 保存 XML 后无需重启即可生效。
- 半写入、错误 XML、非法 include 或绑定不匹配不会清空当前有效树。
- PolarisTools 和游戏端对同一文件给出相同诊断编号。
- 可定位到失败节点和动作。

预计：5～8 人日。

### P7：Player Assist

任务：

- 虚拟输入状态和输入查询补丁。
- 真人输入让权。
- 菜单、事件、暂停和切图清理。

验收：

- `Assist` 模式下真实输入立即覆盖 AI。
- 关闭或异常后不残留按键。
- 默认设置不接管玩家。

预计：4～7 人日。

## 16. 测试矩阵

### 配置测试

- 空文件、非法 XML、未知节点、未知动作和不受支持的 BT.CPP 节点。
- `TreeNodesModel` 端口类型、缺少必填端口和未知端口。
- include 相对路径、越界路径、文件缺失和循环引用。
- 重复树 ID、循环 Subtree、错误参数类型。
- 树文件与 Binding 的 `treeId`、角色能力和控制模式不匹配。
- 多配置匹配优先级。
- 热重载期间文件被连续写入。

### 行为树测试

- Running 子节点的恢复。
- Selector/Sequence 的记忆与反应式语义。
- Timeout、Cooldown 和 Abort。
- 确定性随机节点。

### Groot2 / PolarisTools 工具链测试

- 节点目录生成的 `TreeNodesModel` 快照稳定，节点 ID 和端口与运行时注册表一致。
- Groot2 保存样例经过往返后仍能加载，SubTree 和黑板端口映射不变。
- Groot2 路径、树路径包含空格和中文时可以启动或正确降级。
- 外部保存触发 VSIX watcher 后，Error List 能清除旧诊断并定位新诊断。
- 调试管道版本不匹配、超时、超量文件和非法 XML 都被安全拒绝。
- VSIX 推送与游戏端加载返回相同的核心诊断编号。

### CityCaster 实机测试

- 普通站立、移动、跳跃和升降台。
- 有目标、无目标、目标死亡、目标隔墙。
- MP 不足、魔力塔被移除、多人同时回蓝。
- 受伤、击飞、倒地、眩晕、吸收和死亡。
- 治疗员、担架、帐篷被摧毁。
- 城墙被摧毁和防线重新分配。
- 切图、事件脚本和召唤器结束。

### Enemy 实机测试

- 睡眠与唤醒。
- Ticket 被高优先级动作打断。
- 伤害状态中断。
- OverDrive。
- 目标不可达、目标切换和敌人销毁。

### Player 实机测试

- Assist 让权。
- Autopilot 启停。
- 菜单、事件、游戏暂停和失焦。
- 角色死亡、切图和模组关闭后的输入清理。

## 17. 风险与缓解

| 风险 | 影响 | 缓解措施 |
|---|---|---|
| 游戏更新改变私有字段和方法 | 补丁或动作适配失效 | Hash 门控、逐补丁健康状态、版本适配层 |
| 替换 `CityCasterAITD` 实例 | 破坏强制类型转换和 TD 管理 | 不替换实例，只接管 `consider()` |
| 行为树打断原版强制状态 | 动画、物理或特殊机制异常 | `CanThink()` 门控，状态变化立即 Abort |
| 热重载处于 Running Action 中 | 动作残留或状态错乱 | 动作签名比较、Abort 协议、最后有效配置 |
| XML 暴露脚本或过强原生调用能力 | 崩溃、作弊或任意执行风险 | 白名单节点/端口，拒绝 Script、表达式和任意反射 |
| Groot2 可编辑的 BT.CPP 节点多于 PolarisAI 子集 | 作者误以为均可运行 | 生成专用 TreeNodesModel，VSIX 与游戏端双重校验 |
| Groot2 外部保存与 VS 文档状态冲突 | 丢失修改或错误诊断滞后 | 不抢占 XML Editor，项目级 watcher 防抖重载 |
| Groot2 版本或启动参数变化 | VSIX 无法一键打开文件 | 可配置路径、启动尖峰、失败时只启动程序并复制路径 |
| Team Director 每个 NPC 重复计算 | 保卫战卡顿 | 队伍级低频计算，共享不可变结果 |
| Player 虚拟输入残留 | 角色持续移动或攻击 | 帧租约、焦点/菜单/事件/关闭时强制清理 |
| 原版随机语义变化 | 行为表现难以复现 | 稳定随机 channel，敌人复用 NAI 随机源 |

## 18. 完成定义

保卫战纵切完成需同时满足：

1. 至少一棵外置 CityCaster 战斗员树可热重载。
2. 至少两名 NPC 能根据 Team Director 共享集火目标。
3. 能动态限制回蓝人数并在防线失守后重新分配站位。
4. 原版治疗、搬运、受伤、吸收和死亡流程无明显回归。
5. 任意单树错误都只影响对应角色，并自动回退原版。
6. 关闭 PolarisAI 后游戏行为恢复原版。
7. 调试信息能显示当前树、节点、动作、目标和 fallback 状态。
8. 典型保卫战场景下 PolarisAI 平均耗时不超过 1ms/帧。
9. PolarisTools 能生成 Groot2 节点模型、启动 Groot2、校验树并推送到游戏。

## 19. 推荐首个开发目标

首个可交付纵切：

> 一名普通 `M2CityCaster` 在保留原 `CityCasterAITD` 实例的前提下挂载外置树，接受 Team Director 的防线与集火指令，完成移动、索敌、吟唱和施法；配置缺失、失效或执行异常时安全回退原版。

完成该纵切后，再接入 `EnemyAdapter`。这样可以先解决保卫战的实际痛点，同时确保底层从第一天起就是通用 Character 架构，避免先实现 `NAI` 专用版本后再返工。
