namespace ShipGuard.Api.Services;

public static class GitHubRepositoryValidator
{
    public static bool TryNormalize(
        string repositoryUrl,
        out string normalizedUrl)
    {
        normalizedUrl = "";

        if (!Uri.TryCreate(
                repositoryUrl,
                UriKind.Absolute,
                out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(
                uri.Host,
                "github.com",
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var parts = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
        {
            return false;
        }

        var owner = parts[0];
        var repository = parts[1];

        if (repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repository = repository[..^4];
        }

        if (!IsValidSegment(owner) || !IsValidSegment(repository))
        {
            return false;
        }

        normalizedUrl = $"https://github.com/{owner}/{repository}.git";
        return true;
    }

    private static bool IsValidSegment(string value)
    {
        return value.Length is > 0 and <= 100 &&
               value.All(c =>
                   char.IsAsciiLetterOrDigit(c) ||
                   c == '-' ||
                   c == '_' ||
                   c == '.');
    }
}