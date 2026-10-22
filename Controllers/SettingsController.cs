
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using ShipGuard.Api.Services;

namespace ShipGuard.Api.Controllers;

public sealed class UpdateProfileRequest
{
    public string Name { get; set; } = "";
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public sealed class UserSettingsResponse
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

[ApiController]
[Authorize]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly IMongoCollection<AuthUser> _users;

    public SettingsController(IMongoDatabase database)
    {
        _users = database.GetCollection<AuthUser>("Users");
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(userId) ||
            !ObjectId.TryParse(userId, out var objectId))
        {
            return Unauthorized(new { message = "Invalid user identity in token." });
        }

        var user = await _users.Find(x => x.Id == objectId.ToString())
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return NotFound(new { message = "User not found." });

        return Ok(new UserSettingsResponse
        {
            Name = user.Name,
            Email = user.Email
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(userId) ||
            !ObjectId.TryParse(userId, out var objectId))
        {
            return Unauthorized(new { message = "Invalid user identity in token." });
        }

        var name = request.Name.Trim();

        if (name.Length < 2 || name.Length > 80)
            return BadRequest(new { message = "Name must contain 2–80 characters." });

        var update = Builders<AuthUser>.Update.Set(x => x.Name, name);

        var result = await _users.UpdateOneAsync(
            x => x.Id == objectId.ToString(),
            update,
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
            return NotFound(new { message = "User not found." });

        return Ok(new { message = "Profile updated successfully.", name });
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(userId) ||
            !ObjectId.TryParse(userId, out var objectId))
        {
            return Unauthorized(new { message = "Invalid user identity in token." });
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "Both password fields are required." });
        }

        if (request.NewPassword.Length < 12)
            return BadRequest(new { message = "New password must be at least 12 characters." });

        if (request.NewPassword.Length > 128)
            return BadRequest(new { message = "New password cannot exceed 128 characters." });

        var user = await _users.Find(x => x.Id == objectId.ToString())
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return NotFound(new { message = "User not found." });

        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });

        var newHash = HashPassword(request.NewPassword);

        await _users.UpdateOneAsync(
            x => x.Id == objectId.ToString(),
            Builders<AuthUser>.Update.Set(x => x.PasswordHash, newHash),
            cancellationToken: cancellationToken);

        return Ok(new
        {
            message = "Password changed successfully. Please sign in again if required."
        });
    }

    private static string HashPassword(string password)
    {
        const int iterations = 210_000;
        var salt = RandomNumberGenerator.GetBytes(16);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return $"PBKDF2${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('$');

        if (parts.Length != 4 ||
            parts[0] != "PBKDF2" ||
            !int.TryParse(parts[1], out var iterations) ||
            iterations < 100_000 ||
            iterations > 1_000_000)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
