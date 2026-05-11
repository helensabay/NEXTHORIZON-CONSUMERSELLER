using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace MyAspNetApp.Services
{
    public class MediaPathService
    {
        private readonly IWebHostEnvironment _environment;

        public MediaPathService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public string NormalizePublicPath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            var path = rawPath.Trim();
            if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri) &&
                (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
            {
                return absoluteUri.ToString();
            }

            if (path.StartsWith("/Media/File", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return $"/Media/File?path={Uri.EscapeDataString(path)}";
        }

        public string? ResolveLocalPath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return null;
            }

            var normalized = rawPath.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(normalized) && File.Exists(normalized))
            {
                return normalized;
            }

            var localCandidate = Path.Combine(_environment.WebRootPath ?? string.Empty, normalized.TrimStart(Path.DirectorySeparatorChar));
            if (File.Exists(localCandidate))
            {
                return localCandidate;
            }

            var fileName = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            foreach (var searchRoot in GetSearchRoots())
            {
                if (string.IsNullOrWhiteSpace(searchRoot) || !Directory.Exists(searchRoot))
                {
                    continue;
                }

                try
                {
                    foreach (var file in Directory.EnumerateFiles(searchRoot, fileName, SearchOption.AllDirectories))
                    {
                        var candidate = file.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                        if (candidate.Contains($"{Path.DirectorySeparatorChar}wwwroot{Path.DirectorySeparatorChar}uploads{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                            || candidate.Contains($"{Path.DirectorySeparatorChar}wwwroot{Path.DirectorySeparatorChar}images{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                        {
                            return candidate;
                        }
                    }
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
            }

            return null;
        }

        private IEnumerable<string> GetSearchRoots()
        {
            var currentRoot = _environment.ContentRootPath;
            if (!string.IsNullOrWhiteSpace(currentRoot))
            {
                yield return currentRoot;
            }

            var parentRoot = Directory.GetParent(_environment.ContentRootPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(parentRoot))
            {
                yield return parentRoot;
            }

            var trackerRoot = Path.Combine(parentRoot ?? string.Empty, "Tracker", "NextHorizon-AndradeBranch", "NextHorizon");
            if (!string.IsNullOrWhiteSpace(parentRoot))
            {
                yield return trackerRoot;
            }
        }

        public string GetContentType(string path)
        {
            var extension = Path.GetExtension(path);
            return extension.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".avif" => "image/avif",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                ".mp4" => "video/mp4",
                _ => "application/octet-stream"
            };
        }
    }
}
