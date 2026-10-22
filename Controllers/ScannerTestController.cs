using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ShipGuard.Api.Services;

namespace ShipGuard.Api.Controllers;

[ApiController]
[Route("api/scanner-test")]
public sealed class ScannerTestController : ControllerBase
{
    private readonly GitHubCheckoutService _git;
    private readonly TrivyScannerService _trivy;

    public ScannerTestController(
        GitHubCheckoutService git,
        TrivyScannerService trivy)
    {
        _git = git;
        _trivy = trivy;
    }

    public sealed record ScanRequest(
        string RepositoryUrl,
        string? Branch = null);

    [HttpPost]
    public async Task<IActionResult> Scan(
        [FromBody] ScanRequest request,
        CancellationToken cancellationToken)
    {
        string? checkout = null;

        try
        {
            if (!GitHubRepositoryValidator.TryNormalize(
                    request.RepositoryUrl, out _))
            {
                return BadRequest(new
                {
                    message = "Enter a valid public GitHub repository URL."
                });
            }

            checkout = await _git.ClonePublicRepositoryAsync(
                request.RepositoryUrl,
                request.Branch,
                cancellationToken);

            var report = await _trivy.ScanDirectoryAsync(
                checkout,
                cancellationToken);

            using var json = JsonDocument.Parse(report);

            return Ok(new
            {
                status = "completed",
                repositoryUrl = request.RepositoryUrl,
                report = json.RootElement.Clone()
            });
        }
        catch (TimeoutException ex)
        {
            return StatusCode(504, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499, new { message = "Scan cancelled." });
        }
        catch (Exception)
        {
            // Keep internal paths and process details out of the response.
            return StatusCode(500, new
            {
                message = "The scan failed. Check the backend logs."
            });
        }
        finally
        {
            if (checkout is not null)
            {
                try
                {
                    GitHubCheckoutService.DeleteCheckout(checkout);
                }
                catch (IOException)
                {
                    // Log cleanup failures in production.
                }
                catch (UnauthorizedAccessException)
                {
                    // Log cleanup failures in production.
                }
            }
        }
    }
}