
using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Api.Models;

public class ServiceRequest
{
    public int Id { get; set; }

    public int? GuestId { get; set; }
    public Guest? Guest { get; set; }

    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    [Required]
    [MaxLength(120)]
    public string GuestName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string RoomNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "General";

    [Required]
    [MaxLength(50)]
    public string Department { get; set; } = "Front Desk";

    [Required]
    [MaxLength(20)]
    public string Priority { get; set; } = "Normal";

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Open";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }
}
