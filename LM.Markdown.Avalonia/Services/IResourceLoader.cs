using Avalonia.Media;

namespace LM.Markdown.Avalonia.Services;

public interface IResourceLoader
{
    Task<IImage?> LoadImageAsync(string source, CancellationToken cancellationToken = default);
    void ClearCache();
}
