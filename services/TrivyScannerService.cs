using System.Diagnostics;
using Microsoft.Extensions.Options;
using ShipGuard.Api.Configuration;

namespace ShipGuard.Api.Services;

public sealed class TrivyScannerService
{
    private readonly TrivyOptions _options;

    public TrivyScannerService(IOptions<TrivyOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> ScanDirectoryAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                "The project directory does not exist.");
        }

        if (string.IsNullOrWhiteSpace(_options.ExecutablePath) ||
            !File.Exists(_options.ExecutablePath))
        {
            throw new FileNotFoundException(
                "Trivy was not found. Check Trivy:ExecutablePath.");
        }

        var reportPath = Path.Combine(
            Path.GetTempPath(),
            $"shipguard-{Guid.NewGuid():N}.json");

        try
        {
            using var timeout = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);

            timeout.CancelAfter(
                TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var startInfo = new ProcessStartInfo
            {
                FileName = _options.ExecutablePath,
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("fs");
            startInfo.ArgumentList.Add("--scanners");
            startInfo.ArgumentList.Add("vuln,secret,misconfig");
            startInfo.ArgumentList.Add("--format");
            startInfo.ArgumentList.Add("json");
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(reportPath);
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add(directory);

            using var process = new Process
            {
                StartInfo = startInfo
            };

            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "Trivy could not be started.");
            }

            var stderrTask = process.StandardError.ReadToEndAsync();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The process may already have exited.
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                throw new TimeoutException(
                    "The security scan exceeded its time limit.");
            }

            var stderr = await stderrTask;
            _ = await stdoutTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Trivy failed with exit code {process.ExitCode}: " +
                    stderr);
            }

            if (!File.Exists(reportPath))
            {
                throw new InvalidOperationException(
                    "Trivy completed without creating a report.");
            }

            return await File.ReadAllTextAsync(
                reportPath, cancellationToken);
        }
        finally
        {
            try
            {
                if (File.Exists(reportPath))
                {
                    File.Delete(reportPath);
                }
            }
            catch (IOException)
            {
                // Temporary-file cleanup can be retried separately.
            }
        }
    }
}