using Npgsql;
using System.Data;
using train2.Models;
using train4.Services.Base;
using train4.Services.Interfaces;

namespace train4.Services.Implementation
{
    public class TrainService : BaseDatabaseService, ITrainService
    {
        public TrainService(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<TrainService> logger)
            : base(configuration, httpContextAccessor, logger) { }

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

        public async Task<List<CarriageTypes>> GetAllCarriageTypesAsync()
        {
            var carriageTypes = new List<CarriageTypes>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = @"
                        SELECT carriage_type_id, carriage_type, seat_count
                        FROM carriage_types_view
                        ORDER BY carriage_type";

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

        //CУЩЕСТВЕННІЕ ИЗМЕНЕНИЯ
        public async Task<(bool Success, string ErrorMessage)> CreateTrainWithCarriagesAsync(int trainNumber, int[] carriageTypeIds, int[] carriageNumbers)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string sql = "SELECT create_train_with_carriages(@trainNumber, @carriageTypeIds, @carriageNumbers)";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("trainNumber", trainNumber);
                cmd.Parameters.AddWithValue("carriageTypeIds", carriageTypeIds);
                cmd.Parameters.AddWithValue("carriageNumbers", carriageNumbers);

                string result = (string)(await cmd.ExecuteScalarAsync());

                if (result != "Потяг успішно створено")
                    return (false, result);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при створенні потяга");
                return (false, $"Помилка при створенні потяга: {ex.Message}");
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
                    FROM train_basic_info_view
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
                            SELECT train_carriage_types_id, carriage_type_id, carriage_number,
                                   carriage_type, seat_count, actual_seat_count
                            FROM train_carriages_info_view
                            WHERE train_id = @trainId
                            ORDER BY carriage_number";

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

                // 5. Валидация входных данных
                if (carriageData == null || carriageData.Count == 0)
                {
                    await transaction.RollbackAsync();
                    return (false, "Потяг має мати хоча б один вагон");
                }

                // Проверяем, что количество типов совпадает с количеством номеров
                var carriageTypeIds = carriageData.Where(c => c.ContainsKey("carriage_type_id") &&
                    Convert.ToInt32(c["carriage_type_id"]) > 0).Count();
                var carriageNumbers = carriageData.Where(c => c.ContainsKey("carriage_number") &&
                    Convert.ToInt32(c["carriage_number"]) > 0).Count();

                if (carriageTypeIds != carriageNumbers || carriageTypeIds != carriageData.Count)
                {
                    await transaction.RollbackAsync();
                    return (false, "Кількість типів вагонів не відповідає кількості номерів вагонів");
                }

                // 6. Удаляем незащищенные вагоны, которых нет в новом списке
                var newCarriageIds = carriageData
                    .Where(c => c.ContainsKey("train_carriage_types_id") &&
                        Convert.ToInt32(c["train_carriage_types_id"]) > 0)
                    .Select(c => Convert.ToInt32(c["train_carriage_types_id"]))
                    .ToHashSet();

                foreach (var currentCarriage in currentCarriages)
                {
                    var carriageId = Convert.ToInt32(currentCarriage["train_carriage_types_id"]);

                    // Если вагон защищен, пропускаем
                    if (protectedCarriages.Contains(carriageId))
                    {
                        continue;
                    }

                    // Если незащищенный вагон не в новом списке - удаляем его вместе с местами
                    if (!newCarriageIds.Contains(carriageId))
                    {
                        // Сначала удаляем места
                        using var deleteSeatCmd = new NpgsqlCommand(@"
                    DELETE FROM seat WHERE train_carriage_types_id = @carriageId", conn, transaction);
                        deleteSeatCmd.Parameters.AddWithValue("carriageId", carriageId);
                        await deleteSeatCmd.ExecuteNonQueryAsync();

                        // Затем удаляем вагон
                        using var deleteCarriageCmd = new NpgsqlCommand(@"
                    DELETE FROM train_carriage_types WHERE train_carriage_types_id = @carriageId", conn, transaction);
                        deleteCarriageCmd.Parameters.AddWithValue("carriageId", carriageId);
                        await deleteCarriageCmd.ExecuteNonQueryAsync();

                        _logger.LogInformation($"Удален вагон ID={carriageId} вместе с местами");
                    }
                }

                // 7. Обрабатываем вагоны из формы
                foreach (var carriage in carriageData)
                {
                    var carriageTypeId = Convert.ToInt32(carriage["carriage_type_id"]);
                    var carriageNumber = Convert.ToInt32(carriage["carriage_number"]);

                    if (carriage.ContainsKey("train_carriage_types_id") &&
                        Convert.ToInt32(carriage["train_carriage_types_id"]) > 0)
                    {
                        // Существующий вагон
                        var carriageId = Convert.ToInt32(carriage["train_carriage_types_id"]);

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
                        // Новый вагон - сначала создаем вагон, потом места
                        int newCarriageId;
                        using var cmd = new NpgsqlCommand(@"
                    INSERT INTO train_carriage_types (train_id, carriage_type_id, carriage_number)
                    VALUES (@trainId, @carriageTypeId, @carriageNumber)
                    RETURNING train_carriage_types_id", conn, transaction);

                        cmd.Parameters.AddWithValue("trainId", trainId);
                        cmd.Parameters.AddWithValue("carriageTypeId", carriageTypeId);
                        cmd.Parameters.AddWithValue("carriageNumber", carriageNumber);

                        newCarriageId = (int)(await cmd.ExecuteScalarAsync());

                        // Создаем места для нового вагона
                        await CreateSeatsForCarriageAsync(conn, transaction, newCarriageId, carriageTypeId);

                        _logger.LogInformation($"Добавлен новый вагон №{carriageNumber} с ID={newCarriageId}");
                    }
                }

                // 8. Обновляем количество вагонов
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

        // Вспомогательный метод для создания мест
        private async Task CreateSeatsForCarriageAsync(NpgsqlConnection conn, NpgsqlTransaction transaction,
            int carriageId, int carriageTypeId)
        {
            // Получаем количество мест для данного типа вагона
            int seatCount;
            using (var cmd = new NpgsqlCommand(@"
        SELECT seat_count FROM carriage_types WHERE carriage_type_id = @carriageTypeId", conn, transaction))
            {
                cmd.Parameters.AddWithValue("carriageTypeId", carriageTypeId);
                seatCount = (int)(await cmd.ExecuteScalarAsync());
            }

            // Создаем места
            for (int seatNumber = 1; seatNumber <= seatCount; seatNumber++)
            {
                using var cmd = new NpgsqlCommand(@"
            INSERT INTO seat (seat_number, train_carriage_types_id)
            VALUES (@seatNumber, @carriageId)", conn, transaction);

                cmd.Parameters.AddWithValue("seatNumber", seatNumber);
                cmd.Parameters.AddWithValue("carriageId", carriageId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        //public async Task<(bool Success, string ErrorMessage)> UpdateTrainAsync(
        //   int trainId,
        //   int trainNumber,
        //   List<Dictionary<string, object>> carriageData)
        //{
        //    using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //    await conn.OpenAsync();
        //    using var transaction = await conn.BeginTransactionAsync();

        //    try
        //    {
        //        _logger.LogInformation($"Оновлення потяга: ID={trainId}, Номер={trainNumber}");

        //        // 1. Проверяем уникальность номера поезда (исключая текущий поезд)
        //        using (var checkCmd = new NpgsqlCommand(@"
        //                    SELECT COUNT(*) FROM train 
        //                    WHERE train_number = @trainNumber AND train_id != @trainId", conn, transaction))
        //        {
        //            checkCmd.Parameters.AddWithValue("trainNumber", trainNumber);
        //            checkCmd.Parameters.AddWithValue("trainId", trainId);

        //            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
        //            if (count > 0)
        //            {
        //                await transaction.RollbackAsync();
        //                return (false, $"Потяг з номером {trainNumber} вже існує");
        //            }
        //        }

        //        // 2. Обновляем номер поезда
        //        using (var cmd = new NpgsqlCommand(@"
        //    UPDATE train SET train_number = @trainNumber WHERE train_id = @trainId", conn, transaction))
        //        {
        //            cmd.Parameters.AddWithValue("trainNumber", trainNumber);
        //            cmd.Parameters.AddWithValue("trainId", trainId);
        //            await cmd.ExecuteNonQueryAsync();
        //        }

        //        // 3. Получаем список вагонов с бронированиями (их нельзя изменять)
        //        var protectedCarriages = new HashSet<int>();
        //        using (var cmd = new NpgsqlCommand(@"
        //    SELECT DISTINCT tct.train_carriage_types_id
        //    FROM train_carriage_types tct
        //    INNER JOIN seat s ON tct.train_carriage_types_id = s.train_carriage_types_id
        //    INNER JOIN ticket t ON s.seat_id = t.seat_id
        //    WHERE tct.train_id = @trainId", conn, transaction))
        //        {
        //            cmd.Parameters.AddWithValue("trainId", trainId);
        //            using var reader = await cmd.ExecuteReaderAsync();
        //            while (await reader.ReadAsync())
        //            {
        //                protectedCarriages.Add(reader.GetInt32(0));
        //            }
        //        }

        //        // 4. Получаем текущие вагоны
        //        var currentCarriages = new List<Dictionary<string, object>>();
        //        using (var cmd = new NpgsqlCommand(@"
        //    SELECT train_carriage_types_id, carriage_type_id, carriage_number
        //    FROM train_carriage_types 
        //    WHERE train_id = @trainId
        //    ORDER BY carriage_number", conn, transaction))
        //        {
        //            cmd.Parameters.AddWithValue("trainId", trainId);
        //            using var reader = await cmd.ExecuteReaderAsync();
        //            while (await reader.ReadAsync())
        //            {
        //                currentCarriages.Add(new Dictionary<string, object>
        //                {
        //                    ["train_carriage_types_id"] = reader.GetInt32(0),
        //                    ["carriage_type_id"] = reader.GetInt32(1),
        //                    ["carriage_number"] = reader.GetInt32(2)
        //                });
        //            }
        //        }

        //        // 5. Обрабатываем вагоны из формы
        //        var processedCarriageIds = new List<int>();

        //        foreach (var carriage in carriageData)
        //        {
        //            var carriageTypeId = Convert.ToInt32(carriage["carriage_type_id"]);
        //            var carriageNumber = Convert.ToInt32(carriage["carriage_number"]);

        //            if (carriage.ContainsKey("train_carriage_types_id") &&
        //                Convert.ToInt32(carriage["train_carriage_types_id"]) > 0)
        //            {
        //                // Существующий вагон
        //                var carriageId = Convert.ToInt32(carriage["train_carriage_types_id"]);
        //                processedCarriageIds.Add(carriageId);

        //                // Проверяем, защищен ли вагон
        //                if (protectedCarriages.Contains(carriageId))
        //                {
        //                    _logger.LogInformation($"Пропускаем защищенный вагон ID={carriageId}");
        //                    continue; // Пропускаем защищенные вагоны
        //                }

        //                // Обновляем незащищенный вагон
        //                using var cmd = new NpgsqlCommand(@"
        //            UPDATE train_carriage_types 
        //            SET carriage_type_id = @carriageTypeId, carriage_number = @carriageNumber
        //            WHERE train_carriage_types_id = @carriageId", conn, transaction);

        //                cmd.Parameters.AddWithValue("carriageId", carriageId);
        //                cmd.Parameters.AddWithValue("carriageTypeId", carriageTypeId);
        //                cmd.Parameters.AddWithValue("carriageNumber", carriageNumber);
        //                await cmd.ExecuteNonQueryAsync();

        //                _logger.LogInformation($"Обновлен вагон ID={carriageId}");
        //            }
        //            else
        //            {
        //                // Новый вагон
        //                using var cmd = new NpgsqlCommand(@"
        //            INSERT INTO train_carriage_types (train_id, carriage_type_id, carriage_number)
        //            VALUES (@trainId, @carriageTypeId, @carriageNumber)", conn, transaction);

        //                cmd.Parameters.AddWithValue("trainId", trainId);
        //                cmd.Parameters.AddWithValue("carriageTypeId", carriageTypeId);
        //                cmd.Parameters.AddWithValue("carriageNumber", carriageNumber);
        //                await cmd.ExecuteNonQueryAsync();

        //                _logger.LogInformation($"Добавлен новый вагон №{carriageNumber}");
        //            }
        //        }

        //        // 6. Удаляем вагоны, которых нет в форме (но только незащищенные)
        //        foreach (var currentCarriage in currentCarriages)
        //        {
        //            var carriageId = Convert.ToInt32(currentCarriage["train_carriage_types_id"]);

        //            // Если вагон защищен, добавляем его в список обработанных
        //            if (protectedCarriages.Contains(carriageId))
        //            {
        //                processedCarriageIds.Add(carriageId);
        //                continue;
        //            }

        //            // Если незащищенный вагон не в новом списке - удаляем
        //            if (!processedCarriageIds.Contains(carriageId))
        //            {
        //                using var cmd = new NpgsqlCommand(@"
        //            DELETE FROM train_carriage_types 
        //            WHERE train_carriage_types_id = @carriageId", conn, transaction);

        //                cmd.Parameters.AddWithValue("carriageId", carriageId);
        //                await cmd.ExecuteNonQueryAsync();

        //                _logger.LogInformation($"Удален вагон ID={carriageId}");
        //            }
        //        }

        //        // 7. Обновляем количество вагонов
        //        using (var cmd = new NpgsqlCommand(@"
        //    UPDATE train SET carriage_count = (
        //        SELECT COUNT(*) FROM train_carriage_types WHERE train_id = @trainId
        //    ) WHERE train_id = @trainId", conn, transaction))
        //        {
        //            cmd.Parameters.AddWithValue("trainId", trainId);
        //            await cmd.ExecuteNonQueryAsync();
        //        }

        //        await transaction.CommitAsync();

        //        var protectedCount = protectedCarriages.Count;
        //        var message = protectedCount > 0
        //            ? $"Потяг оновлено. {protectedCount} вагонів з бронюваннями залишились без змін."
        //            : "Потяг успішно оновлено.";

        //        _logger.LogInformation("Потяг успішно оновлено");
        //        return (true, message);
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        _logger.LogError(ex, "Помилка при оновленні потяга");
        //        return (false, ex.Message);
        //    }
        //}

        //public async Task<List<Dictionary<string, object>>> GetCarriagesWithBookingsAsync(int trainId)
        //{
        //    var carriagesWithBookings = new List<Dictionary<string, object>>();

        //    try
        //    {
        //        using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //        await conn.OpenAsync();

        //        // Вызов функции с правильной обработкой типов
        //        string query = "SELECT * FROM get_carriages_with_booking(@p_train_id)";
        //        using var cmd = new NpgsqlCommand(query, conn);
        //        cmd.Parameters.AddWithValue("@p_train_id", trainId);

        //        using var reader = await cmd.ExecuteReaderAsync();
        //        while (await reader.ReadAsync())
        //        {
        //            var carriageInfo = new Dictionary<string, object>();

        //            // Безопасное чтение данных с проверкой типов
        //            carriageInfo["train_carriage_types_id"] = reader.IsDBNull("train_carriage_types_id")
        //                ? 0 : reader.GetInt32("train_carriage_types_id");

        //            carriageInfo["carriage_number"] = reader.IsDBNull("carriage_number")
        //                ? 0 : reader.GetInt32("carriage_number");

        //            carriageInfo["carriage_type"] = reader.IsDBNull("carriage_type")
        //                ? string.Empty : reader.GetString("carriage_type");

        //            // COUNT может возвращать разные типы, проверяем
        //            var bookingCountValue = reader["booking_count"];
        //            if (bookingCountValue is int intValue)
        //            {
        //                carriageInfo["booking_count"] = intValue;
        //            }
        //            else if (bookingCountValue is long longValue)
        //            {
        //                carriageInfo["booking_count"] = (int)longValue;
        //            }
        //            else
        //            {
        //                carriageInfo["booking_count"] = Convert.ToInt32(bookingCountValue);
        //            }

        //            carriagesWithBookings.Add(carriageInfo);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Помилка при отриманні вагонів з бронюваннями для потяга {TrainId}", trainId);

        //        // Для отладки - выводим детали ошибки
        //        if (ex is Npgsql.PostgresException pgEx)
        //        {
        //            _logger.LogError("PostgreSQL Error: {SqlState}, {MessageText}", pgEx.SqlState, pgEx.MessageText);
        //        }
        //    }

        //    return carriagesWithBookings;
        //}

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

        //// ПРИВАТНЫЕ МЕТОДЫ (если нужны в этом сервисе):
        //private async Task<int> GetTrainIdByNumberAsync(int trainNumber, NpgsqlConnection existingConnection = null)
        //{
        //    var shouldCloseConnection = existingConnection == null;

        //    try
        //    {
        //        var conn = existingConnection ?? new NpgsqlConnection(GetCurrentConnectionString());
        //        if (shouldCloseConnection) await conn.OpenAsync();

        //        using var cmd = new NpgsqlCommand("SELECT train_id FROM train WHERE train_number = @trainNumber", conn);
        //        cmd.Parameters.AddWithValue("trainNumber", trainNumber);

        //        var result = await cmd.ExecuteScalarAsync();
        //        return result != null ? Convert.ToInt32(result) : 0;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Помилка отримання ID потягу за номером {trainNumber}");
        //        return 0;
        //    }
        //    finally
        //    {
        //        if (shouldCloseConnection && existingConnection == null)
        //        {
        //            existingConnection?.Close();
        //        }
        //    }
        //}
    }
}
//        public async Task<(bool Success, string ErrorMessage)> UpdateTrainAsync(int trainId,int trainNumber,List<Dictionary<string, object>> carriageData)
//private async Task<int> GetTrainIdByNumberAsync(int trainNumber, NpgsqlConnection existingConnection = null)