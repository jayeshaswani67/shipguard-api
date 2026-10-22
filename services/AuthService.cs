
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace ShipGuard.Api.Services;

// -------------------- REQUESTS --------------------

public sealed class RegisterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// -------------------- RESPONSES --------------------

public sealed class AuthResponse
{
    public string? AccessToken { get; set; }
    public UserResponse User { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public sealed class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// -------------------- MONGODB DOCUMENT --------------------

public sealed class AuthUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("role")]
    public string Role { get; set; } = "Developer";
}

// -------------------- JWT SETTINGS --------------------

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "ShipGuard.Api";
    public string Audience { get; set; } = "ShipGuard.Client";
    public string Secret { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}

// -------------------- SERVICE CONTRACT --------------------

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}

// -------------------- SERVICE EXCEPTIONS --------------------

public sealed class AuthConflictException : Exception
{
    public AuthConflictException(string message) : base(message) { }
}

public sealed class AuthValidationException : Exception
{
    public AuthValidationException(string message) : base(message) { }
}

// -------------------- AUTH SERVICE --------------------

public sealed class AuthService : IAuthService
{
    private readonly IMongoCollection<AuthUser> _users;
    private readonly JwtSettings _jwtSettings;

    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 210_000;

    public AuthService(
        IMongoDatabase database,
        IOptions<JwtSettings> jwtOptions)
    {
        _users = database.GetCollection<AuthUser>("Users");
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var email = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            throw new AuthValidationException(
                "Name is required and must be at most 100 characters.");
        }

        if (!IsValidEmail(email))
        {
            throw new AuthValidationException(
                "Please provide a valid email address.");
        }

        if (string.IsNullOrEmpty(request.Password) ||
            request.Password.Length < 8 ||
            Encoding.UTF8.GetByteCount(request.Password) > 1024)
        {
            throw new AuthValidationException(
                "Password must be at least 8 characters and no more than 1024 UTF-8 bytes.");
        }

        var existing = await _users
            .Find(user => user.Email == email)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            throw new AuthConflictException(
                "An account with this email already exists.");
        }

        var user = new AuthUser
        {
            Name = name,
            Email = email,
            PasswordHash = HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            Role = "Developer"
        };

        try
        {
            await _users.InsertOneAsync(
                user,
                cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex)
            when (ex.WriteError?.Category ==
                  ServerErrorCategory.DuplicateKey)
        {
            throw new AuthConflictException(
                "An account with this email already exists.");
        }

        return new AuthResponse
        {
            AccessToken = null,
            User = ToUserResponse(user),
            Message = "Account created successfully. Please log in."
        };
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        if (!IsValidEmail(email) ||
            string.IsNullOrEmpty(request.Password))
        {
            throw new AuthValidationException(
                "Invalid email or password.");
        }

        var user = await _users
            .Find(item => item.Email == email)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null ||
            !VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new AuthValidationException(
                "Invalid email or password.");
        }

        return new AuthResponse
        {
            AccessToken = GenerateAccessToken(user),
            User = ToUserResponse(user),
            Message = "Login successful."
        };
    }

    private string GenerateAccessToken(AuthUser user)
    {
        if (string.IsNullOrWhiteSpace(_jwtSettings.Secret) ||
            Encoding.UTF8.GetByteCount(_jwtSettings.Secret) < 32)
        {
            throw new InvalidOperationException(
                "JWT secret must contain at least 32 bytes. Configure Jwt:Secret securely.");
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.Secret));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var expiryMinutes = Math.Clamp(
            _jwtSettings.ExpiryMinutes, 5, 1440);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        // Versioned format: algorithm.iterations.salt.hash
        return $"PBKDF2-SHA256.{Iterations}." +
               $"{Convert.ToBase64String(salt)}." +
               $"{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(
        string password,
        string storedHash)
    {
        try
        {
            var parts = storedHash.Split('.');

            if (parts.Length != 4 ||
                parts[0] != "PBKDF2-SHA256" ||
                !int.TryParse(parts[1], out var iterations) ||
                iterations < 100_000 ||
                iterations > 1_000_000)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);

            if (salt.Length != SaltSize ||
                expectedHash.Length != KeySize)
            {
                return false;
            }

            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(
                actualHash,
                expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static UserResponse ToUserResponse(AuthUser user)
    {
        return new UserResponse
        {
            Id = user.Id ?? string.Empty,
            Name = user.Name,
            Email = user.Email
        };
    }

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            email.Length > 254)
        {
            return false;
        }

        var at = email.IndexOf('@');

        return at > 0 &&
               at == email.LastIndexOf('@') &&
               at < email.Length - 1 &&
               !email.Contains(' ') &&
               email[(at + 1)..].Contains('.');
    }
}
