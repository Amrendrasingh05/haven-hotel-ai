
using System.Data;
using HotelManagement.Api.Data;
using HotelManagement.Api.Models;
using HotelManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HotelManagement.Api.Controllers;

public record CreateReservationRequest(
    string GuestName,
    string GuestPhone,
    int RoomId,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int GuestCount);

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly HotelDbContext _context;
    private readonly AdminSessionService _adminSessionService;

    public ReservationsController(
        HotelDbContext context,
        AdminSessionService adminSessionService)
    {
        _context = context;
        _adminSessionService = adminSessionService;
    }

    private bool IsAdminAuthenticated()
    {
        var token = Request.Headers["X-Admin-Session"].FirstOrDefault();
        return _adminSessionService.Validate(token);
    }

    // GET: api/reservations
    // Admin only: reservation records contain guest information.
    [HttpGet]
    public async Task<IActionResult> GetReservations(
        CancellationToken cancellationToken)
    {
        if (!IsAdminAuthenticated())
        {
            return Unauthorized(new
            {
                message = "Admin login required."
            });
        }

        var reservations = await _context.Reservations
            .AsNoTracking()
            .Include(r => r.Room)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new
            {
                r.Id,
                r.GuestName,
                r.GuestPhone,
                r.GuestId,
                r.RoomId,
                RoomNumber = r.Room.RoomNumber,
                r.CheckInDate,
                r.CheckOutDate,
                r.GuestCount,
                r.Status,
                r.TotalPrice,
                r.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(reservations);
    }

    // POST: api/reservations
    // Only authenticated admins can create reservations.
    [HttpPost]
    public async Task<IActionResult> CreateReservation(
        [FromBody] CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAdminAuthenticated())
        {
            return Unauthorized(new
            {
                message = "Admin login required."
            });
        }

        var guestName = request.GuestName?.Trim();
        var guestPhone = request.GuestPhone?.Trim();

        if (string.IsNullOrWhiteSpace(guestName) ||
            string.IsNullOrWhiteSpace(guestPhone))
        {
            return BadRequest(new
            {
                message = "Guest name and phone are required."
            });
        }

        if (guestName.Length > 120 || guestPhone.Length > 20)
        {
            return BadRequest(new
            {
                message = "Guest name or phone is too long."
            });
        }

        if (request.RoomId <= 0 || request.GuestCount < 1)
        {
            return BadRequest(new
            {
                message = "A valid room and guest count are required."
            });
        }

        if (request.CheckInDate <
                DateOnly.FromDateTime(DateTime.UtcNow) ||
            request.CheckOutDate <= request.CheckInDate)
        {
            return BadRequest(new
            {
                message =
                    "Check-in cannot be in the past, and check-out must be after check-in."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            var room = await _context.Rooms
                .SingleOrDefaultAsync(
                    r => r.Id == request.RoomId,
                    cancellationToken);

            if (room is null)
            {
                return NotFound(new
                {
                    message = "Room not found."
                });
            }

            if (room.Status != "Available")
            {
                return Conflict(new
                {
                    message = "This room is not available for booking."
                });
            }

            if (request.GuestCount > room.Capacity)
            {
                return BadRequest(new
                {
                    message =
                        $"This room can accommodate at most {room.Capacity} guests."
                });
            }

            // Prevent overlapping confirmed reservations.
            var overlaps = await _context.Reservations.AnyAsync(r =>
                r.RoomId == room.Id &&
                r.Status == "Confirmed" &&
                r.CheckInDate < request.CheckOutDate &&
                r.CheckOutDate > request.CheckInDate,
                cancellationToken);

            if (overlaps)
            {
                return Conflict(new
                {
                    message =
                        "This room is already reserved for some or all of those dates."
                });
            }

            // Find an existing guest using the exact phone number.
            var guest = await _context.Guests
                .SingleOrDefaultAsync(
                    g => g.Phone == guestPhone,
                    cancellationToken);

            // Register the guest if they do not already exist.
            if (guest is null)
            {
                guest = new Guest
                {
                    FullName = guestName,
                    Phone = guestPhone,
                    Email = null,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _context.Guests.Add(guest);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var nights = request.CheckOutDate.DayNumber -
                         request.CheckInDate.DayNumber;

            var reservation = new Reservation
            {
                GuestName = guestName,
                GuestPhone = guestPhone,
                GuestId = guest.Id,
                RoomId = room.Id,
                CheckInDate = request.CheckInDate,
                CheckOutDate = request.CheckOutDate,
                GuestCount = request.GuestCount,
                Status = "Confirmed",
                TotalPrice = room.PricePerNight * nights,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.Reservations.Add(reservation);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return CreatedAtAction(
                nameof(GetReservations),
                new { id = reservation.Id },
                new
                {
                    reservation.Id,
                    reservation.GuestId,
                    reservation.GuestName,
                    reservation.GuestPhone,
                    reservation.RoomId,
                    RoomNumber = room.RoomNumber,
                    reservation.CheckInDate,
                    reservation.CheckOutDate,
                    reservation.GuestCount,
                    reservation.Status,
                    reservation.TotalPrice,
                    message =
                        "Reservation created and linked to guest successfully."
                });
        }
        catch (PostgresException ex)
            when (ex.SqlState ==
                  PostgresErrorCodes.SerializationFailure)
        {
            return Conflict(new
            {
                message =
                    "A concurrent booking changed availability. Please check availability and try again."
            });
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException postgres &&
                  postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new
            {
                message =
                    "A guest with this phone number was registered concurrently. Please retry the reservation."
            });
        }
    }

    // GET: api/reservations/availability
    // Public: guests can check room availability before contacting staff.
    [HttpGet("availability")]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] DateOnly checkInDate,
        [FromQuery] DateOnly checkOutDate,
        CancellationToken cancellationToken)
    {
        if (checkInDate <
            DateOnly.FromDateTime(DateTime.Today))
        {
            return BadRequest(new
            {
                message = "Check-in cannot be in the past."
            });
        }

        if (checkOutDate <= checkInDate)
        {
            return BadRequest(new
            {
                message = "Check-out must be after check-in."
            });
        }

        var bookedRoomIds = await _context.Reservations
            .Where(r =>
                r.Status == "Confirmed" &&
                r.CheckInDate < checkOutDate &&
                r.CheckOutDate > checkInDate)
            .Select(r => r.RoomId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var rooms = await _context.Rooms
            .Where(r =>
                r.Status == "Available" &&
                !bookedRoomIds.Contains(r.Id))
            .OrderBy(r => r.RoomNumber)
            .Select(r => new
            {
                r.Id,
                r.RoomNumber,
                r.RoomType,
                r.PricePerNight,
                r.Capacity,
                r.Status
            })
            .ToListAsync(cancellationToken);

        return Ok(rooms);
    }

    // PATCH: api/reservations/{id}/cancel
    // Only authenticated admins can cancel reservations.
    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> CancelReservation(
        int id,
        CancellationToken cancellationToken)
    {
        if (!IsAdminAuthenticated())
        {
            return Unauthorized(new
            {
                message = "Admin login required."
            });
        }

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(
                r => r.Id == id,
                cancellationToken);

        if (reservation is null)
        {
            return NotFound(new
            {
                message = $"Reservation #{id} was not found."
            });
        }

        if (reservation.Status != "Confirmed")
        {
            return Conflict(new
            {
                message = "Only confirmed reservations can be cancelled.",
                currentStatus = reservation.Status
            });
        }

        reservation.Status = "Cancelled";

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message =
                $"Reservation #{reservation.Id} cancelled successfully.",
            reservationId = reservation.Id,
            status = reservation.Status
        });
    }
}
