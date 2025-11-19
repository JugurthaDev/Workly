namespace tp_aspire_samy_jugurtha.WebApp.Clients;

using System.Net;

public sealed class BookingConflictException : HttpRequestException
{
    public BookingConflictException(string? message, DateTime? existingStartUtc, DateTime? existingEndUtc)
        : base(message ?? "Ce créneau est déjà réservé.", null, HttpStatusCode.Conflict)
    {
        ExistingStartUtc = existingStartUtc;
        ExistingEndUtc = existingEndUtc;
    }

    public DateTime? ExistingStartUtc { get; }
    public DateTime? ExistingEndUtc { get; }
}
