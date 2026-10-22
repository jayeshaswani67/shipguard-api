
using Microsoft.AspNetCore.Mvc;
using ShipGuard.Api.Services;

namespace ShipGuard.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.RegisterAsync(
                request,
                cancellationToken
            );

            return Created("/api/auth/login", result);
        }
        catch (AuthConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (AuthValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.LoginAsync(
                request,
                cancellationToken
            );

            return Ok(result);
        }
        catch (AuthValidationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}
