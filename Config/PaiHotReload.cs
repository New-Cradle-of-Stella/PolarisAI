using Polaris.Content;

namespace Polaris.AI;

/// <summary>薄封装：把 .pai 目录的轮询热重载接到共享的 <see cref="ContentHotReloadWatcher"/>。</summary>
internal sealed class PaiHotReload
{
    readonly ContentHotReloadWatcher watcher;

    internal PaiHotReload(string directory)
    {
        // 重载失败保留上一份编译结果（BehaviorRepository.LoadFile 本身就是这个语义），这里不用管成功与否。
        watcher = new ContentHotReloadWatcher(directory, "*.pai", path => BehaviorRepository.LoadFile(path));
    }

    internal void Initialize() => watcher.Initialize();

    internal void Tick(float deltaTime) => watcher.Tick(deltaTime);
}
