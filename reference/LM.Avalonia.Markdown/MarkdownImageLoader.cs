using System.Net.Http;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace LM.Avalonia.Markdown;

public class MarkdownImageLoader
{
    private static readonly HttpClient HttpClient = new();

    public virtual async Task<IImage?> LoadImageAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
        {
            return null;
        }

        if (!uri.IsAbsoluteUri)
        {
            var fullPath = Path.GetFullPath(url);
            if (File.Exists(fullPath))
            {
                return new Bitmap(fullPath);
            }

            return null;
        }

        if (uri.Scheme is "http" or "https")
        {
            await using var stream = await LoadRemoteStreamAsync(uri).ConfigureAwait(false);
            if (stream is null)
            {
                return null;
            }

            try
            {
                return new Bitmap(stream);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        if (uri.Scheme == Uri.UriSchemeFile && File.Exists(uri.LocalPath))
        {
            return new Bitmap(uri.LocalPath);
        }

        return null;
    }

    internal static async Task<Stream?> LoadRemoteStreamAsync(Uri uri)
    {
        try
        {
            using var response = await HttpClient.GetAsync(uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var buffer = new MemoryStream();
            await source.CopyToAsync(buffer).ConfigureAwait(false);
            buffer.Position = 0;
            return buffer;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}