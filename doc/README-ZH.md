# LM.Markdown.Avalonia

<div align="center">

![License](https://img.shields.io/badge/License-MIT-yellow.svg)
![Release Badge](https://img.shields.io/badge/release-v_1.0.1-blue)

</div>

LM.Markdown.Avalonia 是一个面向 Avalonia 桌面应用的 Markdown 渲染控件，适合需要富文本 Markdown 展示、增量流式输出、代码高亮、数学公式、表格、任务列表、图片加载和 Mermaid 图表渲染的场景。

如需英文版说明，请查看 [README.md](https://github.com/LilaMorgen/LM.Markdown.Avalonia/blob/master/README.md)。

## 演示效果

![stream-dark](../images/stream-dark.gif)

### 明亮主题

![all-light](../images/all-light.png)

### 暗色主题

![all-light](../images/all-dark.png)

## 包说明

LM.Markdown.Avalonia 以 NuGet 包形式分发，面向 Avalonia 桌面应用。

- 包 ID：`LM.Markdown.Avalonia`
- 目标框架：`.NET 10`
- Avalonia 兼容版本：`11.3.x`
- 源码仓库与示例程序：[LM.Markdown.Avalonia on GitHub](https://github.com/LilaMorgen/LM.Markdown.Avalonia)

## 核心能力

- 基于 Markdig 的块级和行内 Markdown 渲染。
- 通过 `AppendMarkdown` 实现流式追加渲染。
- 代码块语法高亮。
- 行内与块级数学公式渲染。
- Mermaid 图表渲染。
- 带缓存控制和取消能力的图片加载。
- 跨块文本统一选择与自动滚动。
- 明暗主题资源支持。

## 仓库架构

```mermaid
flowchart TD
    A[LM.Markdown.Avalonia.TestDemo] --> B[MarkdownViewer]
    B --> C[MarkdownPipelineFactory]
    B --> D[MarkdownDocumentRenderer]
    D --> E[Block Renderers]
    D --> F[Inline Renderers]
    D --> G[RenderContext]
    G --> H[TextMateSyntaxHighlighter]
    G --> I[MathRendererService]
    G --> J[MermaidRendererService]
    G --> K[DefaultResourceLoader]
    B --> L[CrossBlockSelectionHandler]
    B --> M[MarkdownTheme.axaml]
```

架构说明：

- `MarkdownViewer` 是控件入口，负责可视树创建、全量渲染、增量追加、自动滚动和选择生命周期。
- `MarkdownPipelineFactory` 负责创建 Markdig 解析管线。
- `MarkdownDocumentRenderer` 负责组织块级和行内渲染器，并将源码映射写入 `RenderContext`。
- 各类服务接口用于隔离代码高亮、数学公式、Mermaid 渲染和资源加载。
- `MarkdownTheme.axaml` 提供统一的排版、间距和明暗主题资源。

## 本次更新

- 新增围栏代码块 `mermaid` 图表渲染支持。
- 修复渲染缓存与旧控件引用在控件卸载和重新渲染路径上的内存滞留问题。
- 为资源密集型服务补充取消机制和有界缓存行为。

## 使用方法

### 1. 安装 NuGet 包

使用 .NET CLI：

```powershell
dotnet add package LM.Markdown.Avalonia --version 1.0.1
```

或者手动添加包引用：

```xml
<ItemGroup>
  <PackageReference Include="LM.Markdown.Avalonia" Version="1.0.1" />
</ItemGroup>
```

### 2. 合并主题资源

在 `App.axaml` 中合并 Markdown 主题资源：

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceInclude Source="avares://LM.Markdown.Avalonia/Themes/MarkdownTheme.axaml" />
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

### 3. 在 XAML 中放置控件

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:md="clr-namespace:LM.Markdown.Avalonia.Controls;assembly=LM.Markdown.Avalonia"
        x:Class="Demo.MainWindow">

  <md:MarkdownViewer x:Name="MarkdownViewer"
                     Margin="16"
                     AutoScroll="True"
                     EnableUnifiedSelection="True" />
</Window>
```

### 4. 在代码中设置 Markdown 内容

```csharp
using Avalonia.Controls;
using LM.Markdown.Avalonia.Controls;

namespace Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var viewer = this.FindControl<MarkdownViewer>("MarkdownViewer")!;
        viewer.Markdown = """
# Hello LM.Markdown.Avalonia

This control supports **markdown**, tables, math, code blocks, and Mermaid.

```mermaid
flowchart LR
  User --> Viewer
  Viewer --> Renderer
```
""";
    }
}
```

### 5. 流式追加 Markdown

```csharp
viewer.ClearMarkdown();
viewer.AppendMarkdown("# Streaming");
viewer.AppendMarkdown("\n\nFirst chunk.");
viewer.AppendMarkdown("\n\nSecond chunk.");
```

## 简单使用

完整可运行示例请查看仓库中的示例程序：[LM.Markdown.Avalonia.TestDemo](https://github.com/LilaMorgen/LM.Markdown.Avalonia/tree/main/LM.Markdown.Avalonia.TestDemo)。

最小 XAML 示例：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:md="clr-namespace:LM.Markdown.Avalonia.Controls;assembly=LM.Markdown.Avalonia"
        x:Class="Demo.MainWindow">

  <md:MarkdownViewer x:Name="MarkdownViewer" Margin="16" />
</Window>
```

最小代码示例：

```csharp
using Avalonia.Controls;
using LM.Markdown.Avalonia.Controls;

namespace Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var viewer = this.FindControl<MarkdownViewer>("MarkdownViewer")!;
        viewer.Markdown = "# Hello\n\nThis is **LM.Markdown.Avalonia**.";
    }
}
```

## 示例程序

如果你想从源码运行示例程序：

```powershell
dotnet run --project .\LM.Markdown.Avalonia.TestDemo\LM.Markdown.Avalonia.TestDemo.csproj
```

## 开发说明

- 当前库面向 `.NET 10` 与 Avalonia `11.3.x`。
- 默认构造函数会自动接入 `TextMateSyntaxHighlighter`、`DefaultResourceLoader`、`MathRendererService` 和 `MermaidRendererService`。
- 在长生命周期或流式输出场景下，开始新一轮输出前建议先调用 `ClearMarkdown`。
