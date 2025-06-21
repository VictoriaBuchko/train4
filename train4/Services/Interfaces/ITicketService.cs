using train2.Models;

namespace train4.Services.Interfaces
{
    public interface ITicketService
    {
        //Task<List<Seat>> GetAvailableSeatsAsync(int trainId, DateTime travelDate, int fromStationId, int toStationId);
        Task<List<Dictionary<string, object>>> GetSeatsWithDetailsAsync(int trainId, DateTime travelDate, int fromStationId, int toStationId);
        Task<Dictionary<string, object>> GetTicketInfoAsync(int trainId, DateTime date, int seatId, int fromStationId, int toStationId);
        Task<Dictionary<string, object>> CreateTicketBookingAsync(int clientId, int seatId, int trainId, DateTime date, int fromStationId, int toStationId);
        Task<bool> BookSeatAsync(int seatId, int clientId, DateTime travelDate);
        Task<List<Dictionary<string, object>>> SearchTicketsByClientAsync(string clientQuery);
        Task<Dictionary<string, object>> CancelTicketAsync(int ticketId);


        Task<List<Ticket>> GetActiveTicketsAsync(int clientId);
        Task<List<Ticket>> GetHistoricalTicketsAsync(int clientId);
    }
}
