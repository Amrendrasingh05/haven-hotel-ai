
using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Api.Models;

public class GuestSession
{
    public int Id { get; set; }

    public int GuestId { get; set; }

    public Guest Guest { get; set; } = null!;

    public int ReservationId { get; set; }

    public Reservation Reservation { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }
}
