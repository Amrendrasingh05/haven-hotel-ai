
using System.Security.Cryptography;
using System.Text;
using HotelManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Api.Controllers;

[ApiController]
[Route("api/admin-auth")]
public sealed class AdminAuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AdminSessionService _adminSessions;

    public AdminAuthController(
        IConfiguration configuration,
        AdminSessionService adminSessions)
    {
        _configuration = configuration;
        _adminSessions = adminSessions;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] AdminLoginRequest request)
    {
        var configuredUsername =
            _configuration["HavenAdmin:Username"];

        var configuredPassword =
            _configuration["HavenAdmin:Password"];

        if (string.IsNullOrWhiteSpace(configuredUsername) ||
            string.IsNullOrWhiteSpace(configuredPassword))
        {
            return StatusCode(503, new
            {
                message = "Admin credentials are not configured on the server."
            });
        }

        var usernameMatches = SecureEquals(
            request.Username ?? "",
            configuredUsername);

        var passwordMatches = SecureEquals(
            request.Password ?? "",
            configuredPassword);

        if (!usernameMatches || !passwordMatches)
        {
            return Unauthorized(new
            {
                message = "Invalid username or password."
            });
        }

        var token = _adminSessions.CreateSession();

        return Ok(new
        {
            token,
            username = configuredUsername,
            expiresAtUtc = DateTime.UtcNow.AddHours(8)
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var token = Request.Headers["X-Admin-Session"].FirstOrDefault();

        _adminSessions.EndSession(token);

        return Ok(new { message = "Logged out successfully." });
    }

    private static bool SecureEquals(string supplied, string expected)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(suppliedBytes),
            SHA256.HashData(expectedBytes));
    }
}

public sealed class AdminLoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}
