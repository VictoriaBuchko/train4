using train2.Models;

namespace train4.Services.Interfaces
{
    public interface IStationService
    {
        Task<List<Stations>> GetAllStationsAsync();
        Task<string> AddStationAsync(string name);
        Task<string> DeleteStationAsync(int id);
        Task<string> UpdateStationNameAsync(int id, string newName);
        Task<int?> GetStationIdByNameAsync(string stationName);
    }
}
