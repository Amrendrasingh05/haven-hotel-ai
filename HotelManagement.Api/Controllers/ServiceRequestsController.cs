
using HotelManagement.Api.Data;
using HotelManagement.Api.Models;
using HotelManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Api.Controllers;

[ApiController]
[Route("api/service-requests")]
public class ServiceRequestsController : ControllerBase
{
    private readonly HotelDbContext _context;
    private readonly GuestSessionService _guestSessionService;
    private readonly AdminSessionService _adminSessionService;

    public ServiceRequestsController(
        HotelDbContext context,
        GuestSessionService guestSessionService,
        AdminSessionService adminSessionService)
    {
        _context = context;
        _guestSessionService = guestSessionService;
        _adminSessionService = adminSessionService;
    }

    // GET: api/service-requests
    // Requires a valid X-Admin-Session header.
    // Optional filters: ?status=Open or ?department=Maintenance
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? department)
    {
        var adminToken =
            Request.Headers["X-Admin-Session"].FirstOrDefault();

        if (!_adminSessionService.Validate(adminToken))
        {
            return Unauthorized(new
            {
                message = "Admin login required."
            });
        }

        var query = _context.ServiceRequests
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(r => r.Department == department);
        }

        var requests = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new
            {
                r.Id,
                r.GuestId,
                r.ReservationId,
                r.GuestName,
                r.RoomNumber,
                r.Description,
                r.Category,
                r.Department,
                r.Priority,
                r.Status,
                r.CreatedAtUtc,
                r.ResolvedAtUtc
            })
            .ToListAsync();

        return Ok(requests);
    }

    // POST: api/service-requests
    // Guest ticket creation requires a valid X-Guest-Session header.
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateServiceRequest request)
    {
        var token =
            Request.Headers["X-Guest-Session"].FirstOrDefault();

        var session = await _guestSessionService.ValidateAsync(
            token,
            HttpContext.RequestAborted);

        if (session is null)
        {
            return Unauthorized(new
            {
                message =
                    "Your guest session is invalid or expired. Please log in again."
            });
        }

        var description = request.Description?.Trim();

        if (string.IsNullOrWhiteSpace(description))
        {
            return BadRequest(new
            {
                message = "Please describe your issue."
            });
        }

        if (description.Length > 500)
        {
            return BadRequest(new
            {
                message = "Description cannot exceed 500 characters."
            });
        }

        var guest = session.Guest;
        var reservation = session.Reservation;

        if (guest is null || reservation?.Room is null)
        {
            return Unauthorized(new
            {
                message =
                    "Guest session is invalid. Please log in again."
            });
        }

        var department = AssignDepartment(description);
        var category = GetCategory(department);

        var serviceRequest = new ServiceRequest
        {
            GuestId = guest.Id,
            ReservationId = reservation.Id,
            GuestName = guest.FullName,
            RoomNumber = reservation.Room.RoomNumber,
            Description = description,
            Category = category,
            Department = department,
            Priority = GetPriority(description),
            Status = "Open",
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.ServiceRequests.Add(serviceRequest);
        await _context.SaveChangesAsync();

        return Created(
            $"/api/service-requests/{serviceRequest.Id}",
            new
            {
                serviceRequest.Id,
                serviceRequest.GuestId,
                serviceRequest.ReservationId,
                serviceRequest.GuestName,
                serviceRequest.RoomNumber,
                serviceRequest.Description,
                serviceRequest.Category,
                serviceRequest.Department,
                serviceRequest.Priority,
                serviceRequest.Status,
                serviceRequest.CreatedAtUtc,
                message =
                    $"Your ticket #{serviceRequest.Id} has been created and assigned to {department}."
            });
    }

    // PATCH: api/service-requests/1/status
    // Requires a valid X-Admin-Session header.
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateServiceRequestStatus request)
    {
        var adminToken =
            Request.Headers["X-Admin-Session"].FirstOrDefault();

        if (!_adminSessionService.Validate(adminToken))
        {
            return Unauthorized(new
            {
                message = "Admin login required."
            });
        }

        var serviceRequest = await _context.ServiceRequests
            .FirstOrDefaultAsync(r => r.Id == id);

        if (serviceRequest is null)
        {
            return NotFound(new
            {
                message = "Service request not found."
            });
        }

        var allowedStatuses = new[]
        {
            "Open",
            "In Progress",
            "Resolved",
            "Cancelled"
        };

        var newStatus = request.Status?.Trim();

        if (string.IsNullOrWhiteSpace(newStatus) ||
            !allowedStatuses.Contains(
                newStatus,
                StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Status must be Open, In Progress, Resolved, or Cancelled."
            });
        }

        serviceRequest.Status = allowedStatuses.First(
            s => s.Equals(
                newStatus,
                StringComparison.OrdinalIgnoreCase));

        serviceRequest.ResolvedAtUtc =
            serviceRequest.Status == "Resolved"
                ? DateTime.UtcNow
                : null;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            serviceRequest.Id,
            serviceRequest.Status,
            serviceRequest.ResolvedAtUtc,
            message = "Service request status updated."
        });
    }

    private static string AssignDepartment(string description)
    {
        var text = description.ToLowerInvariant();

        if (ContainsAny(
            text,
            "wifi",
            "wi-fi",
            "internet",
            "network",
            "television",
            " tv ",
            "tv not",
            "remote control",
            "remote isn't working",
            "remote is not working"))
        {
            return "IT";
        }

        if (ContainsAny(
            text,
            "towel",
            "towels",
            "cleaning",
            "clean room",
            "housekeeping",
            "bedsheet",
            "bed sheet",
            "pillow",
            "blanket",
            "laundry"))
        {
            return "Housekeeping";
        }

        if (ContainsAny(
                text,
                "air conditioner",
                "air conditioning",
                " ac ",
                "ac not",
                "plumbing",
                "leak",
                "water tap",
                "faucet",
                "toilet",
                "shower",
                "flush",
                "light",
                "bulb",
                "electrical",
                "power socket",
                "maintenance",
                "broken")
            || text.StartsWith("ac "))
        {
            return "Maintenance";
        }

        if (ContainsAny(
            text,
            "food",
            "room service",
            "meal",
            "breakfast",
            "lunch",
            "dinner",
            "order food"))
        {
            return "Room Service";
        }

        if (ContainsAny(
            text,
            "bill",
            "payment",
            "invoice",
            "refund",
            "charge"))
        {
            return "Accounts";
        }

        if (ContainsAny(
            text,
            "security",
            "lost item",
            "lost property",
            "suspicious"))
        {
            return "Security";
        }

        if (ContainsAny(
            text,
            "manager",
            "complaint",
            "emergency",
            "urgent"))
        {
            return "Duty Manager";
        }

        return "Front Desk";
    }

    private static string GetCategory(string department)
    {
        return department switch
        {
            "IT" => "Technical",
            "Housekeeping" => "Housekeeping",
            "Maintenance" => "Maintenance",
            "Room Service" => "Room Service",
            "Accounts" => "Billing",
            "Security" => "Security",
            "Duty Manager" => "Complaint",
            _ => "General"
        };
    }

    private static string GetPriority(string description)
    {
        var text = description.ToLowerInvariant();

        return ContainsAny(
            text,
            "emergency",
            "fire",
            "smoke",
            "gas leak",
            "medical emergency",
            "urgent")
            ? "High"
            : "Normal";
    }

    private static bool ContainsAny(
        string text,
        params string[] keywords)
    {
        return keywords.Any(text.Contains);
    }
}

public class CreateServiceRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    // Guest identity is taken from the validated session.
    public string? GuestName { get; set; }
    public string? RoomNumber { get; set; }
    public int? GuestId { get; set; }
    public int? ReservationId { get; set; }
}

public class UpdateServiceRequestStatus
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Status { get; set; } = string.Empty;
}
