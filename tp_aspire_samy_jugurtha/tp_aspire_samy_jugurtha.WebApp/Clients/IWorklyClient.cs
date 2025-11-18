namespace tp_aspire_samy_jugurtha.WebApp.Clients;

using Models;

public interface IWorklyClient
{
    Task<List<Room>> GetRoomsAsync(CancellationToken ct = default);
    Task<Room?> CreateRoomAsync(Room room, CancellationToken ct = default);

    Task<List<Booking>> GetBookingsAsync(CancellationToken ct = default);
    Task<List<Booking>> GetAllBookingsAsync(CancellationToken ct = default);
    Task<Booking?> CreateBookingAsync(Booking booking, CancellationToken ct = default);
    Task DeleteBookingAsync(int bookingId, CancellationToken ct = default);
}
