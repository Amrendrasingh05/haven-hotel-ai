
using HotelManagement.Api.Data;
using HotelManagement.Api.Models;
using HotelManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly HotelDbContext _context;
    private readonly AdminSessionService _adminSessionService;

    public RoomsController(
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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Room>>> GetRooms()
    {
        var rooms = await _context.Rooms
            .AsNoTracking()
            .OrderBy(room => room.RoomNumber)
            .ToListAsync();

        return Ok(rooms);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Room>> GetRoom(int id)
    {
        var room = await _context.Rooms.FindAsync(id);

        if (room is null)
            return NotFound();

        return Ok(room);
    }

    [HttpPost]
    public async Task<ActionResult<Room>> CreateRoom(Room room)
    {
        if (!IsAdminAuthenticated())
        {
            return Unauthorized(new
            {
                message = "Admin login required."
            });
        }

        if (room.Id != 0)
            return BadRequest("A new room must not specify an ID.");

        if (string.IsNullOrWhiteSpace(room.RoomNumber))
            return BadRequest("Room number is required.");

        if (room.PricePerNight < 0 || room.Capacity < 1)
            return BadRequest(
                "Price cannot be negative and capacity must be at least 1.");

        room.RoomNumber = room.RoomNumber.Trim();
        room.RoomType = room.RoomType.Trim();

        var duplicate = await _context.Rooms.AnyAsync(
            r => r.RoomNumber == room.RoomNumber);

        if (duplicate)
            return Conflict("That room number already exists.");

        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetRoom),
            new { id = room.Id },
            room);
    }
}
