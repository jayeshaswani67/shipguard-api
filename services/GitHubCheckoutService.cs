using System.Diagnostics;

namespace ShipGuard.Api.Services;

public sealed class GitHubCheckoutService
{
    public async Task<string> ClonePublicRepositoryAsync(
        string repositoryUrl,
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        if (!GitHubRepositoryValidator.TryNormalize(
                repositoryUrl, out var normalizedUrl))
        {
            throw new ArgumentException(
                "Provide a valid public GitHub repository URL.");
        }

        var targetDirectory = Path.Combine(
            Path.GetTempPath(),
            $"shipguard-repo-{Guid.NewGuid():N}");

        Directory.CreateDirectory(targetDirectory);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("clone");
            startInfo.ArgumentList.Add("--depth");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("--single-branch");

            if (!string.IsNullOrWhiteSpace(branch))
            {
                if (branch.Length > 200 ||
                    branch.Any(char.IsControl))
                {
                    throw new ArgumentException("Invalid branch name.");
                }

                startInfo.ArgumentList.Add("--branch");
                startInfo.ArgumentList.Add(branch);
            }

            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add(normalizedUrl);
            startInfo.ArgumentList.Add(targetDirectory);

            using var timeout = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);

            timeout.CancelAfter(TimeSpan.FromMinutes(2));

            using var process = new Process { StartInfo = startInfo };

            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "Git could not be started.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort process cleanup.
                }

                throw;
            }

            var stderr = await stderrTask;
            _ = await stdoutTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Git clone failed: {stderr}");
            }

            return targetDirectory;
        }
        catch
        {
            try
            {
                if (Directory.Exists(targetDirectory))
                {
                    Directory.Delete(targetDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }

            throw;
        }
    }

    public static void DeleteCheckout(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}