using Npgsql;
using train2.Models;
using train4.Services.Base;

namespace train4.Services.Implementation
{
    public class StationService : BaseDatabaseService, Interfaces.IStationService
    {
        public StationService(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<StationService> logger)
            : base(configuration, httpContextAccessor, logger) { }


        public async Task<List<Stations>> GetAllStationsAsync()
        {
            var stations = new List<Stations>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT station_id, station_name FROM stations_view", conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    stations.Add(new Stations
                    {
                        station_id = reader.GetInt32(reader.GetOrdinal("station_id")),
                        station_name = reader.GetString(reader.GetOrdinal("station_name"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання списку станцій через VIEW");
            }
            return stations;
        }

        public async Task<string> AddStationAsync(string name)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT add_station(@name)", conn);
                cmd.Parameters.AddWithValue("name", name);
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString() ?? "Невідома відповідь";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка додавання станції");
                return "Помилка при додаванні станції";
            }
        }

        public async Task<string> DeleteStationAsync(int id)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT delete_station(@id)", conn);
                cmd.Parameters.AddWithValue("id", id);
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString() ?? "Невідома відповідь";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка видалення станції");
                return "Помилка при видаленні станції";
            }
        }

        public async Task<string> UpdateStationNameAsync(int id, string newName)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT update_station(@id, @name)", conn);
                cmd.Parameters.AddWithValue("id", id);
                cmd.Parameters.AddWithValue("name", newName);
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString() ?? "Невідома відповідь";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка оновлення назви станції ID {id}");
                return "Помилка при оновленні станції";
            }
        }

        public async Task<int?> GetStationIdByNameAsync(string stationName)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = "SELECT station_id FROM station_ids_view WHERE station_name = @station_name";
                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("station_name", stationName);

                var result = await cmd.ExecuteScalarAsync();
                return result as int?;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка при отриманні ID станції: {stationName}");
                return null;
            }
        }
    }
}
