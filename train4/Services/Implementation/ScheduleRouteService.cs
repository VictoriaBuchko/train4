using Npgsql;
using train4.Services.Base;

namespace train4.Services.Implementation
{
    public class ScheduleRouteService : BaseDatabaseService, Interfaces.IScheduleRouteService
    {
        public ScheduleRouteService(
                    IConfiguration configuration,
                    IHttpContextAccessor httpContextAccessor,
                    ILogger<ScheduleRouteService> logger)
                    : base(configuration, httpContextAccessor, logger) { }

        public async Task<List<Dictionary<string, object>>> GetAllSchedulesAsync()
        {
            var schedules = new List<Dictionary<string, object>>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT * FROM get_all_schedules()", conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    schedules.Add(new Dictionary<string, object>
                    {
                        ["schedule_id"] = reader.GetInt32(reader.GetOrdinal("schedule_id")),
                        ["train_id"] = reader.GetInt32(reader.GetOrdinal("train_id")),
                        ["date"] = reader.GetDateTime(reader.GetOrdinal("date")),
                        ["weekdays"] = reader.GetString(reader.GetOrdinal("weekdays")),
                        ["departure_time"] = reader.GetTimeSpan(reader.GetOrdinal("departure_time")),
                        ["train_number"] = reader.GetInt32(reader.GetOrdinal("train_number")),
                        ["train_is_active"] = reader.GetBoolean(reader.GetOrdinal("train_is_active")),
                        ["tickets_count"] = reader.GetInt32(reader.GetOrdinal("tickets_count")),
                        ["total_revenue"] = reader.GetDecimal(reader.GetOrdinal("total_revenue")),
                        ["is_active"] = reader.GetBoolean(reader.GetOrdinal("is_active"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні списку розкладів");
            }

            return schedules;
        }


        public async Task<(bool Success, string ErrorMessage)> CreateScheduleAsync(int trainId, DateTime date, TimeSpan departureTime, string weekdays)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(
                    "CALL create_schedule(@trainId, @date::date, @departureTime::time, @weekdays::text)", conn);

                cmd.Parameters.AddWithValue("trainId", trainId);
                cmd.Parameters.AddWithValue("date", date); // приведение сделает ::date
                cmd.Parameters.AddWithValue("departureTime", departureTime); // ::time
                cmd.Parameters.AddWithValue("weekdays", weekdays); // ::text

                await cmd.ExecuteNonQueryAsync();
                return (true, null);
            }
            catch (PostgresException ex) when (ex.SqlState == "P0001")
            {
                return (false, ex.Message); // например: Розклад з такими параметрами вже існує
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при виклику процедури create_schedule");
                return (false, ex.Message);
            }
        }




        public async Task<Dictionary<string, object>?> GetScheduleByIdAsync(int scheduleId)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = "SELECT * FROM get_schedule_by_id(@scheduleId)";
                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("scheduleId", scheduleId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new Dictionary<string, object>
                    {
                        ["schedule_id"] = reader.GetInt32(reader.GetOrdinal("schedule_id")),
                        ["train_id"] = reader.GetInt32(reader.GetOrdinal("train_id")),
                        ["date"] = reader.GetDateTime(reader.GetOrdinal("date")),
                        ["weekdays"] = reader.GetString(reader.GetOrdinal("weekdays")),
                        ["departure_time"] = reader.GetTimeSpan(reader.GetOrdinal("departure_time")),
                        ["train_number"] = reader.GetInt32(reader.GetOrdinal("train_number"))
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні розкладу {ScheduleId}", scheduleId);
                return null;
            }
        }


        public async Task<(bool CanEdit, string Reason)> CanEditScheduleAsync(int scheduleId)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = "SELECT * FROM can_edit_schedule(@scheduleId)";
                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("scheduleId", scheduleId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    bool canEdit = reader.GetBoolean(reader.GetOrdinal("can_edit"));
                    string reason = reader.GetString(reader.GetOrdinal("reason"));
                    return (canEdit, reason);
                }

