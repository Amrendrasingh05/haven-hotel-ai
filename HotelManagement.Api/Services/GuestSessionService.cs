
using System.Security.Cryptography;
using System.Text;
using HotelManagement.Api.Data;
using HotelManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Api.Services;

public class GuestSessionService
{
    private readonly HotelDbContext _context;

    public GuestSessionService(HotelDbContext context)
    {
        _context = context;
    }

    public async Task<GuestSession?> ValidateAsync(
        string? rawToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 200)
            return null;

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var now = DateTime.UtcNow;

        return await _context.GuestSessions
            .Include(s => s.Guest)
            .Include(s => s.Reservation)
                .ThenInclude(r => r.Room)
            .FirstOrDefaultAsync(
                s => s.TokenHash == tokenHash
                     && s.EndedAtUtc == null
                     && s.ExpiresAtUtc > now,
                cancellationToken);
    }
}
