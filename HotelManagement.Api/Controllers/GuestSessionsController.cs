
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using HotelManagement.Api.Data;
using HotelManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Api.Controllers;

[ApiController]
[Route("api/guest-sessions")]
public class GuestSessionsController : ControllerBase
{
    private readonly HotelDbContext _context;

    public GuestSessionsController(HotelDbContext context)
    {
        _context = context;
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(
        [FromBody] GuestSessionVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var roomNumber = request.RoomNumber.Trim();
        var phone = request.Phone.Trim();

        if (request.ReservationId <= 0 ||
            string.IsNullOrWhiteSpace(roomNumber) ||
            string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new
            {
                message = "Reservation ID, room number, and phone are required."
            });
        }

        // Preliminary validation only; not sufficient for production identity verification.
        var reservation = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r =>
                r.Id == request.ReservationId &&
                r.Room.RoomNumber == roomNumber &&
                r.Guest != null &&
                r.Guest.Phone == phone &&
                r.Status == "Confirmed",
                cancellationToken);

        if (reservation?.Guest == null)
        {
            return Unauthorized(new
            {
                message = "We couldn't verify those reservation details."
            });
        }

        // Invalidate previous active sessions for this reservation.
        var now = DateTime.UtcNow;

        var oldSessions = await _context.GuestSessions
            .Where(s =>
                s.ReservationId == reservation.Id &&
                s.EndedAtUtc == null &&
                s.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        foreach (var oldSession in oldSessions)
        {
            oldSession.EndedAtUtc = now;
        }

        var rawToken = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var session = new GuestSession
        {
            GuestId = reservation.GuestId!.Value,
            ReservationId = reservation.Id,
            TokenHash = tokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(8)
        };

        _context.GuestSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Reservation details checked. A temporary session was created.",
            token = rawToken,
            expiresAtUtc = session.ExpiresAtUtc,
            guestName = reservation.Guest.FullName,
            reservationId = reservation.Id,
            roomNumber = reservation.Room.RoomNumber
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Guest-Session", out var token) ||
            string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new
            {
                message = "A guest session token is required."
            });
        }

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token.ToString())));

        var session = await _context.GuestSessions
            .FirstOrDefaultAsync(s =>
                s.TokenHash == tokenHash &&
                s.EndedAtUtc == null &&
                s.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);

        if (session == null)
        {
            return Unauthorized(new
            {
                message = "The session is invalid or has expired."
            });
        }

        session.EndedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Guest session ended successfully." });
    }
}

public class GuestSessionVerifyRequest
{
    [Range(1, int.MaxValue)]
    public int ReservationId { get; set; }

    [Required, MaxLength(20)]
    public string RoomNumber { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;
}