                return (false, "Помилка виконання функції");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка перевірки можливості редагування розкладу {ScheduleId}", scheduleId);
                return (false, "Помилка перевірки");
            }
        }


        public async Task<(bool Success, string ErrorMessage, string WarningMessage)> UpdateScheduleAsync(int scheduleId, int trainId, DateTime date, TimeSpan departureTime, string weekdays)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("CALL update_schedule(@scheduleId, @trainId, @date, @departureTime, @weekdays)", conn);
                cmd.Parameters.AddWithValue("scheduleId", scheduleId);
                cmd.Parameters.AddWithValue("trainId", trainId);
                cmd.Parameters.AddWithValue("date", date.Date);
                cmd.Parameters.Add("departureTime", NpgsqlTypes.NpgsqlDbType.Time).Value = departureTime;
                cmd.Parameters.AddWithValue("weekdays", weekdays);


                await cmd.ExecuteNonQueryAsync();
                return (true, null, null);
            }
            catch (PostgresException pgex)
            {
                // Вивід помилки з raise exception
                _logger.LogWarning(pgex, "Помилка бізнес-логіки при оновленні розкладу {ScheduleId}", scheduleId);
                return (false, pgex.MessageText, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Серверна помилка при оновленні розкладу {ScheduleId}", scheduleId);
                return (false, "Серверна помилка", null);
            }
        }

        public async Task<(bool Success, string Message, bool NewStatus)> ToggleScheduleStatusAsync(int scheduleId)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("CALL toggle_schedule_status(@scheduleId, @newStatus)", conn);
                cmd.Parameters.AddWithValue("scheduleId", scheduleId);

                var newStatusParam = new NpgsqlParameter("newStatus", NpgsqlTypes.NpgsqlDbType.Boolean)
                {
                    Direction = System.Data.ParameterDirection.Output
                };
                cmd.Parameters.Add(newStatusParam);

                await cmd.ExecuteNonQueryAsync();

                bool newStatus = (bool)newStatusParam.Value;
                return (true, "Статус розкладу змінено", newStatus);
            }
            catch (PostgresException pgex)
            {
                _logger.LogWarning(pgex, "Помилка логіки при зміні статусу розкладу {ScheduleId}", scheduleId);
                return (false, pgex.MessageText, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Серверна помилка при зміні статусу розкладу {ScheduleId}", scheduleId);
                return (false, ex.Message, false);
            }
        }


        public async Task<Dictionary<string, object>?> GetScheduleDetailsAsync(int scheduleId)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = @"
                            SELECT * 
                            FROM schedule_details_view
                            WHERE schedule_id = @scheduleId";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("scheduleId", scheduleId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var schedule = new Dictionary<string, object>
                    {
                        ["schedule_id"] = reader.GetInt32(reader.GetOrdinal("schedule_id")),
                        ["train_id"] = reader.GetInt32(reader.GetOrdinal("train_id")),
                        ["date"] = reader.GetDateTime(reader.GetOrdinal("date")),
                        ["weekdays"] = reader.GetString(reader.GetOrdinal("weekdays")),
                        ["departure_time"] = reader.GetTimeSpan(reader.GetOrdinal("departure_time")),
                        ["train_number"] = reader.GetInt32(reader.GetOrdinal("train_number")),
                        ["carriage_count"] = reader.GetInt32(reader.GetOrdinal("carriage_count")),
                        ["train_is_active"] = reader.GetBoolean(reader.GetOrdinal("train_is_active")),
                        ["tickets_count"] = reader.GetInt32(reader.GetOrdinal("tickets_count")),
                        ["total_revenue"] = reader.GetDecimal(reader.GetOrdinal("total_revenue"))
                    };

                    // Отримання маршруту — можна винести окремо, або включити у View як підзапит
                    var routeInfo = await GetTrainRouteAsync((int)schedule["train_id"]);
                    schedule["route"] = routeInfo.Route;
                    schedule["has_route"] = routeInfo.HasRoute;

                    return schedule;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні деталей розкладу {ScheduleId}", scheduleId);
                return null;
            }
        }


        public async Task<List<Dictionary<string, object>>> GetActiveTrainsAsync()
        {
            var trains = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = "SELECT * FROM active_trains_view ORDER BY train_number";

                using var cmd = new NpgsqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    trains.Add(new Dictionary<string, object>
                    {
                        ["train_id"] = reader.GetInt32(reader.GetOrdinal("train_id")),
                        ["train_number"] = reader.GetInt32(reader.GetOrdinal("train_number")),
                        ["carriage_count"] = reader.GetInt32(reader.GetOrdinal("carriage_count"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні списку активних потягів");
            }

            return trains;
        }


        public async Task<(bool HasRoute, string TrainNumber, List<Dictionary<string, object>> Route)> GetTrainRouteAsync(int trainId)
        {
            var route = new List<Dictionary<string, object>>();
            string trainNumber = "Невідомо";

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Отримуємо номер поїзда (можна теж виносити у функцію при бажанні)
                string getTrainQuery = "SELECT get_train_number(@trainId)";
                using (var trainCmd = new NpgsqlCommand(getTrainQuery, conn))
                {
                    trainCmd.Parameters.AddWithValue("trainId", trainId);
                    var result = await trainCmd.ExecuteScalarAsync();
                    trainNumber = result?.ToString() ?? "Невідомо";
                }

                // Отримуємо маршрут через збережену функцію
                string routeQuery = "SELECT * FROM get_train_route(@trainId)";
                using var cmd = new NpgsqlCommand(routeQuery, conn);
                cmd.Parameters.AddWithValue("trainId", trainId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    route.Add(new Dictionary<string, object>
                    {
                        ["sequence_id"] = reader.GetInt32(reader.GetOrdinal("sequence_id")),
                        ["station_id"] = reader.GetInt32(reader.GetOrdinal("station_id")),
                        ["station_name"] = reader.GetString(reader.GetOrdinal("station_name")),
                        ["travel_duration"] = reader.GetTimeSpan(reader.GetOrdinal("travel_duration")),
                        ["distance_km"] = reader.GetInt32(reader.GetOrdinal("distance_km"))
                    });
                }

                return (route.Count > 0, trainNumber, route);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні маршруту потяга {TrainId}", trainId);
                return (false, trainNumber, route);
            }
        }

        public async Task<List<Dictionary<string, object>>> GetSchedulesWithFiltersAsync(
            int? trainId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool? isActive = null)
        {
            var schedules = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
            SELECT * FROM get_schedules_with_filters(
                @trainId::INTEGER,
                @fromDate::DATE,
                @toDate::DATE,
                @isActive::BOOLEAN
            )", conn);

                cmd.Parameters.AddWithValue("trainId", trainId.HasValue ? trainId.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("fromDate", fromDate.HasValue ? fromDate.Value.Date : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("toDate", toDate.HasValue ? toDate.Value.Date : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("isActive", isActive.HasValue ? isActive.Value : (object)DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    schedules.Add(new Dictionary<string, object>
                    {
                        ["schedule_id"] = reader.GetInt32(reader.GetOrdinal("schedule_id")),
                        ["train_id"] = reader.GetInt32(reader.GetOrdinal("train_id")),
                        ["date"] = reader.GetDateTime(reader.GetOrdinal("date")),
                        ["weekdays"] = reader.GetString(reader.GetOrdinal("weekdays")),
                        ["departure_time"] = reader.GetTimeSpan(reader.GetOrdinal("departure_time")),
                        ["train_number"] = reader.GetInt32(reader.GetOrdinal("train_number")),
                        ["train_is_active"] = reader.GetBoolean(reader.GetOrdinal("train_is_active")),
                        ["tickets_count"] = reader.GetInt32(reader.GetOrdinal("tickets_count"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні розкладів з фільтрами");
            }

            return schedules;
        }

    }
}