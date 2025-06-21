using Npgsql;
using train4.Services.Base;

namespace train4.Services.Implementation
{
    public class AnalyticsService : BaseDatabaseService, Interfaces.IAnalyticsService
    {
        public AnalyticsService(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AnalyticsService> logger)
            : base(configuration, httpContextAccessor, logger) { }

        public async Task<List<Dictionary<string, object>>> GetTopFilledRoutesAsync(DateTime start, DateTime end)
        {
            var result = new List<Dictionary<string, object>>();
            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("SELECT * FROM get_top_filled_routes(@start::DATE, @end::DATE)", conn);
            cmd.Parameters.AddWithValue("start", start);
            cmd.Parameters.AddWithValue("end", end);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new Dictionary<string, object>
                {
                    ["schedule_id"] = reader["schedule_id"],
                    ["train_number"] = reader["train_number"],
                    ["booked_tickets_count"] = reader["booked_tickets_count"]
                });
            }

            return result;
        }

        public async Task<List<Dictionary<string, object>>> GetTopIncomeRoutesAsync(DateTime start, DateTime end)
        {
            var result = new List<Dictionary<string, object>>();
            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("SELECT * FROM get_top_income_routes(@start::DATE, @end::DATE)", conn);
            cmd.Parameters.AddWithValue("start", start);
            cmd.Parameters.AddWithValue("end", end);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new Dictionary<string, object>
                {
                    ["schedule_id"] = reader["schedule_id"],
                    ["train_number"] = reader["train_number"],
                    ["tickets_sold"] = reader["tickets_sold"],
                    ["total_income"] = reader["total_income"]
                });
            }

            return result;
        }

        public async Task<List<Dictionary<string, object>>> GetMostPopularRoutesAsync(DateTime start, DateTime end)
        {
            var result = new List<Dictionary<string, object>>();
            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("SELECT * FROM get_most_popular_routes(@start::DATE, @end::DATE)", conn);
            cmd.Parameters.AddWithValue("start", start);
            cmd.Parameters.AddWithValue("end", end);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new Dictionary<string, object>
                {
                    ["train_number"] = reader["train_number"],
                    ["from_station"] = reader["from_station"],
                    ["to_station"] = reader["to_station"],
                    ["tickets_sold"] = reader["tickets_sold"]
                });
            }

            return result;
        }
    }
}
