using train2.Models;

namespace train2.Services
{
    public interface ITicketServicetest
    {
        Task<List<Ticket>> GetActiveTicketsAsync(int clientId);
        Task<List<Ticket>> GetHistoricalTicketsAsync(int clientId);
    }
}
