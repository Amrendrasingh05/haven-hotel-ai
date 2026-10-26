
using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Api.Models;

public class Reservation
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string GuestName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string GuestPhone { get; set; } = string.Empty;

    // Optional link to the Guest record.
    // Existing reservations remain valid.
    public int? GuestId { get; set; }
    public Guest? Guest { get; set; }

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }

    public int GuestCount { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Confirmed";

    public decimal TotalPrice { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
