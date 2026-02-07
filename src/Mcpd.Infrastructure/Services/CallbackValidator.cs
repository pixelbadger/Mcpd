using System.Text.RegularExpressions;
using Mcpd.Application.Interfaces;
using Mcpd.Domain.Interfaces;

namespace Mcpd.Infrastructure.Services;

public sealed class CallbackValidator(ICallbackWhitelistRepository whitelistRepo) : ICallbackValidator
{
    public async Task<CallbackValidationResult> ValidateAsync(Guid serverId, string[] redirectUris, CancellationToken ct)
    {
        var entries = await whitelistRepo.GetForServerAsync(serverId, ct);
        var errors = new List<string>();

        foreach (var uri in redirectUris)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            {
                errors.Add($"'{uri}' is not a valid absolute URI.");
                continue;
            }

            // Reject fragments
            if (!string.IsNullOrEmpty(parsed.Fragment) && parsed.Fragment != "#")
            {
                errors.Add($"'{uri}' must not contain a fragment component.");
                continue;
            }

            // Reject userinfo
            if (!string.IsNullOrEmpty(parsed.UserInfo))
            {
                errors.Add($"'{uri}' must not contain user information.");
                continue;
            }

            var matched = false;
            foreach (var entry in entries)
            {
                if (MatchesPattern(parsed, entry.Pattern))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                errors.Add($"'{uri}' does not match any whitelisted pattern for this server.");
            }
        }

        return new CallbackValidationResult(errors.Count == 0, errors.ToArray());
    }

    public static bool MatchesPattern(Uri uri, string pattern)
    {
        // Localhost pattern: http://localhost:*/path
        if (pattern.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) && pattern.Contains('*'))
        {
            if (uri.Scheme != "http" || uri.Host != "localhost")
                return false;

            var patternPath = GetPathFromLocalhostPattern(pattern);
            return string.Equals(uri.AbsolutePath, patternPath, StringComparison.OrdinalIgnoreCase);
        }

        // Wildcard subdomain: https://*.domain.com/path
        if (pattern.Contains("*.", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(pattern.Replace("*.", "wildcard-placeholder.", StringComparison.OrdinalIgnoreCase), UriKind.Absolute, out var patternUri))
                return false;

            if (!string.Equals(uri.Scheme, patternUri.Scheme, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals(uri.AbsolutePath, patternUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                return false;

            // Extract the domain part after the wildcard
            var wildcardIdx = pattern.IndexOf("*.", StringComparison.Ordinal);
            var baseDomain = pattern[(wildcardIdx + 2)..];
            // baseDomain now has the rest: domain.com/path, extract just host
            if (Uri.TryCreate(pattern.Replace("*.", "x.", StringComparison.OrdinalIgnoreCase), UriKind.Absolute, out var parsed))
            {
                var expectedDomain = parsed.Host[2..]; // Remove "x."
                var hostRegex = new Regex($"^[a-z0-9-]+\\.{Regex.Escape(expectedDomain)}$", RegexOptions.IgnoreCase);
                return hostRegex.IsMatch(uri.Host);
            }

            return false;
        }

        // Exact match
        return string.Equals(uri.ToString().TrimEnd('/'), pattern.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPathFromLocalhostPattern(string pattern)
    {
        // Pattern: http://localhost:*/path
        var afterPort = pattern.IndexOf('*');
        if (afterPort < 0) return "/";
        var pathStart = pattern.IndexOf('/', afterPort);
        return pathStart >= 0 ? pattern[pathStart..] : "/";
    }
}
