using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using train2.Models;
using train4.Services.Base;

namespace train4.Services.Implementation
{
    public class ScheduleService : BaseDatabaseService, Interfaces.IScheduleService
    {
        public ScheduleService(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ScheduleService> logger)
            : base(configuration, httpContextAccessor, logger) { }

        public async Task<List<Schedule>> SearchSchedulesAsync(string fromStation, string toStation, DateTime date)
        {
            var schedules = new List<Schedule>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = "SELECT * FROM get_trains_between_stations(@departure_station, @arrival_station, @travel_date::date)";
                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("departure_station", fromStation);
                cmd.Parameters.AddWithValue("arrival_station", toStation);
                cmd.Parameters.AddWithValue("travel_date", date.Date);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var schedule = new Schedule
                    {
                        train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
                        date = date,
                        weekdays = reader.GetString(reader.GetOrdinal("weekdays")),
                        departure_time = reader.GetTimeSpan(reader.GetOrdinal("adjusted_departure_time")),
                        Train = new Train
                        {
                            train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
                            train_number = reader.GetInt32(reader.GetOrdinal("train_number"))
                        }
                    };

                    schedules.Add(schedule);
                }
            }
            catch (PostgresException pgEx)
            {
                _logger.LogError(pgEx, "PostgreSQL помилка при виконанні функції get_trains_between_stations");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Загальна помилка при пошуку розкладу");
            }

