namespace LM.Markdown.Avalonia.Events;

public class LinkClickedEventArgs : EventArgs
{
    public string Url { get; }
    public string? Title { get; }

    public LinkClickedEventArgs(string url, string? title = null)
    {
        Url = url;
        Title = title;
    }
}

public class ImageClickedEventArgs : EventArgs
{
    public string Source { get; }
    public string? AltText { get; }

    public ImageClickedEventArgs(string source, string? altText = null)
    {
        Source = source;
        AltText = altText;
    }
}

public class CodeBlockCopyEventArgs : EventArgs
{
    public string Code { get; }
    public string? Language { get; }

    public CodeBlockCopyEventArgs(string code, string? language = null)
    {
        Code = code;
        Language = language;
    }
}
