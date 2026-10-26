
using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Api.Models;

public class Guest
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(254)]
    [EmailAddress]
    public string? Email { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Reservation> Reservations { get; set; }
        = new List<Reservation>();
}
