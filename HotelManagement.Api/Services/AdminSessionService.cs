
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace HotelManagement.Api.Services;

public sealed class AdminSessionService
{
    private readonly ConcurrentDictionary<string, DateTime> _sessions = new();

    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    public string CreateSession()
    {
        var token = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));

        var tokenHash = HashToken(token);
        _sessions[tokenHash] = DateTime.UtcNow.Add(SessionLifetime);

        return token;
    }

    public bool Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var tokenHash = HashToken(token);

        if (!_sessions.TryGetValue(tokenHash, out var expiresAt))
            return false;

        if (DateTime.UtcNow < expiresAt)
            return true;

        _sessions.TryRemove(tokenHash, out _);
        return false;
    }

    public void EndSession(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        _sessions.TryRemove(HashToken(token), out _);
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
