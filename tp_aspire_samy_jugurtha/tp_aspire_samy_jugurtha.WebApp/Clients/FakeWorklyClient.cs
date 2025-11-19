using tp_aspire_samy_jugurtha.WebApp.Models;

namespace tp_aspire_samy_jugurtha.WebApp.Clients;

public class FakeWorklyClient : IWorklyClient
{
    private readonly List<Room> _rooms = new()
    {
        new Room { Id = 1, WorkspaceId = 1, Name = "Salle Volt", Capacity = 6 }
    };

    private readonly List<Booking> _bookings = new();
    private int _bookingId = 1;

    public Task<List<Room>> GetRoomsAsync(CancellationToken ct = default)
        => Task.FromResult(_rooms.ToList());

    public Task<Room?> CreateRoomAsync(Room room, CancellationToken ct = default)
    {
        room.Id = _rooms.Max(r => (int?)r.Id) + 1 ?? 1;
        _rooms.Add(room);
        return Task.FromResult<Room?>(room);
    }

    public Task<List<Booking>> GetBookingsAsync(CancellationToken ct = default)
        => Task.FromResult(_bookings.ToList());

    public Task<List<Booking>> GetAllBookingsAsync(CancellationToken ct = default)
        => Task.FromResult(_bookings.ToList());

    public Task<Booking?> CreateBookingAsync(Booking booking, CancellationToken ct = default)
    {
        booking.Id = _bookingId++;
        if (booking.EndUtc <= booking.StartUtc)
            throw new InvalidOperationException("End must be after Start");
        _bookings.Add(booking);
        return Task.FromResult<Booking?>(booking);
    }

    public Task DeleteBookingAsync(int bookingId, CancellationToken ct = default)
    {
        var b = _bookings.FirstOrDefault(x => x.Id == bookingId);
        if (b != null) _bookings.Remove(b);
        return Task.CompletedTask;
    }
}
