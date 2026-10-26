
using HotelManagement.Api.Data;
using HotelManagement.Api.Models;
using HotelManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HotelManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GuestsController : ControllerBase
{
    private readonly HotelDbContext _db;
    private readonly AdminSessionService _adminSessionService;

    public GuestsController(
        HotelDbContext db,
        AdminSessionService adminSessionService)
    {
        _db = db;
        _adminSessionService = adminSessionService;
    }

    private bool IsAdminAuthenticated()
    {
        var token = Request.Headers["X-Admin-Session"].FirstOrDefault();
        return _adminSessionService.Validate(token);
    }

    // GET: /api/guests
    // GET: /api/guests?search=guest-name
    [HttpGet]
    public async Task<IActionResult> GetGuests(
        [FromQuery] string? search)
    {
        if (!IsAdminAuthenticated())
        {
            return Unauthorized(new
            {
                message = "Admin login required."
            });
        }

        var query = _db.Guests.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(g =>
                g.FullName.Contains(term) ||
                g.Phone.Contains(term) ||
                (g.Email != null && g.Email.Contains(term)));
        }

        var guests = await query
            .OrderBy(g => g.FullName)
            .Select(g => new
            {
                g.Id,
                g.FullName,
                g.Phone,
                g.Email,
                g.CreatedAtUtc,
                ReservationCount = g.Reservations.Count()
            })
            .ToListAsync();

        return Ok(guests);
    }

    // GET: /api/guests/1
    // Includes reservation history.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetGuest(int id)
    {
        if (!IsAdminAuthenticated())
        {
            return Unauthorized(new
            {
                message = "Admin login required."
            });
        }

        var guest = await _db.Guests
            .AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new
            {
                g.Id,
                g.FullName,
                g.Phone,
                g.Email,
                g.CreatedAtUtc,

                Reservations = g.Reservations
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .Select(r => new
                    {
                        r.Id,
                        r.RoomId,
                        RoomNumber = r.Room.RoomNumber,
                        r.CheckInDate,
                        r.CheckOutDate,
                        r.GuestCount,
                        r.Status,
                        r.TotalPrice,
                        r.CreatedAtUtc
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (guest == null)
        {
            return NotFound(new
            {
                message = $"Guest with ID {id} was not found."
            });
        }

        return Ok(guest);
    }

    // POST: /api/guests
    // Admin only.
    [HttpPost]
    public async Task<IActionResult> CreateGuest(
        [FromBody] CreateGuestRequest request)
    {
        if (!IsAdminAuthenticated())
        {
            return Unauthorized(new
            {
                message = "Admin login required."
            });
        }

        var fullName = request.FullName.Trim();
        var phone = request.Phone.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest(new
            {
                message = "Guest name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new
            {
                message = "Guest phone number is required."
            });
        }

        if (fullName.Length > 120)
        {
            return BadRequest(new
            {
                message = "Guest name cannot exceed 120 characters."
            });
        }

        if (phone.Length > 20)
        {
            return BadRequest(new
            {
                message = "Phone number cannot exceed 20 characters."
            });
        }

        if (email != null && email.Length > 254)
        {
            return BadRequest(new
            {
                message = "Email cannot exceed 254 characters."
            });
        }

        if (email != null &&
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute()
                .IsValid(email))
        {
            return BadRequest(new
            {
                message = "Please provide a valid email address."
            });
        }

        var phoneExists = await _db.Guests
            .AnyAsync(g => g.Phone == phone);

        if (phoneExists)
        {
            return Conflict(new
            {
                message = "A guest with this phone number already exists."
            });
        }

        var guest = new Guest
        {
            FullName = fullName,
            Phone = phone,
            Email = email,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Guests.Add(guest);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException postgres &&
                  postgres.SqlState == "23505")
        {
            return Conflict(new
            {
                message = "A guest with this phone number already exists."
            });
        }

        return CreatedAtAction(
            nameof(GetGuest),
            new { id = guest.Id },
            new
            {
                guest.Id,
                guest.FullName,
                guest.Phone,
                guest.Email,
                guest.CreatedAtUtc,
                message = "Guest registered successfully."
            });
    }
}

public class CreateGuestRequest
{
    public string FullName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }
}
