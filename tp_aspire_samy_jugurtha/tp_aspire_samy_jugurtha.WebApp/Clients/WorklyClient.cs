using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using tp_aspire_samy_jugurtha.WebApp.Models;

namespace tp_aspire_samy_jugurtha.WebApp.Clients;

public class WorklyClient(HttpClient http) : IWorklyClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<Workspace>> GetWorkspacesAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync("/api/workspaces", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Workspace>>(JsonOptions, ct) ?? [];
    }

    public async Task<Workspace?> CreateWorkspaceAsync(Workspace workspace, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/workspaces", workspace, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Workspace>(JsonOptions, ct);
    }

    public async Task<List<Room>> GetRoomsAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync("/api/rooms", ct);
        
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException();

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Room>>(JsonOptions, ct) ?? [];
    }

    public async Task<Room?> CreateRoomAsync(Room room, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/rooms", room, JsonOptions, ct);
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Room>(JsonOptions, ct);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions, ct);
            throw new InvalidOperationException(error?.Message ?? "Workspace introuvable.");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException();

        response.EnsureSuccessStatusCode();
        return null;
    }

    public async Task<List<Booking>> GetBookingsAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync("/api/bookings", ct);
        
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException();

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Booking>>(JsonOptions, ct) ?? [];
    }

    public async Task<List<Booking>> GetAllBookingsAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync("/api/bookings/all", ct);
        
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException();

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Booking>>(JsonOptions, ct) ?? [];
    }

    public async Task<Booking?> CreateBookingAsync(Booking booking, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/bookings", booking, JsonOptions, ct);
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Booking>(JsonOptions, ct);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = await response.Content.ReadFromJsonAsync<BookingConflictResponse>(JsonOptions, ct);
            throw new BookingConflictException(
                conflict?.Message ?? "Ce créneau est déjà réservé.",
                conflict?.ExistingStartUtc,
                conflict?.ExistingEndUtc);
        }

        response.EnsureSuccessStatusCode();
        return null;
    }

    public async Task DeleteBookingAsync(int bookingId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/bookings/{bookingId}", ct);
        response.EnsureSuccessStatusCode();
    }

    private sealed record BookingConflictResponse(string? Message, DateTime? ExistingStartUtc, DateTime? ExistingEndUtc);
    private sealed record ErrorResponse(string? Message);
}
