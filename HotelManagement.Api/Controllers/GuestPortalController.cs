
using HotelManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Api.Controllers;

[ApiController]
[Route("api/guest-portal")]
public class GuestPortalController : ControllerBase
{
    private readonly GuestSessionService _sessionService;

    public GuestPortalController(GuestSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentGuest(
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Guest-Session", out var token))
        {
            return Unauthorized(new
            {
                message = "A guest session token is required."
            });
        }

        var session = await _sessionService.ValidateAsync(
            token.ToString(), cancellationToken);

        if (session == null)
        {
            return Unauthorized(new
            {
                message = "The guest session is invalid or has expired."
            });
        }

        return Ok(new
        {
            guestName = session.Guest.FullName,
            reservationId = session.ReservationId,
            roomNumber = session.Reservation.Room.RoomNumber,
            expiresAtUtc = session.ExpiresAtUtc
        });
    }
}
