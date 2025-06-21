using train2.Models;

namespace train4.Services.Interfaces
{
    public interface IScheduleService
    {
        Task<List<Schedule>> SearchSchedulesAsync(string fromStation, string toStation, DateTime date);

        Task<List<Dictionary<string, object>>> SearchSchedulesDictionaryAsync(string fromStation, string toStation, DateTime date);

        Task<(bool Success, string ErrorMessage)> CreateRouteAsync(int trainId, List<int> stationIds);

        Task<(string TrainNumber, List<StationSequence> Route)> GetRouteByTrainIdAsync(int trainId);

        Task<int> GetScheduleIdAsync(int trainId, DateTime travelDate);

        Task<(List<StationSequence>, bool)> GetRouteWithEditPermissionAsync(int trainId);

        Task<bool> UpdateRouteDurationsAndPricesAsync(int trainId, List<StationSequence> updatedRoute);
    }
}
