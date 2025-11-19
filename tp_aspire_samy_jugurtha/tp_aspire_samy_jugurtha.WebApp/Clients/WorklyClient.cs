namespace tp_aspire_samy_jugurtha.WebApp.Clients;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using tp_aspire_samy_jugurtha.WebApp.Models;

public class WorklyClient(HttpClient http) : IWorklyClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ----- Workspaces -----
    public async Task<List<Workspace>> GetWorkspacesAsync(CancellationToken ct = default)
    {
        var resp = await http.GetAsync("/api/workspaces", ct);
        if (!resp.IsSuccessStatusCode)
        {
            resp.EnsureSuccessStatusCode();
        }
        return await resp.Content.ReadFromJsonAsync<List<Workspace>>(JsonOpts, ct) ?? new List<Workspace>();
    }

    public async Task<Workspace?> CreateWorkspaceAsync(Workspace ws, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/workspaces", ws, JsonOpts, ct);
        if (!resp.IsSuccessStatusCode)
        {
            resp.EnsureSuccessStatusCode();
        }
        return await resp.Content.ReadFromJsonAsync<Workspace>(JsonOpts, ct);
    }

    // ----- Rooms -----
    public async Task<List<Room>> GetRoomsAsync(CancellationToken ct = default)
    {
        var resp = await http.GetAsync("/api/rooms", ct);
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException();

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<Room>>(JsonOpts, ct) ?? new List<Room>();
    }

    public async Task<Room?> CreateRoomAsync(Room room, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/rooms", room, JsonOpts, ct);
        if (resp.IsSuccessStatusCode)
        {
            return await resp.Content.ReadFromJsonAsync<Room>(JsonOpts, ct);
        }

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            // Workspace inexistant
            var error = await resp.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts, ct);
            throw new InvalidOperationException(error?.Message ?? "Workspace introuvable.");
        }

        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException();

        resp.EnsureSuccessStatusCode();
        return null;
    }

    // ----- Bookings -----
    public async Task<List<Booking>> GetBookingsAsync(CancellationToken ct = default)
    {
        var resp = await http.GetAsync("/api/bookings", ct);
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException();

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<Booking>>(JsonOpts, ct) ?? new List<Booking>();
    }

    public async Task<List<Booking>> GetAllBookingsAsync(CancellationToken ct = default)
    {
        var resp = await http.GetAsync("/api/bookings/all", ct);
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException();

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<Booking>>(JsonOpts, ct) ?? new List<Booking>();
    }

    public async Task<Booking?> CreateBookingAsync(Booking booking, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/bookings", booking, JsonOpts, ct);
        if (resp.IsSuccessStatusCode)
        {
            return await resp.Content.ReadFromJsonAsync<Booking>(JsonOpts, ct);
        }

        if (resp.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = await resp.Content.ReadFromJsonAsync<BookingConflictResponse>(JsonOpts, ct);
            throw new BookingConflictException(
                conflict?.Message ?? "Ce créneau est déjà réservé.",
                conflict?.ExistingStartUtc,
                conflict?.ExistingEndUtc);
        }

        resp.EnsureSuccessStatusCode();
        return null;
    }

    public async Task DeleteBookingAsync(int bookingId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/api/bookings/{bookingId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    private sealed record BookingConflictResponse(string? Message, DateTime? ExistingStartUtc, DateTime? ExistingEndUtc);
    private sealed record ErrorResponse(string? Message);
}
