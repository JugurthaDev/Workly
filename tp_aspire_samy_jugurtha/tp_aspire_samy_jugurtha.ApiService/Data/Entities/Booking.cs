namespace tp_aspire_samy_jugurtha.ApiService.Data.Entities;

public class Booking
{
    public int Id { get; set; }
    public int AppUserId { get; set; }

    public ResourceType ResourceType { get; set; }
    public int ResourceId { get; set; }

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public AppUser? AppUser { get; set; }
}
