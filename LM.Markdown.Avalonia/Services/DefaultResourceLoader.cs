using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;

namespace LM.Markdown.Avalonia.Services;

public class DefaultResourceLoader : IResourceLoader
{
    private const int MaxCacheEntries = 64;
    private readonly ConcurrentDictionary<string, IImage?> _cache = new();
    private readonly ConcurrentQueue<string> _cacheOrder = new();
    private readonly HttpClient _httpClient;
    private readonly string? _basePath;

    public DefaultResourceLoader(string? basePath = null)
    {
        _basePath = basePath;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "LM.Markdown.Avalonia/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<IImage?> LoadImageAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        if (_cache.TryGetValue(source, out var cached))
            return cached;

        try
        {
            var image = await LoadImageCoreAsync(source, cancellationToken);
            AddToCache(source, image);
            return image;
        }
        catch
        {
            AddToCache(source, null);
            return null;
        }
    }

    public void ClearCache()
    {
        while (_cacheOrder.TryDequeue(out _))
        {
        }

        foreach (var entry in _cache)
        {
            DisposeImage(entry.Value);
        }

        _cache.Clear();
    }

    private void AddToCache(string source, IImage? image)
    {
        if (_cache.TryAdd(source, image))
        {
            _cacheOrder.Enqueue(source);
            TrimCache();
            return;
        }

        _cache[source] = image;
    }

    private void TrimCache()
    {
        while (_cache.Count > MaxCacheEntries && _cacheOrder.TryDequeue(out var key))
        {
            if (_cache.TryRemove(key, out var removed))
            {
                DisposeImage(removed);
            }
        }
    }

    private async Task<IImage?> LoadImageCoreAsync(string source, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme == "http" || absoluteUri.Scheme == "https"))
        {
            return await LoadRemoteImageAsync(absoluteUri, cancellationToken);
        }

        return LoadLocalImage(source);
    }

    private async Task<IImage?> LoadRemoteImageAsync(Uri uri, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        if (IsSvg(uri.AbsolutePath, contentType))
        {
            return LoadSvgFromStream(stream);
        }

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;
        return new Bitmap(ms);
    }

    private IImage? LoadLocalImage(string path)
    {
        if (!Path.IsPathRooted(path) && _basePath != null)
            path = Path.Combine(_basePath, path);

        if (!File.Exists(path))
            return null;

        if (IsSvg(path, null))
        {
            using var stream = File.OpenRead(path);
            return LoadSvgFromStream(stream);
        }

        return new Bitmap(path);
    }

    private static IImage? LoadSvgFromStream(Stream stream)
    {
        var svg = new SvgImage();
        using var reader = new StreamReader(stream);
        var svgContent = reader.ReadToEnd();
        var svgSource = SvgSource.Load(svgContent);
        svg.Source = svgSource;
        return svg;
    }

    private static bool IsSvg(string path, string? contentType)
    {
        if (contentType?.Contains("svg", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        return path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static void DisposeImage(IImage? image)
    {
        if (image is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
