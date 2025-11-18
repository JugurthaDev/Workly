namespace tp_aspire_samy_jugurtha.WebApp.Clients;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Models;

public class WorklyClient(HttpClient http) : IWorklyClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Room>(JsonOpts, ct);
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
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Booking>(JsonOpts, ct);
    }

    public async Task DeleteBookingAsync(int bookingId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/api/bookings/{bookingId}", ct);
        resp.EnsureSuccessStatusCode();
    }
}
