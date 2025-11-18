namespace tp_aspire_samy_jugurtha.WebApp.Models;

public enum ResourceType { Room = 1, Desk = 2 }
public enum BookingStatus { Pending = 1, Confirmed = 2, Cancelled = 3 }

public class Booking
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public ResourceType ResourceType { get; set; }
    public int ResourceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
}
