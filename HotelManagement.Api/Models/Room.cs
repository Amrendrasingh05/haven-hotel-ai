
namespace HotelManagement.Api.Models;

public class Room
{
    public int Id { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string RoomType { get; set; } = "Standard";

    public decimal PricePerNight { get; set; }

    public int Capacity { get; set; } = 2;

    public string Status { get; set; } = "Available";
}
