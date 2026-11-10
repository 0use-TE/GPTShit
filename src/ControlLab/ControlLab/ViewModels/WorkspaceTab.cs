using System;
using System.IO;
using System.Linq;
using GOZA.Dock;

namespace ControlLab.ViewModels;

public abstract record WorkspaceTab(string Id, string Header, MainViewModel Workspace) : IDockTabItem
{
    public bool ReuseSurface => true;
    public bool IsClosable => false;
    public sealed override string ToString() => Header;
}

public sealed record DeviceTab(MainViewModel Owner) : WorkspaceTab("device", "控制", Owner);
public sealed record CircuitTab(MainViewModel Owner) : WorkspaceTab("circuit", "仿真", Owner);
public sealed record LogTab(MainViewModel Owner) : WorkspaceTab("logs", "日志", Owner);

public sealed record SourceTab(MainViewModel Owner) : WorkspaceTab("firmware", "代码", Owner)
{
    public string Code { get; } = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Web", "main.py"));
    public string LineNumbers => string.Join('\n', Enumerable.Range(1, Code.Split('\n').Length));
}
