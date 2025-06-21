using Microsoft.AspNetCore.Http;
using train2.Models;
using Npgsql;
using NpgsqlTypes;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Connections;

namespace train2.Services
{
    public class PostgreSqlDatabaseService : IDatabaseService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<PostgreSqlDatabaseService> _logger;

        private const string DefaultHost = "localhost";
        private const string DefaultDatabase = "TrainTicketDb2";
        private const string DefaultPort = "5432";
        private const string GuestUser = "guest_user";
        private const string GuestPassword = "guest123";

        public PostgreSqlDatabaseService(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<PostgreSqlDatabaseService> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private string GetCurrentConnectionString()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            string login = session?.GetString("db_user") ?? GuestUser;
            string password = session?.GetString("db_password") ?? GuestPassword;

            return $"Host={DefaultHost};Port={DefaultPort};Database={DefaultDatabase};Username={login};Password={password}";
        }

        public async Task<(bool Success, string ErrorMessage)> ConnectAsync(string login, string password, string email = null)
        {
            string connectionString = $"Host={DefaultHost};Port={DefaultPort};Database={DefaultDatabase};Username={login};Password={password}";

            try
            {
                using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                string query = @"
            SELECT COUNT(*) 
            FROM my_client_info 
            WHERE login = @login;";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("login", login);
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                if (count == 0)
                {
                    return (false, "Користувача не знайдено у my_client_info або немає доступу");
                }

                // Зберігаємо дані авторизації в сесію
                var session = _httpContextAccessor.HttpContext?.Session;
                session?.SetString("db_user", login);
                session?.SetString("db_password", password);

                // Отримуємо коротку інформацію про клієнта через представлення
                var client = await GetClientByLoginAsync(login);
                if (client != null)
                {
                    session?.SetString("client_name", client.client_name);
                    session?.SetString("client_surname", client.client_surname);
                    session?.SetInt32("client_id", client.client_id);
                }

                return (true, null);
            }
            catch (NpgsqlException ex)
            {
                _logger.LogError(ex, "Помилка підключення до бази даних");
                return (false, $"Помилка авторизації: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неочікувана помилка при авторизації");
                return (false, "Неочікувана помилка при авторизації");
            }
        }


        public async Task<(string Username, string Role)> GetCurrentUserInfoAsync()
        {
            string username = "Гість";
            string role = "guest";
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Получаем информацию о пользователе через VIEW одним запросом
                using var cmd = new NpgsqlCommand("SELECT username, role FROM current_user_info_view", conn);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    username = reader.GetString("username") ?? "Гість";
                    role = reader.GetString("role") ?? "guest";
                }

