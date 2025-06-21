namespace train4.Services.Interfaces
{
    public interface IAccountingService
    {   
        Task<List<Dictionary<string, object>>> GetUnpaidTicketsAsync(int page, int pageSize, string searchQuery = "");

        Task<int> GetUnpaidTicketsCountAsync(string searchQuery = "");

        Task<Dictionary<string, object>> ConfirmTicketPaymentAsync(int ticketId, decimal paidAmount, string paymentMethod);

        Task<Dictionary<string, object>> GetFinancialReportAsync(DateTime startDate, DateTime endDate, string reportType);

        Task<List<Dictionary<string, object>>> GetDailyStatisticsAsync(DateTime startDate, DateTime endDate);

        Task<List<Dictionary<string, object>>> GetDetailedFinancialReportAsync(DateTime startDate, DateTime endDate);
    }
}
