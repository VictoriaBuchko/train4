namespace train4.Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<List<Dictionary<string, object>>> GetTopFilledRoutesAsync(DateTime start, DateTime end);

        Task<List<Dictionary<string, object>>> GetTopIncomeRoutesAsync(DateTime start, DateTime end);

        Task<List<Dictionary<string, object>>> GetMostPopularRoutesAsync(DateTime start, DateTime end);
    }
}