                return (username, role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання інформації про користувача через VIEW");
                return ("Гість", "guest");
            }
        }

        public async Task<List<Stations>> GetAllStationsAsync()
        {
            var stations = new List<Stations>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Используем VIEW вместо функции - намного проще!
                using var cmd = new NpgsqlCommand("SELECT station_id, station_name FROM stations_view", conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    stations.Add(new Stations
                    {
                        station_id = reader.GetInt32("station_id"),
                        station_name = reader.GetString("station_name")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання списку станцій через VIEW");
            }
            return stations;
        }


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
        public async Task<(bool Success, string ErrorMessage)> UpdateClientAsync(Client client)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = @"
            UPDATE client 
            SET client_name = @client_name, 
                client_surname = @client_surname, 
                email = @email
            WHERE client_id = @client_id";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("client_id", client.client_id);
                cmd.Parameters.AddWithValue("client_name", client.client_name);
                cmd.Parameters.AddWithValue("client_surname", client.client_surname);
                cmd.Parameters.AddWithValue("email", client.email ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при оновленні даних клієнта");
                return (false, "Помилка при оновленні даних клієнта");
            }
        }

        private async Task<int> GetTrainIdByNumberAsync(int trainNumber, NpgsqlConnection existingConnection = null)
        {
            var shouldCloseConnection = existingConnection == null;

            try
            {
                var conn = existingConnection ?? new NpgsqlConnection(GetCurrentConnectionString());
                if (shouldCloseConnection) await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT train_id FROM train WHERE train_number = @trainNumber", conn);
                cmd.Parameters.AddWithValue("trainNumber", trainNumber);

                var result = await cmd.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка отримання ID потягу за номером {trainNumber}");
                return 0;
            }
            finally
            {
                if (shouldCloseConnection && existingConnection == null)
                {
                    existingConnection?.Close();
                }
            }
        }

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
        public async Task<Client?> GetClientByLoginAsync(string login)
        {
            try
            {
                using var connection = new NpgsqlConnection(GetCurrentConnectionString());
                await connection.OpenAsync();

                string query = @"
                        SELECT client_id, client_name, client_surname, login
                        FROM my_client_info
                        LIMIT 1";

                using var command = new NpgsqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new Client
                    {
                        client_id = reader.GetInt32(reader.GetOrdinal("client_id")),
                        client_name = reader.GetString(reader.GetOrdinal("client_name")),
                        client_surname = reader.GetString(reader.GetOrdinal("client_surname")),
                        login = reader.GetString(reader.GetOrdinal("login")),

                        // Інші поля залишаємо порожніми або null
                        client_patronymic = null,
                        email = null,
                        phone_number = null,
                        payment_info = null
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні клієнта через представлення my_client_info");
                return null;
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                // Очищаємо сесію
                _httpContextAccessor.HttpContext.Session.Clear();

                // Створюємо нове підключення під гостьовим користувачем
                using var conn = new NpgsqlConnection($"Host={DefaultHost};Port={DefaultPort};Database={DefaultDatabase};Username={GuestUser};Password={GuestPassword}");
                await conn.OpenAsync();

                // Виконуємо запит для перевірки з'єднання
                using var cmd = new NpgsqlCommand("SELECT current_user", conn);
                var guestUser = (await cmd.ExecuteScalarAsync())?.ToString();

                _logger.LogInformation($"Вихід з системи. Підключено як {guestUser}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при виході з системи");
            }
        }


        public async Task<List<CarriageTypes>> GetAllCarriageTypesAsync()
        {
            var carriageTypes = new List<CarriageTypes>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = @"SELECT * FROM get_carriage_types()";


                using var cmd = new NpgsqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    carriageTypes.Add(new CarriageTypes
                    {
                        carriage_type_id = reader.GetInt32(reader.GetOrdinal("carriage_type_id")),
                        carriage_type = reader.GetString(reader.GetOrdinal("carriage_type")),
                        seat_count = reader.GetInt32(reader.GetOrdinal("seat_count"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні типів вагонів");
            }

            return carriageTypes;
        }

        public async Task<(bool Success, string ErrorMessage)> CreateTrainWithCarriagesAsync(Train train, int[] carriageTypeIds, int[] carriageNumbers)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("CALL create_train_with_carriages(@trainNumber, @carriageCount, @carriageTypeIds, @carriageNumbers)", conn);

                cmd.Parameters.AddWithValue("trainNumber", train.train_number);
                cmd.Parameters.AddWithValue("carriageCount", train.carriage_count);
                cmd.Parameters.AddWithValue("carriageTypeIds", carriageTypeIds);
                cmd.Parameters.AddWithValue("carriageNumbers", carriageNumbers);

                await cmd.ExecuteNonQueryAsync();

                return (true, null);
            }
            catch (PostgresException pgex)
            {
                _logger.LogWarning(pgex, "Бізнес-помилка при створенні потяга");
                return (false, pgex.MessageText); // Можеш підміняти текст для користувача
            }
            catch (Exception ex)
            {а
                _logger.LogError(ex, "Помилка при створенні потяга");
                return (false, "Внутрішня помилка при створенні потяга");
            }
        }

        public async Task<(Train Train, Dictionary<int, int> SeatCounts)> GetTrainDetailsAsync(int trainId)
        {
            var seatCounts = new Dictionary<int, int>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Основна інформація про потяг
                string trainQuery = @"
            SELECT train_id, train_number, carriage_count
            FROM train
            WHERE train_id = @trainId";

                Train train = null;
                using (var cmd = new NpgsqlCommand(trainQuery, conn))
                {
                    cmd.Parameters.AddWithValue("trainId", trainId);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        train = new Train
                        {
                            train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
                            train_number = reader.GetInt32(reader.GetOrdinal("train_number")),
                            carriage_count = reader.GetInt32(reader.GetOrdinal("carriage_count")),
                            TrainCarriageTypes = new List<TrainCarriageTypes>()
                        };
                    }
                    else
                    {
                        return (null, seatCounts);
                    }
                }

                // Інформація про вагони
                string carriagesQuery = @"
            SELECT tct.train_carriage_types_id, tct.carriage_type_id, tct.carriage_number,
                   ct.carriage_type, ct.seat_count,
                   COUNT(s.seat_id) as actual_seat_count
            FROM train_carriage_types tct
            JOIN carriage_types ct ON tct.carriage_type_id = ct.carriage_type_id
            LEFT JOIN seat s ON tct.train_carriage_types_id = s.train_carriage_types_id
            WHERE tct.train_id = @trainId
            GROUP BY tct.train_carriage_types_id, tct.carriage_type_id, tct.carriage_number, ct.carriage_type, ct.seat_count
            ORDER BY tct.carriage_number";

                using (var cmd = new NpgsqlCommand(carriagesQuery, conn))
                {
                    cmd.Parameters.AddWithValue("trainId", trainId);
                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var carriageTypes = new CarriageTypes
                        {
                            carriage_type_id = reader.GetInt32(reader.GetOrdinal("carriage_type_id")),
                            carriage_type = reader.GetString(reader.GetOrdinal("carriage_type")),
                            seat_count = reader.GetInt32(reader.GetOrdinal("seat_count"))
                        };

                        var trainCarriageType = new TrainCarriageTypes
                        {
                            train_carriage_types_id = reader.GetInt32(reader.GetOrdinal("train_carriage_types_id")),
                            train_id = trainId,
                            carriage_type_id = reader.GetInt32(reader.GetOrdinal("carriage_type_id")),
                            carriage_number = reader.GetInt32(reader.GetOrdinal("carriage_number")),
                            CarriageTypes = carriageTypes
                        };

                        var actualSeatCount = reader.GetInt32(reader.GetOrdinal("actual_seat_count"));

                        seatCounts[trainCarriageType.train_carriage_types_id] = actualSeatCount;

                        train.TrainCarriageTypes.Add(trainCarriageType);
                    }
                }

                return (train, seatCounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка при отриманні деталей потяга {trainId}");
                return (null, seatCounts);
            }
        }

        // Додайте цей метод до класу PostgreSqlDatabaseService

        public async Task<(Train train, List<Dictionary<string, object>> carriages)> GetTrainForEditAsync(int trainId)
        {
            Train train = null;
            var carriages = new List<Dictionary<string, object>>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Використовуємо функцію get_train_for_edit
                string query = "SELECT * FROM get_train_for_edit(@trainId)";
                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("trainId", trainId);

                using var reader = await cmd.ExecuteReaderAsync();

                bool trainInfoSet = false;
                while (await reader.ReadAsync())
                {
                    // Заповнюємо інформацію про потяг (робимо це один раз)
                    if (!trainInfoSet)
                    {
                        train = new Train
                        {
                            train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
                            train_number = reader.GetInt32(reader.GetOrdinal("train_number")),
                            carriage_count = reader.GetInt32(reader.GetOrdinal("carriage_count"))
                        };
                        trainInfoSet = true;
                    }

                    // Додаємо інформацію про вагони (якщо є)
                    if (!reader.IsDBNull(reader.GetOrdinal("carriage_type_id")))
                    {
                        var carriageInfo = new Dictionary<string, object>
                        {
                            ["train_carriage_types_id"] = reader.GetInt32(reader.GetOrdinal("train_carriage_types_id")),
                            ["carriage_type_id"] = reader.GetInt32(reader.GetOrdinal("carriage_type_id")),
                            ["carriage_number"] = reader.GetInt32(reader.GetOrdinal("carriage_number")),
                            ["carriage_type"] = reader.GetString(reader.GetOrdinal("carriage_type")),
                            ["seat_count"] = reader.GetInt32(reader.GetOrdinal("seat_count"))
                        };

                        carriages.Add(carriageInfo);
                    }
                }

                return (train, carriages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка при отриманні потяга для редагування {trainId}");
                return (null, new List<Dictionary<string, object>>());
            }
        }

        // Додайте ці методи до класу PostgreSqlDatabaseService
        // Простий метод UpdateTrainAsync для PostgreSqlDatabaseService

        // ЗАМЕНИТЕ существующий метод UpdateTrainAsync в DatabaseService на этот:

        public async Task<(bool Success, string ErrorMessage)> UpdateTrainAsync(
            int trainId,
            int trainNumber,
            List<Dictionary<string, object>> carriageData)
        {
            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Оновлення потяга: ID={trainId}, Номер={trainNumber}");

                // 1. Проверяем уникальность номера поезда (исключая текущий поезд)
                using (var checkCmd = new NpgsqlCommand(@"
            SELECT COUNT(*) FROM train 
            WHERE train_number = @trainNumber AND train_id != @trainId", conn, transaction))
                {
                    checkCmd.Parameters.AddWithValue("trainNumber", trainNumber);
                    checkCmd.Parameters.AddWithValue("trainId", trainId);

                    var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                    if (count > 0)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Потяг з номером {trainNumber} вже існує");
                    }
                }

                // 2. Обновляем номер поезда
                using (var cmd = new NpgsqlCommand(@"
            UPDATE train SET train_number = @trainNumber WHERE train_id = @trainId", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("trainNumber", trainNumber);
                    cmd.Parameters.AddWithValue("trainId", trainId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 3. Получаем список вагонов с бронированиями (их нельзя изменять)
                var protectedCarriages = new HashSet<int>();
                using (var cmd = new NpgsqlCommand(@"
            SELECT DISTINCT tct.train_carriage_types_id
            FROM train_carriage_types tct
            INNER JOIN seat s ON tct.train_carriage_types_id = s.train_carriage_types_id
            INNER JOIN ticket t ON s.seat_id = t.seat_id
            WHERE tct.train_id = @trainId", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("trainId", trainId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        protectedCarriages.Add(reader.GetInt32(0));
                    }
                }

                // 4. Получаем текущие вагоны
                var currentCarriages = new List<Dictionary<string, object>>();
                using (var cmd = new NpgsqlCommand(@"
            SELECT train_carriage_types_id, carriage_type_id, carriage_number
            FROM train_carriage_types 
            WHERE train_id = @trainId
            ORDER BY carriage_number", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("trainId", trainId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        currentCarriages.Add(new Dictionary<string, object>
                        {
                            ["train_carriage_types_id"] = reader.GetInt32(0),
                            ["carriage_type_id"] = reader.GetInt32(1),
                            ["carriage_number"] = reader.GetInt32(2)
                        });
                    }
                }

                // 5. Обрабатываем вагоны из формы
                var processedCarriageIds = new List<int>();

                foreach (var carriage in carriageData)
                {
                    var carriageTypeId = Convert.ToInt32(carriage["carriage_type_id"]);
                    var carriageNumber = Convert.ToInt32(carriage["carriage_number"]);

                    if (carriage.ContainsKey("train_carriage_types_id") &&
                        Convert.ToInt32(carriage["train_carriage_types_id"]) > 0)
                    {
                        // Существующий вагон
                        var carriageId = Convert.ToInt32(carriage["train_carriage_types_id"]);
                        processedCarriageIds.Add(carriageId);

                        // Проверяем, защищен ли вагон
                        if (protectedCarriages.Contains(carriageId))
                        {
                            _logger.LogInformation($"Пропускаем защищенный вагон ID={carriageId}");
                            continue; // Пропускаем защищенные вагоны
                        }

                        // Обновляем незащищенный вагон
                        using var cmd = new NpgsqlCommand(@"
                    UPDATE train_carriage_types 
                    SET carriage_type_id = @carriageTypeId, carriage_number = @carriageNumber
                    WHERE train_carriage_types_id = @carriageId", conn, transaction);

                        cmd.Parameters.AddWithValue("carriageId", carriageId);
                        cmd.Parameters.AddWithValue("carriageTypeId", carriageTypeId);
                        cmd.Parameters.AddWithValue("carriageNumber", carriageNumber);
                        await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation($"Обновлен вагон ID={carriageId}");
                    }
                    else
                    {
                        // Новый вагон
                        using var cmd = new NpgsqlCommand(@"
                    INSERT INTO train_carriage_types (train_id, carriage_type_id, carriage_number)
                    VALUES (@trainId, @carriageTypeId, @carriageNumber)", conn, transaction);

                        cmd.Parameters.AddWithValue("trainId", trainId);
                        cmd.Parameters.AddWithValue("carriageTypeId", carriageTypeId);
                        cmd.Parameters.AddWithValue("carriageNumber", carriageNumber);
                        await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation($"Добавлен новый вагон №{carriageNumber}");
                    }
                }

                // 6. Удаляем вагоны, которых нет в форме (но только незащищенные)
                foreach (var currentCarriage in currentCarriages)
                {
                    var carriageId = Convert.ToInt32(currentCarriage["train_carriage_types_id"]);

                    // Если вагон защищен, добавляем его в список обработанных
                    if (protectedCarriages.Contains(carriageId))
                    {
                        processedCarriageIds.Add(carriageId);
                        continue;
                    }

                    // Если незащищенный вагон не в новом списке - удаляем
                    if (!processedCarriageIds.Contains(carriageId))
                    {
                        using var cmd = new NpgsqlCommand(@"
                    DELETE FROM train_carriage_types 
                    WHERE train_carriage_types_id = @carriageId", conn, transaction);

                        cmd.Parameters.AddWithValue("carriageId", carriageId);
                        await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation($"Удален вагон ID={carriageId}");
                    }
                }

                // 7. Обновляем количество вагонов
                using (var cmd = new NpgsqlCommand(@"
            UPDATE train SET carriage_count = (
                SELECT COUNT(*) FROM train_carriage_types WHERE train_id = @trainId
            ) WHERE train_id = @trainId", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("trainId", trainId);
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                var protectedCount = protectedCarriages.Count;
                var message = protectedCount > 0
                    ? $"Потяг оновлено. {protectedCount} вагонів з бронюваннями залишились без змін."
                    : "Потяг успішно оновлено.";

                _logger.LogInformation("Потяг успішно оновлено");
                return (true, message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Помилка при оновленні потяга");
                return (false, ex.Message);
            }
        }

        public async Task<List<Dictionary<string, object>>> GetCarriagesWithBookingsAsync(int trainId)
        {
            var carriagesWithBookings = new List<Dictionary<string, object>>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = @"
            SELECT 
                tct.train_carriage_types_id,
                tct.carriage_number,
                ct.carriage_type,
                COUNT(t.ticket_id) as booking_count
            FROM train_carriage_types tct
            INNER JOIN carriage_types ct ON tct.carriage_type_id = ct.carriage_type_id
            INNER JOIN seat s ON tct.train_carriage_types_id = s.train_carriage_types_id
            INNER JOIN ticket t ON s.seat_id = t.seat_id
            WHERE tct.train_id = @trainId
            GROUP BY tct.train_carriage_types_id, tct.carriage_number, ct.carriage_type
            HAVING COUNT(t.ticket_id) > 0
            ORDER BY tct.carriage_number";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("trainId", trainId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var carriageInfo = new Dictionary<string, object>
                    {
                        ["train_carriage_types_id"] = reader.GetInt32("train_carriage_types_id"),
                        ["carriage_number"] = reader.GetInt32("carriage_number"),
                        ["carriage_type"] = reader.GetString("carriage_type"),
                        ["booking_count"] = reader.GetInt32("booking_count")
                    };

                    carriagesWithBookings.Add(carriageInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка при отриманні вагонів з бронюваннями для потяга {trainId}");
            }

            return carriagesWithBookings;
        }

        public async Task<Train?> GetTrainByIdAsync(int trainId)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = @"
            SELECT train_id, train_number, carriage_count, is_active 
            FROM train_details_view 
            WHERE train_id = @trainId";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("trainId", trainId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new Train
                    {
                        train_id = reader.GetInt32("train_id"),
                        train_number = reader.GetInt32("train_number"),
                        carriage_count = reader.GetInt32("carriage_count"),
                        is_active = reader.GetBoolean("is_active")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні поїзда {TrainId}", trainId);
            }
            return null;
        }

        public async Task<(bool CanChange, string Reason, int ActiveTickets)> CanChangeTrainStatusAsync(int trainId)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = "SELECT * FROM can_change_train_status(@trainId)";
                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("trainId", trainId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (
                        reader.GetBoolean("can_change"),
                        reader.GetString("reason"),
                        reader.GetInt32("active_tickets")
                    );
                }

                return (false, "Помилка перевірки", 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка перевірки можливості зміни статусу потяга {trainId}");
                return (false, "Помилка перевірки", 0);
            }
        }

        public async Task<(bool Success, string Message, bool NewStatus)> ToggleTrainStatusAsync(int trainId)
        {
            if (trainId <= 0)
            {
                _logger.LogWarning("Спроба змінити статус потяга з некоректним ID: {trainId}", trainId);
                return (false, "Некоректний ID потяга", false);
            }

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT * FROM toggle_train_status_fn(@trainId)", conn);
                cmd.Parameters.AddWithValue("trainId", trainId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    bool success = reader.GetBoolean(reader.GetOrdinal("p_success"));
                    string message = reader.GetString(reader.GetOrdinal("p_message"));
                    bool newStatus = reader.GetBoolean(reader.GetOrdinal("p_new_status"));

                    return (success, message, newStatus);
                }

                return (false, "Невідома помилка", false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка зміни статусу потяга {trainId}");
                return (false, $"Помилка: {ex.Message}", false);
            }
        }


        // Також оновіть метод GetAllTrainsAsync щоб включити is_active
        public async Task<List<Train>> GetAllTrainsAsync()
        {
            var trains = new List<Train>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Вызываем представление вместо таблицы
                string query = @"
            SELECT t.train_id, t.train_number, t.carriage_count, t.is_active
            FROM trains_view t
            ORDER BY t.train_number";

                using var cmd = new NpgsqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    trains.Add(new Train
                    {
                        train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
                        train_number = reader.GetInt32(reader.GetOrdinal("train_number")),
                        carriage_count = reader.GetInt32(reader.GetOrdinal("carriage_count")),
                        is_active = reader.GetBoolean(reader.GetOrdinal("is_active"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні списку потягів");
            }
            return trains;
        }

        public async Task<(string TrainNumber, List<StationSequence> Route)> GetRouteByTrainIdAsync(int trainId)
        {
            var result = new List<StationSequence>();
            string trainNumber = "Невідомо";
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // 1. Отримуємо номер поїзда через VIEW
                string getTrainNumberQuery = "SELECT train_number FROM trains_view WHERE train_id = @trainId";
                using (var cmdTrain = new NpgsqlCommand(getTrainNumberQuery, conn))
                {
                    cmdTrain.Parameters.AddWithValue("trainId", trainId);
                    var res = await cmdTrain.ExecuteScalarAsync();
                    trainNumber = res?.ToString() ?? "Невідомо";
                }

                // 2. Отримуємо маршрут через VIEW
                string query = @"
            SELECT sequence_id, train_id, station_id, travel_duration, distance_km, station_name
            FROM train_routes_view 
            WHERE train_id = @trainId
            ORDER BY sequence_id";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("trainId", trainId);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(new StationSequence
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

                return (trainNumber, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні маршруту");
                return (trainNumber, result);
            }
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



        // Замініть метод CreateClientAsync в PostgreSqlDatabaseService

        public async Task<(bool Success, string ErrorMessage)> CreateClientAsync(Client client, string password)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = "SELECT register_client_account(@name, @surname, @patronymic, @mail, @phone, @login, @password, @payment_info)";
                using var cmd = new NpgsqlCommand(query, conn);

                cmd.Parameters.AddWithValue("name", client.client_name);
                cmd.Parameters.AddWithValue("surname", client.client_surname);
                cmd.Parameters.AddWithValue("patronymic", client.client_patronymic);
                cmd.Parameters.AddWithValue("mail", (object?)client.email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("phone", (object?)client.phone_number ?? DBNull.Value);
                cmd.Parameters.AddWithValue("login", client.login);
                cmd.Parameters.AddWithValue("password", password);
                cmd.Parameters.AddWithValue("payment_info", (object?)client.payment_info ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                return (true, null);
            }
            catch (PostgresException pgEx)
            {
                _logger.LogError(pgEx, "PostgreSQL помилка при створенні клієнта через register_client_account");
                return (false, pgEx.MessageText ?? pgEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Загальна помилка при реєстрації клієнта");
                return (false, "Неочікувана помилка при реєстрації");
            }
        }

        // Обновленный метод для получения мест с учетом конкретного маршрута
        public async Task<List<Seat>> GetAvailableSeatsAsync(int trainId, DateTime travelDate, int fromStationId, int toStationId)
        {
            var seats = new List<Seat>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Используем новую функцию с учетом маршрута
                string query = @"SELECT seat_id, seat_number, train_carriage_types_id 
                FROM get_seats_with_statusss(@train_id, @travel_date::DATE, @from_station_id, @to_station_id) 
                WHERE status = 'available'";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("train_id", trainId);
                cmd.Parameters.AddWithValue("travel_date", travelDate.Date);
                cmd.Parameters.AddWithValue("from_station_id", fromStationId);
                cmd.Parameters.AddWithValue("to_station_id", toStationId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var seat = new Seat
                    {
                        seat_id = reader.GetInt32("seat_id"),
                        seat_number = reader.GetInt32("seat_number"),
                        train_carriage_types_id = reader.GetInt32("train_carriage_types_id")
                    };

                    seats.Add(seat);
                }
            }
            catch (PostgresException pgEx)
            {
                _logger.LogError(pgEx, $"PostgreSQL помилка при отриманні місць: {pgEx.MessageText}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Загальна помилка при отриманні місць");
            }

            return seats;
        }

        // Новый метод для получения статусов всех мест с учетом маршрута
        public async Task<Dictionary<int, string>> GetSeatStatusesAsync(int trainId, DateTime travelDate, int fromStationId, int toStationId)
        {
            var seatStatuses = new Dictionary<int, string>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Получаем статусы всех мест для конкретного маршрута
                string query = "SELECT seat_id, status FROM get_seats_with_statusss(@train_id, @travel_date::DATE, @from_station_id, @to_station_id)";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("train_id", trainId);
                cmd.Parameters.AddWithValue("travel_date", travelDate.Date);
                cmd.Parameters.AddWithValue("from_station_id", fromStationId);
                cmd.Parameters.AddWithValue("to_station_id", toStationId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int seatId = reader.GetInt32("seat_id");
                    string status = reader.GetString("status");
                    seatStatuses[seatId] = status;
                }
            }
            catch (PostgresException pgEx)
            {
                _logger.LogError(pgEx, $"PostgreSQL помилка при отриманні статусів місць: {pgEx.MessageText}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Загальна помилка при отриманні статусів місць");
            }

            return seatStatuses;
        }

        public async Task<List<Dictionary<string, object>>> GetSeatsWithDetailsAsync(int trainId, DateTime travelDate, int fromStationId, int toStationId)
        {
            var seatsData = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Получаем все данные о местах с правильными статусами для маршрута
                string query = "SELECT * FROM get_seats_with_statusss(@train_id, @travel_date::DATE, @from_station_id, @to_station_id)";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("train_id", trainId);
                cmd.Parameters.AddWithValue("travel_date", travelDate.Date);
                cmd.Parameters.AddWithValue("from_station_id", fromStationId);
                cmd.Parameters.AddWithValue("to_station_id", toStationId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var seatData = new Dictionary<string, object>
                    {
                        ["seat_id"] = reader.GetInt32("seat_id"),
                        ["seat_number"] = reader.GetInt32("seat_number"),
                        ["train_carriage_types_id"] = reader.GetInt32("train_carriage_types_id"),
                        ["carriage_number"] = reader.GetInt32("carriage_number"),
                        ["carriage_type"] = reader.GetString("carriage_type"),
                        ["status"] = reader.GetString("status") // Теперь поддерживает: available, occupied, reserved, booked
                    };

                    seatsData.Add(seatData);
                }
            }
            catch (PostgresException pgEx)
            {
                _logger.LogError(pgEx, $"PostgreSQL помилка при отриманні місць: {pgEx.MessageText}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Загальна помилка при отриманні місць");
            }

            return seatsData;
        }
        // Дополнительный метод для получения ID станций по их названиям
        public async Task<int?> GetStationIdByNameAsync(string stationName)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = "SELECT station_id FROM stations WHERE station_name = @station_name";
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
        public async Task<Dictionary<string, object>> GetTicketInfoAsync(int trainId, DateTime date, int seatId, int fromStationId, int toStationId)
        {
            var result = new Dictionary<string, object>();

            try
            {
                // Вивід у консоль
                Console.WriteLine("----- DEBUG: Значення параметрів -----");
                Console.WriteLine("Train ID: " + trainId);
                Console.WriteLine("Date: " + date.ToString("yyyy-MM-dd"));
                Console.WriteLine("Seat ID: " + seatId);
                Console.WriteLine("From Station ID: " + fromStationId);
                Console.WriteLine("To Station ID: " + toStationId);
                Console.WriteLine("--------------------------------------");

                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                var cmd = new NpgsqlCommand("SELECT * FROM get_ticket_info(@train_id, @date::date, @seat_id, @from_station_id, @to_station_id)", conn);
                cmd.Parameters.AddWithValue("train_id", trainId);
                cmd.Parameters.AddWithValue("date", date.Date);
                cmd.Parameters.AddWithValue("seat_id", seatId);
                cmd.Parameters.AddWithValue("from_station_id", fromStationId);
                cmd.Parameters.AddWithValue("to_station_id", toStationId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result["from_station_name"] = reader.GetString(0);
                    result["to_station_name"] = reader.GetString(1);
                    result["departure_time"] = reader.GetTimeSpan(2).ToString(@"hh\:mm");
                    result["arrival_time"] = reader.GetTimeSpan(3).ToString(@"hh\:mm");
                    result["carriage_number"] = reader.GetInt32(4);
                    result["seat_number"] = reader.GetInt32(5);
                    result["total_price"] = reader.GetDecimal(6).ToString("F2");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Помилка при виклику get_ticket_info: " + ex.Message);
            }

            return result;
        }


        //public async Task<Dictionary<string, object>> GetTicketInfoAsync(int trainId, DateTime date, int seatId, int fromStationId, int toStationId)
        //{
        //    var result = new Dictionary<string, object>();

        //    try
        //    {
        //        using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //        await conn.OpenAsync();

        //        var cmd = new NpgsqlCommand("SELECT * FROM get_ticket_info(@train_id, @date::date, @seat_id, @from_station_id, @to_station_id)", conn);
        //        cmd.Parameters.AddWithValue("train_id", trainId);
        //        cmd.Parameters.AddWithValue("date", date.Date);
        //        cmd.Parameters.AddWithValue("seat_id", seatId);
        //        cmd.Parameters.AddWithValue("from_station_id", fromStationId);
        //        cmd.Parameters.AddWithValue("to_station_id", toStationId);

        //        using var reader = await cmd.ExecuteReaderAsync();
        //        if (await reader.ReadAsync())
        //        {
        //            result["from_station_name"] = reader.GetString(0);
        //            result["to_station_name"] = reader.GetString(1);
        //            result["departure_time"] = reader.GetTimeSpan(2).ToString(@"hh\:mm");
        //            result["arrival_time"] = reader.GetTimeSpan(3).ToString(@"hh\:mm");
        //            result["carriage_number"] = reader.GetInt32(4);
        //            result["seat_number"] = reader.GetInt32(5);
        //            result["total_price"] = reader.GetDecimal(6).ToString("F2");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger?.LogError(ex, "Помилка під час виклику get_ticket_info");
        //    }

        //    return result;
        //}

        public async Task<Dictionary<string, object>> CreateTicketBookingAsync(int clientId, int seatId, int trainId, DateTime travelDate, int fromStationId, int toStationId)
        {
            var result = new Dictionary<string, object>();

            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // Получаем schedule_id для этого поезда и даты
                using var scheduleCmd = new NpgsqlCommand(@"
            SELECT schedule_id 
            FROM schedule 
            WHERE train_id = @trainId AND date = @travelDate::DATE
            LIMIT 1", conn, transaction);

                scheduleCmd.Parameters.AddWithValue("trainId", trainId);
                scheduleCmd.Parameters.AddWithValue("travelDate", travelDate.Date);

                var scheduleResult = await scheduleCmd.ExecuteScalarAsync();
                if (scheduleResult == null)
                {
                    await transaction.RollbackAsync();
                    result["success"] = false;
                    result["message"] = "Розклад для цього поїзда не знайдено";
                    return result;
                }

                var scheduleId = Convert.ToInt32(scheduleResult);

                // Проверяем, что место свободно для данного маршрута
                using var checkCmd = new NpgsqlCommand(@"
            SELECT status 
            FROM get_seats_with_statusss(@trainId, @travelDate::DATE, @fromStationId, @toStationId) 
            WHERE seat_id = @seatId", conn, transaction);

                checkCmd.Parameters.AddWithValue("trainId", trainId);
                checkCmd.Parameters.AddWithValue("travelDate", travelDate.Date);
                checkCmd.Parameters.AddWithValue("fromStationId", fromStationId);
                checkCmd.Parameters.AddWithValue("toStationId", toStationId);
                checkCmd.Parameters.AddWithValue("seatId", seatId);

                var seatStatus = await checkCmd.ExecuteScalarAsync() as string;

                if (seatStatus != "available")
                {
                    await transaction.RollbackAsync();
                    result["success"] = false;
                    result["message"] = "Це місце вже зайняте або недоступне";
                    return result;
                }

                // Получаем информацию о билете и цене
                using var ticketInfoCmd = new NpgsqlCommand(@"
            SELECT * FROM get_ticket_info(@trainId, @travelDate::DATE, @seatId, @fromStationId, @toStationId)", conn, transaction);

                ticketInfoCmd.Parameters.AddWithValue("trainId", trainId);
                ticketInfoCmd.Parameters.AddWithValue("travelDate", travelDate.Date);
                ticketInfoCmd.Parameters.AddWithValue("seatId", seatId);
                ticketInfoCmd.Parameters.AddWithValue("fromStationId", fromStationId);
                ticketInfoCmd.Parameters.AddWithValue("toStationId", toStationId);

                decimal totalPrice = 0;
                using (var reader = await ticketInfoCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        totalPrice = reader.GetDecimal("total_price");
                    }
                    else
                    {
                        await transaction.RollbackAsync();
                        result["success"] = false;
                        result["message"] = "Не вдається розрахувати вартість квитка";
                        return result;
                    }
                }

                // Создаем билет
                using var createTicketCmd = new NpgsqlCommand(@"
            INSERT INTO ticket (seat_id, client_id, schedule_id, from_sequence_id, to_sequence_id, total_price, booking_date)
            VALUES (@seatId, @clientId, @scheduleId, @fromStationId, @toStationId, @totalPrice, @bookingDate)
            RETURNING ticket_id", conn, transaction);

                createTicketCmd.Parameters.AddWithValue("seatId", seatId);
                createTicketCmd.Parameters.AddWithValue("clientId", clientId);
                createTicketCmd.Parameters.AddWithValue("scheduleId", scheduleId);
                createTicketCmd.Parameters.AddWithValue("fromStationId", fromStationId);
                createTicketCmd.Parameters.AddWithValue("toStationId", toStationId);
                createTicketCmd.Parameters.AddWithValue("totalPrice", totalPrice);
                createTicketCmd.Parameters.AddWithValue("bookingDate", DateTime.Now.Date);

                var ticketId = await createTicketCmd.ExecuteScalarAsync();

                if (ticketId != null)
                {
                    await transaction.CommitAsync();

                    result["success"] = true;
                    result["message"] = "Квиток успішно заброньовано";
                    result["ticket_id"] = Convert.ToInt32(ticketId);
                    result["total_price"] = totalPrice;

                    _logger.LogInformation("Менеджер створив квиток {TicketId} для клієнта {ClientId}", ticketId, clientId);
                }
                else
                {
                    await transaction.RollbackAsync();
                    result["success"] = false;
                    result["message"] = "Помилка при створенні квитка";
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Помилка при створенні квитка через менеджера");

                result["success"] = false;
                result["message"] = "Помилка сервера при бронюванні квитка";
            }

            return result;
        }



        public async Task<bool> BookSeatAsync(int seatId, int clientId, DateTime travelDate)
        {
            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();

            using var transaction = await conn.BeginTransactionAsync();
            try
            {
                // Отримуємо schedule_id для цього поїзда та дати
                using var scheduleCmd = new NpgsqlCommand(@"
            SELECT s.schedule_id 
            FROM schedule s
            INNER JOIN seat seat_table ON seat_table.seat_id = @seatId
            INNER JOIN train_carriage_types tct ON seat_table.train_carriage_types_id = tct.train_carriage_types_id
            WHERE s.train_id = tct.train_id AND s.date = @travelDate::DATE
            LIMIT 1", conn, transaction);

                scheduleCmd.Parameters.AddWithValue("seatId", seatId);
                scheduleCmd.Parameters.AddWithValue("travelDate", travelDate.Date);

                var scheduleResult = await scheduleCmd.ExecuteScalarAsync();
                if (scheduleResult == null)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                var scheduleId = Convert.ToInt32(scheduleResult);

                // Перевіряємо, чи місце не зайняте
                using var checkCmd = new NpgsqlCommand(@"
            SELECT COUNT(*) FROM ticket 
            WHERE seat_id = @seatId AND schedule_id = @scheduleId", conn, transaction);

                checkCmd.Parameters.AddWithValue("seatId", seatId);
                checkCmd.Parameters.AddWithValue("scheduleId", scheduleId);

                var existingBookings = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (existingBookings > 0)
                {
                    await transaction.RollbackAsync();
                    return false; // Місце вже заброньовано
                }

                // Бронюємо місце
                using var bookCmd = new NpgsqlCommand(@"
            INSERT INTO ticket (seat_id, client_id, schedule_id, booking_date)
            VALUES (@seatId, @clientId, @scheduleId, @bookingDate::DATE)", conn, transaction);

                bookCmd.Parameters.AddWithValue("seatId", seatId);
                bookCmd.Parameters.AddWithValue("clientId", clientId);
                bookCmd.Parameters.AddWithValue("scheduleId", scheduleId);
                bookCmd.Parameters.AddWithValue("bookingDate", DateTime.Now.Date);

                var result = await bookCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Помилка при бронюванні місця {SeatId} для клієнта {ClientId}", seatId, clientId);
                return false;
            }
        }

        /// <summary>
        /// Отримання ID розкладу за ID поїзда та датою
        /// </summary>
        public async Task<int> GetScheduleIdAsync(int trainId, DateTime travelDate)
        {
            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
        SELECT schedule_id FROM schedule 
        WHERE train_id = @trainId AND date = @travelDate::DATE 
        LIMIT 1", conn);

            cmd.Parameters.AddWithValue("trainId", trainId);
            cmd.Parameters.AddWithValue("travelDate", travelDate.Date);

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }





        // Добавить эти методы в класс PostgreSqlDatabaseService

        /// <summary>
        /// Поиск клиентов по имени, фамилии или логину
        /// </summary>
        public async Task<List<Client>> SearchClientsAsync(string query)
        {
            var clients = new List<Client>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string sqlQuery = @"
            SELECT client_id, client_name, client_surname, client_patronymic, login, email, phone_number
            FROM client 
            WHERE LOWER(client_name) LIKE LOWER(@query) 
               OR LOWER(client_surname) LIKE LOWER(@query) 
               OR LOWER(client_patronymic) LIKE LOWER(@query)
               OR LOWER(login) LIKE LOWER(@query)
            ORDER BY client_surname, client_name
            LIMIT 20";

                using var cmd = new NpgsqlCommand(sqlQuery, conn);
                cmd.Parameters.AddWithValue("query", $"%{query}%");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    clients.Add(new Client
                    {
                        client_id = reader.GetInt32("client_id"),
                        client_name = reader.GetString("client_name"),
                        client_surname = reader.GetString("client_surname"),
                        client_patronymic = reader.GetString("client_patronymic"),
                        login = reader.GetString("login"),
                        email = reader.IsDBNull("email") ? null : reader.GetString("email"),
                        phone_number = reader.IsDBNull("phone_number") ? null : reader.GetString("phone_number")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при пошуку клієнтів за запитом: {Query}", query);
            }

            return clients;
        }

        /// <summary>
        /// Поиск билетов по клиенту для управления
        /// </summary>
        public async Task<List<Dictionary<string, object>>> SearchTicketsByClientAsync(string clientQuery)
        {
            var tickets = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string sqlQuery = @"
            SELECT 
                t.ticket_id,
                t.booking_date,
                t.total_price,
                c.client_id,
                CONCAT(c.client_name, ' ', c.client_surname, ' ', c.client_patronymic) as client_full_name,
                c.login as client_login,
                tr.train_number,
                s.date as travel_date,
                s.departure_time,
                st_from.station_name as from_station,
                st_to.station_name as to_station,
                seat.seat_number,
                tct.carriage_number,
                ct.carriage_type,
                CASE 
                    WHEN trans.transaction_id IS NOT NULL THEN 'paid'
                    ELSE 'booked'
                END as status
            FROM ticket t
            INNER JOIN client c ON t.client_id = c.client_id
            INNER JOIN schedule s ON t.schedule_id = s.schedule_id
            INNER JOIN train tr ON s.train_id = tr.train_id
            INNER JOIN seat ON t.seat_id = seat.seat_id
            INNER JOIN train_carriage_types tct ON seat.train_carriage_types_id = tct.train_carriage_types_id
            INNER JOIN carriage_types ct ON tct.carriage_type_id = ct.carriage_type_id
            INNER JOIN stations st_from ON t.from_sequence_id = st_from.station_id
            INNER JOIN stations st_to ON t.to_sequence_id = st_to.station_id
            LEFT JOIN transactions trans ON t.ticket_id = trans.ticket_id
            WHERE (LOWER(c.client_name) LIKE LOWER(@query) 
                   OR LOWER(c.client_surname) LIKE LOWER(@query) 
                   OR LOWER(c.client_patronymic) LIKE LOWER(@query)
                   OR LOWER(c.login) LIKE LOWER(@query))
            ORDER BY t.booking_date DESC, t.ticket_id DESC
            LIMIT 50";

                using var cmd = new NpgsqlCommand(sqlQuery, conn);
                cmd.Parameters.AddWithValue("query", $"%{clientQuery}%");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var ticket = new Dictionary<string, object>
                    {
                        ["ticket_id"] = reader.GetInt32("ticket_id"),
                        ["booking_date"] = reader.GetDateTime("booking_date"),
                        ["total_price"] = reader.GetDecimal("total_price"),
                        ["client_id"] = reader.GetInt32("client_id"),
                        ["client_full_name"] = reader.GetString("client_full_name"),
                        ["client_login"] = reader.GetString("client_login"),
                        ["train_number"] = reader.GetInt32("train_number"),
                        ["travel_date"] = reader.GetDateTime("travel_date"),
                        ["departure_time"] = reader.GetTimeSpan(reader.GetOrdinal("departure_time")),
                        ["from_station"] = reader.GetString("from_station"),
                        ["to_station"] = reader.GetString("to_station"),
                        ["seat_number"] = reader.GetInt32("seat_number"),
                        ["carriage_number"] = reader.GetInt32("carriage_number"),
                        ["carriage_type"] = reader.GetString("carriage_type"),
                        ["status"] = reader.GetString("status")
                    };

                    tickets.Add(ticket);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при пошуку квитків для клієнта: {Query}", clientQuery);
            }

            return tickets;
        }

        /// <summary>
        /// Отмена билета
        /// </summary>
        public async Task<Dictionary<string, object>> CancelTicketAsync(int ticketId)
        {
            var result = new Dictionary<string, object>();

            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // Проверяем существование билета и его статус
                string checkQuery = @"
            SELECT 
                t.ticket_id,
                t.client_id,
                s.date as travel_date,
                CASE 
                    WHEN trans.transaction_id IS NOT NULL THEN 'paid'
                    ELSE 'booked'
                END as status,
                CONCAT(c.client_name, ' ', c.client_surname) as client_name,
                tr.train_number,
                st_from.station_name as from_station,
                st_to.station_name as to_station
            FROM ticket t
            INNER JOIN schedule s ON t.schedule_id = s.schedule_id
            INNER JOIN train tr ON s.train_id = tr.train_id
            INNER JOIN client c ON t.client_id = c.client_id
            INNER JOIN stations st_from ON t.from_sequence_id = st_from.station_id
            INNER JOIN stations st_to ON t.to_sequence_id = st_to.station_id
            LEFT JOIN transactions trans ON t.ticket_id = trans.ticket_id
            WHERE t.ticket_id = @ticketId";

                Dictionary<string, object> ticketInfo = null;
                using (var checkCmd = new NpgsqlCommand(checkQuery, conn, transaction))
                {
                    checkCmd.Parameters.AddWithValue("ticketId", ticketId);
                    using var reader = await checkCmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        ticketInfo = new Dictionary<string, object>
                        {
                            ["ticket_id"] = reader.GetInt32("ticket_id"),
                            ["client_id"] = reader.GetInt32("client_id"),
                            ["travel_date"] = reader.GetDateTime("travel_date"),
                            ["status"] = reader.GetString("status"),
                            ["client_name"] = reader.GetString("client_name"),
                            ["train_number"] = reader.GetInt32("train_number"),
                            ["from_station"] = reader.GetString("from_station"),
                            ["to_station"] = reader.GetString("to_station")
                        };
                    }
                }

                if (ticketInfo == null)
                {
                    result["success"] = false;
                    result["message"] = "Квиток не знайдено";
                    return result;
                }

                // Проверяем, не прошла ли дата поездки
                var travelDate = (DateTime)ticketInfo["travel_date"];
                if (travelDate <= DateTime.Today)
                {
                    result["success"] = false;
                    result["message"] = "Неможливо скасувати квиток на минулу або сьогоднішню дату";
                    return result;
                }

                var status = ticketInfo["status"].ToString();

                // Удаляем транзакцию если билет оплачен
                if (status == "paid")
                {
                    using var deleteTransCmd = new NpgsqlCommand(@"
                DELETE FROM transactions WHERE ticket_id = @ticketId", conn, transaction);
                    deleteTransCmd.Parameters.AddWithValue("ticketId", ticketId);
                    await deleteTransCmd.ExecuteNonQueryAsync();
                }

                // Удаляем сам билет
                using var deleteTicketCmd = new NpgsqlCommand(@"
            DELETE FROM ticket WHERE ticket_id = @ticketId", conn, transaction);
                deleteTicketCmd.Parameters.AddWithValue("ticketId", ticketId);

                var deletedRows = await deleteTicketCmd.ExecuteNonQueryAsync();

                if (deletedRows > 0)
                {
                    await transaction.CommitAsync();

                    result["success"] = true;
                    result["message"] = $"Квиток #{ticketId} успішно скасовано";
                    result["ticket_info"] = ticketInfo;

                    _logger.LogInformation("Квиток {TicketId} скасовано менеджером для клієнта {ClientName}",
                        ticketId, ticketInfo["client_name"]);
                }
                else
                {
                    await transaction.RollbackAsync();
                    result["success"] = false;
                    result["message"] = "Помилка при скасуванні квитка";
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Помилка при скасуванні квитка {TicketId}", ticketId);

                result["success"] = false;
                result["message"] = "Помилка сервера при скасуванні квитка";
            }

            return result;
        }















        // Добавить эти методы в класс PostgreSqlDatabaseService

        /// <summary>
        /// Получение неоплаченных билетов с пагинацией
        /// </summary>
        /// <summary>
        /// Получение неоплаченных билетов с пагинацией
        /// </summary>
        public async Task<List<Dictionary<string, object>>> GetUnpaidTicketsAsync(int page, int pageSize, string searchQuery = "")
        {
            var tickets = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string whereClause = "";
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    whereClause = @"AND (LOWER(CONCAT(c.client_name, ' ', c.client_surname)) LIKE LOWER(@searchQuery) 
                   OR LOWER(c.login) LIKE LOWER(@searchQuery)
                   OR tr.train_number::text LIKE @searchQuery
                   OR t.ticket_id::text LIKE @searchQuery)";
                }

                string sqlQuery = $@"
SELECT 
    t.ticket_id,
    t.booking_date,
    t.total_price,
    CONCAT(c.client_name, ' ', c.client_surname, ' ', c.client_patronymic) as client_full_name,
    c.login as client_login,
    c.phone_number,
    c.email,
    tr.train_number,
    s.date as travel_date,
    s.departure_time,
    st_from.station_name as from_station,
    st_to.station_name as to_station,
    seat.seat_number,
    tct.carriage_number,
    ct.carriage_type,
    (CURRENT_DATE - t.booking_date::date) as days_since_booking
FROM ticket t
INNER JOIN client c ON t.client_id = c.client_id
INNER JOIN schedule s ON t.schedule_id = s.schedule_id
INNER JOIN train tr ON s.train_id = tr.train_id
INNER JOIN seat ON t.seat_id = seat.seat_id
INNER JOIN train_carriage_types tct ON seat.train_carriage_types_id = tct.train_carriage_types_id
INNER JOIN carriage_types ct ON tct.carriage_type_id = ct.carriage_type_id
INNER JOIN stations st_from ON t.from_sequence_id = st_from.station_id
INNER JOIN stations st_to ON t.to_sequence_id = st_to.station_id
LEFT JOIN transactions trans ON t.ticket_id = trans.ticket_id
WHERE trans.transaction_id IS NULL 
AND s.date >= CURRENT_DATE
{whereClause}
ORDER BY t.booking_date DESC, t.ticket_id DESC
LIMIT @pageSize OFFSET @offset";

                using var cmd = new NpgsqlCommand(sqlQuery, conn);
                cmd.Parameters.AddWithValue("pageSize", pageSize);
                cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    cmd.Parameters.AddWithValue("searchQuery", $"%{searchQuery}%");
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var ticket = new Dictionary<string, object>
                    {
                        ["ticket_id"] = reader.GetInt32("ticket_id"),
                        ["booking_date"] = reader.GetDateTime("booking_date"),
                        ["total_price"] = reader.GetDecimal("total_price"),
                        ["client_full_name"] = reader.GetString("client_full_name"),
                        ["client_login"] = reader.GetString("client_login"),
                        ["client_phone"] = reader.IsDBNull("phone_number") ? "" : reader.GetString("phone_number"),
                        ["client_email"] = reader.IsDBNull("email") ? "" : reader.GetString("email"),
                        ["train_number"] = reader.GetInt32("train_number"),
                        ["travel_date"] = reader.GetDateTime("travel_date"),
                        ["departure_time"] = reader.GetTimeSpan(reader.GetOrdinal("departure_time")),
                        ["from_station"] = reader.GetString("from_station"),
                        ["to_station"] = reader.GetString("to_station"),
                        ["seat_number"] = reader.GetInt32("seat_number"),
                        ["carriage_number"] = reader.GetInt32("carriage_number"),
                        ["carriage_type"] = reader.GetString("carriage_type"),
                        ["days_since_booking"] = reader.GetInt32("days_since_booking")
                    };

                    tickets.Add(ticket);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні неоплачених квитків");
            }

            return tickets;
        }

        /// <summary>
        /// Получение количества неоплаченных билетов
        /// </summary>
        public async Task<int> GetUnpaidTicketsCountAsync(string searchQuery = "")
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string whereClause = "";
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    whereClause = @"AND (LOWER(CONCAT(c.client_name, ' ', c.client_surname)) LIKE LOWER(@searchQuery) 
                           OR LOWER(c.login) LIKE LOWER(@searchQuery)
                           OR tr.train_number::text LIKE @searchQuery
                           OR t.ticket_id::text LIKE @searchQuery)";
                }

                string sqlQuery = $@"
        SELECT COUNT(*)
        FROM ticket t
        INNER JOIN client c ON t.client_id = c.client_id
        INNER JOIN schedule s ON t.schedule_id = s.schedule_id
        INNER JOIN train tr ON s.train_id = tr.train_id
        LEFT JOIN transactions trans ON t.ticket_id = trans.ticket_id
        WHERE trans.transaction_id IS NULL 
        AND s.date >= CURRENT_DATE
        {whereClause}";

                using var cmd = new NpgsqlCommand(sqlQuery, conn);

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    cmd.Parameters.AddWithValue("searchQuery", $"%{searchQuery}%");
                }

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при підрахунку неоплачених квитків");
                return 0;
            }
        }

        /// <summary>
        /// Подтверждение оплаты билета
        /// </summary>
        public async Task<Dictionary<string, object>> ConfirmTicketPaymentAsync(int ticketId, decimal paidAmount, string paymentMethod)
        {
            var result = new Dictionary<string, object>();

            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // Проверяем существование билета и его статус
                string checkQuery = @"
        SELECT 
            t.ticket_id,
            t.total_price,
            t.client_id,
            CONCAT(c.client_name, ' ', c.client_surname) as client_name,
            tr.train_number,
            s.date as travel_date,
            CASE 
                WHEN trans.transaction_id IS NOT NULL THEN true
                ELSE false
            END as is_paid
        FROM ticket t
        INNER JOIN client c ON t.client_id = c.client_id
        INNER JOIN schedule s ON t.schedule_id = s.schedule_id
        INNER JOIN train tr ON s.train_id = tr.train_id
        LEFT JOIN transactions trans ON t.ticket_id = trans.ticket_id
        WHERE t.ticket_id = @ticketId";

                Dictionary<string, object> ticketInfo = null;
                using (var checkCmd = new NpgsqlCommand(checkQuery, conn, transaction))
                {
                    checkCmd.Parameters.AddWithValue("ticketId", ticketId);
                    using var reader = await checkCmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        ticketInfo = new Dictionary<string, object>
                        {
                            ["ticket_id"] = reader.GetInt32("ticket_id"),
                            ["total_price"] = reader.GetDecimal("total_price"),
                            ["client_id"] = reader.GetInt32("client_id"),
                            ["client_name"] = reader.GetString("client_name"),
                            ["train_number"] = reader.GetInt32("train_number"),
                            ["travel_date"] = reader.GetDateTime("travel_date"),
                            ["is_paid"] = reader.GetBoolean("is_paid")
                        };
                    }
                }

                if (ticketInfo == null)
                {
                    result["success"] = false;
                    result["message"] = "Квиток не знайдено";
                    return result;
                }

                if ((bool)ticketInfo["is_paid"])
                {
                    result["success"] = false;
                    result["message"] = "Квиток вже оплачений";
                    return result;
                }

                var expectedPrice = (decimal)ticketInfo["total_price"];
                if (paidAmount < expectedPrice)
                {
                    result["success"] = false;
                    result["message"] = $"Недостатня сума оплати. Очікується: {expectedPrice} грн";
                    return result;
                }

                // Создаем транзакцию (запись об оплате) - используем только поля из модели
                using var insertTransCmd = new NpgsqlCommand(@"
        INSERT INTO transactions (ticket_id, transaction_date, transaction_time)
        VALUES (@ticketId, @transactionDate, @transactionTime)
        RETURNING transaction_id", conn, transaction);

                var now = DateTime.Now;
                insertTransCmd.Parameters.AddWithValue("ticketId", ticketId);
                insertTransCmd.Parameters.AddWithValue("transactionDate", now.Date);
                insertTransCmd.Parameters.AddWithValue("transactionTime", now.TimeOfDay);

                var transactionId = await insertTransCmd.ExecuteScalarAsync();

                if (transactionId != null)
                {
                    await transaction.CommitAsync();

                    result["success"] = true;
                    result["message"] = $"Оплату квитка #{ticketId} підтверджено";
                    result["transaction_id"] = Convert.ToInt32(transactionId);
                    result["ticket_info"] = ticketInfo;
                    result["total_price"] = expectedPrice; // Используем цену из билета
                    result["change"] = paidAmount - expectedPrice;

                    _logger.LogInformation("Бухгалтер підтвердив оплату квитка {TicketId} на сумму {Amount} грн",
                        ticketId, expectedPrice);
                }
                else
                {
                    await transaction.RollbackAsync();
                    result["success"] = false;
                    result["message"] = "Помилка при створенні транзакції";
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Помилка при підтвердженні оплати квитка {TicketId}", ticketId);

                result["success"] = false;
                result["message"] = "Помилка сервера при підтвердженні оплати";
            }

            return result;
        }

        /// <summary>
        /// Получение финансового отчета
        /// </summary>
        public async Task<Dictionary<string, object>> GetFinancialReportAsync(DateTime startDate, DateTime endDate, string reportType)
        {
            var report = new Dictionary<string, object>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Общая статистика - используем total_price из ticket вместо amount из transactions
                string summaryQuery = @"
        SELECT 
            COUNT(t.ticket_id) as total_tickets_booked,
            COALESCE(SUM(t.total_price), 0) as total_amount_booked,
            COUNT(trans.transaction_id) as total_tickets_paid,
            COALESCE(SUM(CASE WHEN trans.transaction_id IS NOT NULL THEN t.total_price ELSE 0 END), 0) as total_amount_paid,
            COUNT(t.ticket_id) - COUNT(trans.transaction_id) as unpaid_tickets,
            COALESCE(SUM(CASE WHEN trans.transaction_id IS NULL THEN t.total_price ELSE 0 END), 0) as unpaid_amount
        FROM ticket t
        INNER JOIN schedule s ON t.schedule_id = s.schedule_id
        LEFT JOIN transactions trans ON t.ticket_id = trans.ticket_id
        WHERE t.booking_date >= @startDate AND t.booking_date <= @endDate";

                using var summaryCmd = new NpgsqlCommand(summaryQuery, conn);
                summaryCmd.Parameters.AddWithValue("startDate", startDate.Date);
                summaryCmd.Parameters.AddWithValue("endDate", endDate.Date.AddDays(1).AddTicks(-1));

                using var reader = await summaryCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    report["summary"] = new Dictionary<string, object>
                    {
                        ["total_tickets_booked"] = reader.GetInt32("total_tickets_booked"),
                        ["total_amount_booked"] = reader.GetDecimal("total_amount_booked"),
                        ["total_tickets_paid"] = reader.GetInt32("total_tickets_paid"),
                        ["total_amount_paid"] = reader.GetDecimal("total_amount_paid"),
                        ["unpaid_tickets"] = reader.GetInt32("unpaid_tickets"),
                        ["unpaid_amount"] = reader.GetDecimal("unpaid_amount"),
                        ["period_start"] = startDate.ToString("yyyy-MM-dd"),
                        ["period_end"] = endDate.ToString("yyyy-MM-dd")
                    };
                }

                // Статистика по поездам
                if (reportType == "detailed" || reportType == "trains")
                {
                    await reader.CloseAsync();

                    string trainsQuery = @"
            SELECT 
                tr.train_number,
                COUNT(t.ticket_id) as tickets_booked,
                COALESCE(SUM(t.total_price), 0) as amount_booked,
                COUNT(trans.transaction_id) as tickets_paid,
                COALESCE(SUM(CASE WHEN trans.transaction_id IS NOT NULL THEN t.total_price ELSE 0 END), 0) as amount_paid
            FROM ticket t
            INNER JOIN schedule s ON t.schedule_id = s.schedule_id
            INNER JOIN train tr ON s.train_id = tr.train_id
            LEFT JOIN transactions trans ON t.ticket_id = trans.ticket_id
            WHERE t.booking_date >= @startDate AND t.booking_date <= @endDate
            GROUP BY tr.train_number
            ORDER BY amount_paid DESC";

                    using var trainsCmd = new NpgsqlCommand(trainsQuery, conn);
                    trainsCmd.Parameters.AddWithValue("startDate", startDate.Date);
                    trainsCmd.Parameters.AddWithValue("endDate", endDate.Date.AddDays(1).AddTicks(-1));

                    var trainStats = new List<Dictionary<string, object>>();
                    using var trainsReader = await trainsCmd.ExecuteReaderAsync();
                    while (await trainsReader.ReadAsync())
                    {
                        trainStats.Add(new Dictionary<string, object>
                        {
                            ["train_number"] = trainsReader.GetInt32("train_number"),
                            ["tickets_booked"] = trainsReader.GetInt32("tickets_booked"),
                            ["amount_booked"] = trainsReader.GetDecimal("amount_booked"),
                            ["tickets_paid"] = trainsReader.GetInt32("tickets_paid"),
                            ["amount_paid"] = trainsReader.GetDecimal("amount_paid")
                        });
                    }
                    report["trains"] = trainStats;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при генерації фінансового звіту");
                throw;
            }

            return report;
        }

        /// <summary>
        /// Получение ежедневной статистики
        /// </summary>
        public async Task<List<Dictionary<string, object>>> GetDailyStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            var statistics = new List<Dictionary<string, object>>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = @"
        SELECT 
            t.booking_date::date as date,
            COUNT(t.ticket_id) as tickets_booked,
            COALESCE(SUM(t.total_price), 0) as amount_booked,
            COUNT(trans.transaction_id) as tickets_paid,
            COALESCE(SUM(CASE WHEN trans.transaction_id IS NOT NULL THEN t.total_price ELSE 0 END), 0) as amount_paid
        FROM ticket t
        LEFT JOIN transactions trans ON t.ticket_id = trans.ticket_id
        WHERE t.booking_date >= @startDate AND t.booking_date <= @endDate
        GROUP BY t.booking_date::date
        ORDER BY date DESC";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("startDate", startDate.Date);
                cmd.Parameters.AddWithValue("endDate", endDate.Date.AddDays(1).AddTicks(-1));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    statistics.Add(new Dictionary<string, object>
                    {
                        ["date"] = reader.GetDateTime("date").ToString("yyyy-MM-dd"),
                        ["tickets_booked"] = reader.GetInt32("tickets_booked"),
                        ["amount_booked"] = reader.GetDecimal("amount_booked"),
                        ["tickets_paid"] = reader.GetInt32("tickets_paid"),
                        ["amount_paid"] = reader.GetDecimal("amount_paid")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні денної статистики");
                throw;
            }

            return statistics;
        }

        /// <summary>
        /// Получение детального отчета для экспорта
        /// </summary>
        public async Task<List<Dictionary<string, object>>> GetDetailedFinancialReportAsync(DateTime startDate, DateTime endDate)
        {
            var reportData = new List<Dictionary<string, object>>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = @"
        SELECT 
            s.date as travel_date,
            t.ticket_id,
            CONCAT(c.client_name, ' ', c.client_surname) as client_name,
            tr.train_number,
            CONCAT(st_from.station_name, ' - ', st_to.station_name) as route,
            t.total_price,
            CASE 
                WHEN trans.transaction_id IS NOT NULL THEN 'Оплачено'
                ELSE 'Заброньовано'
            END as status,
            trans.transaction_date as payment_date
        FROM ticket t
        INNER JOIN client c ON t.client_id = c.client_id
        INNER JOIN schedule s ON t.schedule_id = s.schedule_id
        INNER JOIN train tr ON s.train_id = tr.train_id
        INNER JOIN stations st_from ON t.from_sequence_id = st_from.station_id
        INNER JOIN stations st_to ON t.to_sequence_id = st_to.station_id
        LEFT JOIN transactions trans ON t.ticket_id = trans.ticket_id
        WHERE t.booking_date >= @startDate AND t.booking_date <= @endDate
        ORDER BY t.booking_date DESC";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("startDate", startDate.Date);
                cmd.Parameters.AddWithValue("endDate", endDate.Date.AddDays(1).AddTicks(-1));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    reportData.Add(new Dictionary<string, object>
                    {
                        ["travel_date"] = reader.GetDateTime("travel_date").ToString("yyyy-MM-dd"),
                        ["ticket_id"] = reader.GetInt32("ticket_id"),
                        ["client_name"] = reader.GetString("client_name"),
                        ["train_number"] = reader.GetInt32("train_number"),
                        ["route"] = reader.GetString("route"),
                        ["total_price"] = reader.GetDecimal("total_price"),
                        ["status"] = reader.GetString("status"),
                        ["payment_date"] = reader.IsDBNull("payment_date") ? "" : reader.GetDateTime("payment_date").ToString("yyyy-MM-dd")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні детального фінансового звіту");
                throw;
            }

            return reportData;
        }

    }
}