            return schedules;
        }


        // Альтернативний метод, що повертає Dictionary для більшої гнучкості
        public async Task<List<Dictionary<string, object>>> SearchSchedulesDictionaryAsync(string fromStation, string toStation, DateTime date)
        {
            var schedules = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = "SELECT * FROM get_trains_between_stations(@departure_station, @arrival_station, @travel_date)";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("departure_station", fromStation);
                cmd.Parameters.AddWithValue("arrival_station", toStation);
                cmd.Parameters.AddWithValue("travel_date", date.Date);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var schedule = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        schedule[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    schedules.Add(schedule);
                }
            }
            catch (PostgresException pgEx)
            {
                _logger.LogError(pgEx, $"PostgreSQL помилка: {pgEx.MessageText}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Загальна помилка при пошуку розкладу");
            }

            return schedules;
        }

        public async Task<(bool Success, string ErrorMessage)> CreateRouteAsync(int trainId, List<int> stationIds)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("CALL create_route_full(@train_id, VARIADIC @station_ids)", conn);
                cmd.Parameters.AddWithValue("train_id", trainId);
                cmd.Parameters.AddWithValue("station_ids", stationIds.ToArray());

                await cmd.ExecuteNonQueryAsync();
                return (true, null);
            }
            catch (PostgresException pgEx)
            {
                _logger.LogError(pgEx, "Помилка створення маршруту");
                return (false, pgEx.MessageText); // <-- точне повідомлення з RAISE EXCEPTION
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Загальна помилка створення маршруту");
                return (false, ex.Message);
            }
        }

        public async Task<(string TrainNumber, List<StationSequence> Route)> GetRouteByTrainIdAsync(int trainId)
        {
            var route = new List<StationSequence>();
            string trainNumber = "Невідомо";

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string sql = @"
                    SELECT sequence_id, train_id, station_id, travel_duration, distance_km, station_name, train_number
                    FROM train_routes_view 
                    WHERE train_id = @trainId
                    ORDER BY sequence_id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("trainId", trainId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (trainNumber == "Невідомо")
                        trainNumber = reader.GetInt32(reader.GetOrdinal("train_number")).ToString();

                    route.Add(new StationSequence
                    {
                        sequence_id = reader.GetInt32(reader.GetOrdinal("sequence_id")),
                        train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
                        station_id = reader.GetInt32(reader.GetOrdinal("station_id")),
                        travel_duration = reader.GetTimeSpan(reader.GetOrdinal("travel_duration")),
                        distance_km = reader.GetInt32(reader.GetOrdinal("distance_km")),
                        Stations = new Stations
                        {
                            station_id = reader.GetInt32(reader.GetOrdinal("station_id")),
                            station_name = reader.GetString(reader.GetOrdinal("station_name"))
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні маршруту");
            }

            return (trainNumber, route);
        }


        public async Task<int> GetScheduleIdAsync(int trainId, DateTime travelDate)
        {
            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();

            // Виклик SQL-функції, яка повертає schedule_id
            using var cmd = new NpgsqlCommand("SELECT get_schedule_id(@trainId, @travelDate)", conn);
            cmd.Parameters.AddWithValue("trainId", trainId);
            cmd.Parameters.AddWithValue("travelDate", travelDate.Date); // .Date — щоб прибрати час

            var result = await cmd.ExecuteScalarAsync();

            return result != null ? Convert.ToInt32(result) : 0;
        }



        //// ПРИВАТНЫЕ МЕТОДЫ:
        //private async Task<Schedule> GetScheduleByTrainIdAndDateAsync(int trainId, DateTime date, NpgsqlConnection existingConnection = null)
        //{
        //    var shouldCloseConnection = existingConnection == null;

        //    try
        //    {
        //        var conn = existingConnection ?? new NpgsqlConnection(GetCurrentConnectionString());
        //        if (shouldCloseConnection) await conn.OpenAsync();

        //        string query = @"
        //            SELECT s.schedule_id, s.train_id, s.date, s.weekdays, s.departure_time,
        //                   t.train_number, t.carriage_count
        //            FROM schedule s
        //            JOIN train t ON s.train_id = t.train_id
        //            WHERE s.train_id = @trainId 
        //            AND s.date = @date";

        //        using var cmd = new NpgsqlCommand(query, conn);
        //        cmd.Parameters.AddWithValue("trainId", trainId);
        //        cmd.Parameters.AddWithValue("date", date.Date);

        //        using var reader = await cmd.ExecuteReaderAsync();

        //        if (await reader.ReadAsync())
        //        {
        //            var schedule = new Schedule
        //            {
        //                schedule_id = reader.GetInt32(reader.GetOrdinal("schedule_id")),
        //                train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
        //                date = reader.GetDateTime(reader.GetOrdinal("date")),
        //                weekdays = reader.GetString(reader.GetOrdinal("weekdays")),
        //                departure_time = reader.GetTimeSpan(reader.GetOrdinal("departure_time")),
        //                Train = new Train
        //                {
        //                    train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
        //                    train_number = reader.GetInt32(reader.GetOrdinal("train_number")),
        //                    carriage_count = reader.GetInt32(reader.GetOrdinal("carriage_count"))
        //                }
        //            };

        //            return schedule;
        //        }

        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Помилка отримання розкладу за trainId={trainId} і date={date}");
        //        return null;
        //    }
        //    finally
        //    {
        //        if (shouldCloseConnection && existingConnection == null)
        //        {
        //            existingConnection?.Close();
        //        }
        //    }
        //}

        //public async Task<(List<StationSequence>, bool)> GetRouteWithEditPermissionAsync(int trainId)
        //{
        //    var route = new List<StationSequence>();
        //    bool allowFullEdit = true;

        //    try
        //    {
        //        using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //        await conn.OpenAsync();

        //        string sql = "SELECT * FROM get_route_with_edit_permission(@trainId)";
        //        using var cmd = new NpgsqlCommand(sql, conn);
        //        cmd.Parameters.AddWithValue("trainId", trainId);

        //        using var reader = await cmd.ExecuteReaderAsync();
        //        while (await reader.ReadAsync())
        //        {
        //            allowFullEdit = reader.GetBoolean(reader.GetOrdinal("allow_full_edit"));

        //            route.Add(new StationSequence
        //            {
        //                sequence_id = reader.GetInt32(reader.GetOrdinal("sequence_id")),
        //                train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
        //                station_id = reader.GetInt32(reader.GetOrdinal("station_id")),
        //                travel_duration = reader.GetTimeSpan(reader.GetOrdinal("travel_duration")),
        //                distance_km = reader.GetInt32(reader.GetOrdinal("distance_km")),
        //                Stations = new Stations
        //                {
        //                    station_id = reader.GetInt32(reader.GetOrdinal("station_id")),
        //                    station_name = reader.GetString(reader.GetOrdinal("station_name"))
        //                }
        //            });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Помилка при перевірці дозволу на редагування маршруту");
        //    }

        //    return (route, allowFullEdit);
        //}

        // Додайте цей метод до вашого ScheduleService.cs

        // Додайте цей метод до ScheduleService.cs
        public async Task<bool> UpdateRouteDurationsAndPricesAsync(int trainId, List<StationSequence> updatedRoute)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();
                using var transaction = await conn.BeginTransactionAsync();

                // Оновлюємо station_sequence (тривалість та відстань)
                foreach (var station in updatedRoute)
                {
                    string updateStationSql = @"
                UPDATE station_sequence 
                SET travel_duration = @travelDuration, 
                    distance_km = @distanceKm 
                WHERE sequence_id = @sequenceId AND train_id = @trainId";

                    using var cmd = new NpgsqlCommand(updateStationSql, conn, transaction);
                    cmd.Parameters.AddWithValue("travelDuration", station.travel_duration);
                    cmd.Parameters.AddWithValue("distanceKm", station.distance_km);
                    cmd.Parameters.AddWithValue("sequenceId", station.sequence_id);
                    cmd.Parameters.AddWithValue("trainId", trainId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Оновлюємо ціни між станціями
                await UpdateStationPricesAsync(conn, transaction, trainId, updatedRoute);

                await transaction.CommitAsync();
                _logger.LogInformation($"Маршрут та ціни для поїзда {trainId} успішно оновлено");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка при оновленні маршруту для поїзда {trainId}");
                return false;
            }
        }

        private async Task UpdateStationPricesAsync(NpgsqlConnection conn, NpgsqlTransaction transaction, int trainId, List<StationSequence> updatedRoute)
        {
            try
            {
                // Отримуємо повний маршрут поїзда в правильному порядку
                string getRouteSql = @"
            SELECT ss.sequence_id, ss.station_id, ss.travel_duration, ss.distance_km, s.station_name
            FROM station_sequence ss
            JOIN stations s ON ss.station_id = s.station_id
            WHERE ss.train_id = @trainId
            ORDER BY ss.sequence_id";

                var fullRoute = new List<StationSequence>();

                // ВАЖЛИВО: Закриваємо reader перед виконанням інших запитів
                using (var cmd = new NpgsqlCommand(getRouteSql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("trainId", trainId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var station = new StationSequence
                            {
                                sequence_id = reader.GetInt32("sequence_id"),
                                station_id = reader.GetInt32("station_id"),
                                travel_duration = reader.GetTimeSpan(reader.GetOrdinal("travel_duration")),
                                distance_km = reader.GetInt32("distance_km"),
                                Stations = new Stations
                                {
                                    station_id = reader.GetInt32("station_id"),
                                    station_name = reader.GetString("station_name")
                                }
                            };

                            // Знаходимо оновлені дані для цієї станції
                            var updatedStation = updatedRoute.FirstOrDefault(u => u.sequence_id == station.sequence_id);
                            if (updatedStation != null)
                            {
                                station.travel_duration = updatedStation.travel_duration;
                                station.distance_km = updatedStation.distance_km;
                            }

                            fullRoute.Add(station);
                        }
                    } // Reader автоматично закривається тут
                } // Command також закривається

                // Тепер можемо безпечно виконувати інші запити
                // Обчислюємо та оновлюємо ціни між всіма парами станцій
                for (int i = 0; i < fullRoute.Count; i++)
                {
                    for (int j = i + 1; j < fullRoute.Count; j++)
                    {
                        var fromStation = fullRoute[i];
                        var toStation = fullRoute[j];

                        // Обчислюємо загальну відстань та тривалість між станціями
                        decimal totalDistance = 0;
                        TimeSpan totalDuration = TimeSpan.Zero;

                        for (int k = i; k < j; k++)
                        {
                            totalDistance += fullRoute[k].distance_km;
                            totalDuration = totalDuration.Add(fullRoute[k].travel_duration);
                        }

                        // Формула розрахунку ціни: відстань * 0.5 + тривалість_в_хвилинах * 0.3
                        decimal calculatedPrice = (totalDistance * 0.5m) + ((decimal)totalDuration.TotalMinutes * 0.3m);

                        // Округлюємо до 2 знаків після коми
                        calculatedPrice = Math.Round(calculatedPrice, 2);

                        // Оновлюємо або вставляємо ціну в таблицю station_prices
                        await UpsertStationPrice(conn, transaction, fromStation.station_id, toStation.station_id, calculatedPrice);

                        _logger.LogInformation($"Оновлено ціну між станціями {fromStation.Stations.station_name} -> {toStation.Stations.station_name}: {calculatedPrice} грн");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при оновленні цін між станціями");
                throw;
            }
        }

        private async Task UpsertStationPrice(NpgsqlConnection conn, NpgsqlTransaction transaction, int fromStationId, int toStationId, decimal price)
        {
            // Спочатку перевіряємо, чи існує запис
            string checkExistsSql = @"
        SELECT COUNT(*) FROM station_prices 
        WHERE from_sequence_id = @fromStationId AND to_sequence_id = @toStationId";

            bool exists;
            using (var checkCmd = new NpgsqlCommand(checkExistsSql, conn, transaction))
            {
                checkCmd.Parameters.AddWithValue("fromStationId", fromStationId);
                checkCmd.Parameters.AddWithValue("toStationId", toStationId);
                exists = (long)await checkCmd.ExecuteScalarAsync() > 0;
            } // Команда закривається тут

            // Тепер виконуємо UPDATE або INSERT
            string sql;
            if (exists)
            {
                // Оновлюємо існуючий запис
                sql = @"
            UPDATE station_prices 
            SET price = @price, curr_date = CURRENT_DATE 
            WHERE from_sequence_id = @fromStationId AND to_sequence_id = @toStationId";
            }
            else
            {
                // Вставляємо новий запис
                sql = @"
            INSERT INTO station_prices (from_sequence_id, to_sequence_id, price, curr_date) 
            VALUES (@fromStationId, @toStationId, @price, CURRENT_DATE)";
            }

            using (var cmd = new NpgsqlCommand(sql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("fromStationId", fromStationId);
                cmd.Parameters.AddWithValue("toStationId", toStationId);
                cmd.Parameters.AddWithValue("price", price);
                await cmd.ExecuteNonQueryAsync();
            } // Команда закривається тут
        }

        public async Task<(List<StationSequence>, bool)> GetRouteWithEditPermissionAsync(int trainId)
        {
            var route = new List<StationSequence>();
            bool allowFullEdit = true;

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // ВИПРАВЛЕНА назва функції - без 's' в кінці
                string sql = "SELECT * FROM get_route_with_edit_permissions(@trainId)";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("trainId", trainId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    // Перевіряємо чи можна повністю редагувати
                    if (!reader.GetBoolean(reader.GetOrdinal("allow_full_edit")))
                    {
                        allowFullEdit = false;
                    }

                    route.Add(new StationSequence
                    {
                        sequence_id = reader.GetInt32(reader.GetOrdinal("sequence_id")),
                        train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
                        station_id = reader.GetInt32(reader.GetOrdinal("station_id")),
                        travel_duration = reader.GetTimeSpan(reader.GetOrdinal("travel_duration")),
                        distance_km = reader.GetInt32(reader.GetOrdinal("distance_km")),
                        Stations = new Stations
                        {
                            station_id = reader.GetInt32(reader.GetOrdinal("station_id")),
                            station_name = reader.GetString(reader.GetOrdinal("station_name"))
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при перевірці дозволу на редагування маршруту");
            }

            return (route, allowFullEdit);
        }
    }
}
