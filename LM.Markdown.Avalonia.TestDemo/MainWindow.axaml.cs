using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using LM.Markdown.Avalonia.Controls;

namespace LM.Markdown.Avalonia.TestDemo;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _streamCts;
    private ThemeVariant _currentThemeVariant = ThemeVariant.Light;

    private const string SampleMarkdown = """
# LM.Markdown.Avalonia Demo

This is a **comprehensive demo** of the *Markdown rendering control*.

## Features

### Text Formatting

You can use **bold**, *italic*, ***bold italic***, and ~~strikethrough~~ text.

Inline `code` looks like this, and here is a [link](https://github.com/LilaMorgen/LM.Markdown.Avalonia).

### Code Block

```csharp
public class HelloWorld
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello, Markdown!");
        var numbers = Enumerable.Range(1, 10)
            .Where(n => n % 2 == 0)
            .Select(n => n * n);
    }
}
```

```python
def fibonacci(n: int) -> list[int]:
    # Generate Fibonacci sequence
    fib = [0, 1]
    for i in range(2, n):
        fib.append(fib[-1] + fib[-2])
    return fib

print(fibonacci(10))
```

```csharp
using Avalonia.WebView.Desktop;

public static AppBuilder BuildAvaloniaApp()
	=> AppBuilder.Configure<App>()
		.UsePlatformDetect()
		.UseDesktopWebView();
```

### Blockquote

> "The best way to predict the future is to invent it."
> [link](https://github.com/LilaMorgen/LM.Markdown.Avalonia)
> — Alan Kay

### Lists

**Ordered list:**

1. First item
   1. First child item
2. Second item
3. Third item
4. [link](https://github.com/LilaMorgen/LM.Markdown.Avalonia)

**Unordered list:**

- Avalonia UI
  - Avalonia UI child item
- Markdig parser
- TextMateSharp highlighting
- CSharpMath formulas
- Mermaider diagrams
- [link](https://github.com/LilaMorgen/LM.Markdown.Avalonia)

### Task List

- [x] Markdown parsing
- [x] Syntax highlighting
- [x] Theme support
- [ ] PDF export
- [ ] Plugin system
- [ ] [link](https://github.com/LilaMorgen/LM.Markdown.Avalonia)

### Table

| Feature | Status | Priority |
|---------|--------|----------|
| Parsing | Done | High |
| Themes | Done | High |
| Streaming | Done | Medium |
| Math | Done | Medium |
| Mermaid | Done | Low |
| Link | Done | [link](https://github.com/LilaMorgen/LM.Markdown.Avalonia) |

### Image

![Sample Image1](images/wt.jpg)

### Math Formula

Inline math: $E = mc^2$

**矩阵运算：**
$$
\begin{bmatrix}
1 & 2 & 3 \\
4 & 5 & 6 \\
7 & 8 & 9
\end{bmatrix}
\times
\begin{bmatrix}
a \\ b \\ c
\end{bmatrix}
=
\begin{bmatrix}
1a + 2b + 3c \\
4a + 5b + 6c \\
7a + 8b + 9c
\end{bmatrix}
$$

二次方程求根公式：$x = \frac{-b \pm \sqrt{b^2 - 4ac}}{2a}$

表格中的公式：

| 公式名称 | LaTeX表达式           | 结果               |
| -------- | --------------------- | ------------------ |
| 求和     | $\sum_{i=1}^{n} i$    | $\frac{n(n+1)}{2}$ |
| 积分     | $\int_{0}^{1} x^2 dx$ | $\frac{1}{3}$      |

Block math:

$$
\int_{-\infty}^{\infty} e^{-x^2} dx = \sqrt{\pi}
$$

### Mermaid Diagram

```mermaid
graph TD
    A[Start] --> B{Is it working?}
    B -->|Yes| C[Great!]
    B -->|No| D[Debug]
    D --> B
```

---

### Horizontal Rule

That was a horizontal rule above.

*End of demo*
""";

    public MainWindow()
    {
        InitializeComponent();

        var btnStatic = this.FindControl<Button>("BtnStatic")!;
        var btnStream = this.FindControl<Button>("BtnStream")!;
        var btnClear = this.FindControl<Button>("BtnClear")!;
        var btnToggleTheme = this.FindControl<Button>("BtnToggleTheme")!;
        var themeStatusText = this.FindControl<TextBlock>("ThemeStatusText")!;
        var mdViewer = this.FindControl<MarkdownViewer>("MdViewer")!;

        ApplyTheme(ResolveInitialThemeVariant(), themeStatusText);

        btnStatic.Click += (_, _) =>
        {
            CancelStream();
            mdViewer.Markdown = SampleMarkdown;
        };

        btnStream.Click += (_, _) =>
        {
            CancelStream();
            _ = StreamMarkdownAsync(mdViewer);
        };

        btnClear.Click += (_, _) =>
        {
            CancelStream();
            mdViewer.ClearMarkdown();
        };

        btnToggleTheme.Click += (_, _) =>
        {
            var nextThemeVariant = _currentThemeVariant == ThemeVariant.Dark
                ? ThemeVariant.Light
                : ThemeVariant.Dark;

            ApplyTheme(nextThemeVariant, themeStatusText);
        };

        mdViewer.LinkClicked += (_, e) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Url,
                UseShellExecute = true,
            });
        };

        // Load static demo on startup
        mdViewer.Markdown = SampleMarkdown;
    }

    private async Task StreamMarkdownAsync(MarkdownViewer viewer)
    {
        _streamCts = new CancellationTokenSource();
        var ct = _streamCts.Token;

        viewer.ClearMarkdown();

        var chunks = SplitIntoChunks(SampleMarkdown, 12);

        foreach (var chunk in chunks)
        {
            if (ct.IsCancellationRequested)
                break;

            await Dispatcher.UIThread.InvokeAsync(() => viewer.AppendMarkdown(chunk));
            await Task.Delay(50, ct).ConfigureAwait(false);
        }
    }

    private void CancelStream()
    {
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = null;
    }

    private ThemeVariant ResolveInitialThemeVariant()
    {
        if (RequestedThemeVariant == ThemeVariant.Dark)
        {
            return ThemeVariant.Dark;
        }

        if (RequestedThemeVariant == ThemeVariant.Light)
        {
            return ThemeVariant.Light;
        }

        return ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    private void ApplyTheme(ThemeVariant themeVariant, TextBlock themeStatusText)
    {
        _currentThemeVariant = themeVariant;
        RequestedThemeVariant = themeVariant;
        themeStatusText.Text = themeVariant == ThemeVariant.Dark ? "Theme: Dark" : "Theme: Light";
    }

    private static List<string> SplitIntoChunks(string text, int chunkSize)
    {
        var chunks = new List<string>();
        for (int i = 0; i < text.Length; i += chunkSize)
        {
            var len = Math.Min(chunkSize, text.Length - i);
            chunks.Add(text.Substring(i, len));
        }
        return chunks;
    }
}