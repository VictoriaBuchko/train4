using train2.Models;

namespace train4.Services.Interfaces
{
    public interface ITrainService
    {
        Task<List<Train>> GetAllTrainsAsync();
        Task<List<CarriageTypes>> GetAllCarriageTypesAsync();
        Task<(bool Success, string ErrorMessage)> CreateTrainWithCarriagesAsync(int trainNumber, int[] carriageTypeIds, int[] carriageNumbers);
        Task<(Train Train, Dictionary<int, int> SeatCounts)> GetTrainDetailsAsync(int trainId);
        Task<(Train train, List<Dictionary<string, object>> carriages)> GetTrainForEditAsync(int trainId);
        Task<(bool Success, string ErrorMessage)> UpdateTrainAsync(int trainId, int trainNumber, List<Dictionary<string, object>> carriageData);
        Task<List<Dictionary<string, object>>> GetCarriagesWithBookingsAsync(int trainId);
        Task<(bool CanChange, string Reason, int ActiveTickets)> CanChangeTrainStatusAsync(int trainId);
        Task<(bool Success, string Message, bool NewStatus)> ToggleTrainStatusAsync(int trainId);
        Task<Train?> GetTrainByIdAsync(int trainId);
    }
}
