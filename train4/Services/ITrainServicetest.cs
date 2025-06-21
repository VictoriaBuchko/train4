using Npgsql;
using System.Data;
using train2.Models;
using train2.Services;

public class PostgreSqlTicketService : ITicketServicetest
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PostgreSqlTicketService> _logger;

    private const string DefaultHost = "localhost";
    private const string DefaultDatabase = "TrainTicketDb2";
    private const string DefaultPort = "5432";
    private const string GuestUser = "guest_user";
    private const string GuestPassword = "guest123";

    public PostgreSqlTicketService(
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PostgreSqlTicketService> logger)
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


    //public async Task<List<Ticket>> GetActiveTicketsAsync(int clientId)
    //{
    //    var tickets = new List<Ticket>();

    //    try
    //    {
    //        using var conn = new NpgsqlConnection(GetCurrentConnectionString());
    //        await conn.OpenAsync();
    //        using (var setPathCmd = new NpgsqlCommand("SET search_path TO public;", conn))
    //        {
    //            await setPathCmd.ExecuteNonQueryAsync();
    //        }

    //        var cmd = new NpgsqlCommand("SELECT * FROM get_client_active_tickets_detailed(@client_id)", conn);
    //        cmd.Parameters.AddWithValue("client_id", clientId);

    //        using var reader = await cmd.ExecuteReaderAsync();

    //        while (await reader.ReadAsync())
    //        {
    //            var ticket = new Ticket
    //            {
    //                ticket_id = reader.GetInt32("ticket_id"),
    //                seat_id = reader.GetInt32("seat_id"),
    //                client_id = reader.GetInt32("client_id"),
    //                schedule_id = reader.GetInt32("schedule_id"),
    //                booking_date = reader.GetDateTime("booking_date"), // ИСПРАВЛЕНО: убрали GetFieldValue
    //                total_price = reader.GetDecimal("total_price"),
    //                from_sequence_id = reader.GetInt32("from_sequence_id"),
    //                to_sequence_id = reader.GetInt32("to_sequence_id"),

    //                // Seat information
    //                Seat = new Seat
    //                {
    //                    seat_id = reader.GetInt32("seat_id"),
    //                    seat_number = reader.GetInt32("seat_number"),
    //                    train_carriage_types_id = reader.GetInt32("train_carriage_types_id"),
    //                    TrainCarriageTypes = new TrainCarriageTypes
    //                    {
    //                        train_carriage_types_id = reader.GetInt32("train_carriage_types_id"),
    //                        carriage_number = reader.GetInt32("carriage_number"),
    //                        train_id = reader.GetInt32("train_id"),
    //                        CarriageTypes = new CarriageTypes
    //                        {
    //                            carriage_type = reader.GetString("carriage_type")
    //                        }
    //                    }
    //                },

    //                Schedule = new Schedule
    //                {
    //                    schedule_id = reader.GetInt32(reader.GetOrdinal("schedule_id")),
    //                    train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
    //                    date = reader.GetDateTime(reader.GetOrdinal("date")),
    //                    departure_time = reader.GetTimeSpan(reader.GetOrdinal("departure_time")),
    //                    Train = new Train
    //                    {
    //                        train_id = reader.GetInt32(reader.GetOrdinal("train_id")),
    //                        train_number = reader.GetInt32(reader.GetOrdinal("train_number"))
    //                    }
    //                },


    //                // Station information using existing navigation properties
    //                FromStation = new Stations
    //                {
    //                    station_id = reader.GetInt32("from_sequence_id"),
    //                    station_name = reader.GetString("from_station_name")
    //                },

    //                ToStation = new Stations
    //                {
    //                    station_id = reader.GetInt32("to_sequence_id"),
    //                    station_name = reader.GetString("to_station_name")
    //                },

    //                // Payment status (using Transactions list to indicate payment)
    //                Transactions = reader.GetBoolean("is_paid")
    //                    ? new List<Transactions> { new Transactions() }
    //                    : new List<Transactions>()
    //            };

    //            // Добавляем станции в StationSequences для совместимости с существующей логикой
    //            if (ticket.Schedule.Train.StationSequences == null)
    //                ticket.Schedule.Train.StationSequences = new List<StationSequence>();

    //            // Создаем StationSequence для отображения маршрута
    //            var fromStationSeq = new StationSequence
    //            {
    //                station_id = reader.GetInt32("from_sequence_id"),
    //                train_id = reader.GetInt32("train_id"),
    //                Stations = ticket.FromStation
    //            };

    //            var toStationSeq = new StationSequence
    //            {
    //                station_id = reader.GetInt32("to_sequence_id"),
    //                train_id = reader.GetInt32("train_id"),
    //                Stations = ticket.ToStation
    //            };

    //            ticket.Schedule.Train.StationSequences.Add(fromStationSeq);
    //            ticket.Schedule.Train.StationSequences.Add(toStationSeq);

    //            tickets.Add(ticket);
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Ошибка при получении активных билетов для клиента {ClientId}", clientId);
    //    }

    //    return tickets;
    //}

    //public async Task<List<Ticket>> GetHistoricalTicketsAsync(int clientId)
    //{
    //    var tickets = new List<Ticket>();

    //    try
    //    {
    //        using var conn = new NpgsqlConnection(GetCurrentConnectionString());
    //        await conn.OpenAsync();

    //        //var cmd = new NpgsqlCommand("SELECT * FROM get_client_historical_tickets_detailed(@client_id)", conn);
    //        var cmd = new NpgsqlCommand("SELECT * FROM get_client_historical_tickets_detailed(@client_id)", conn);

    //        cmd.Parameters.AddWithValue("client_id", clientId);

    //        using var reader = await cmd.ExecuteReaderAsync();

    //        while (await reader.ReadAsync())
    //        {
    //            var ticket = new Ticket
    //            {
    //                ticket_id = reader.GetInt32("ticket_id"),
    //                seat_id = reader.GetInt32("seat_id"),
    //                client_id = reader.GetInt32("client_id"),
    //                schedule_id = reader.GetInt32("schedule_id"),
    //                booking_date = reader.GetFieldValue<DateTime>("booking_date"),
    //                total_price = reader.GetDecimal("total_price"),
    //                from_sequence_id = reader.GetInt32("from_sequence_id"),
    //                to_sequence_id = reader.GetInt32("to_sequence_id"),

    //                // Seat information
    //                Seat = new Seat
    //                {
    //                    seat_id = reader.GetInt32("seat_id"),
    //                    seat_number = reader.GetInt32("seat_number"),
    //                    train_carriage_types_id = reader.GetInt32("train_carriage_types_id"),
    //                    TrainCarriageTypes = new TrainCarriageTypes
    //                    {
    //                        train_carriage_types_id = reader.GetInt32("train_carriage_types_id"),
    //                        carriage_number = reader.GetInt32("carriage_number"),
    //                        train_id = reader.GetInt32("train_id"),
    //                        CarriageTypes = new CarriageTypes
    //                        {
    //                            carriage_type = reader.GetString("carriage_type")
    //                        }
    //                    }
    //                },

    //                // Schedule information
    //                Schedule = new Schedule
    //                {
    //                    schedule_id = reader.GetInt32("schedule_id"),
    //                    train_id = reader.GetInt32("train_id"),
    //                    date = reader.GetFieldValue<DateTime>("date"),
    //                    departure_time = reader.GetFieldValue<TimeSpan>("departure_time"),
    //                    Train = new Train
    //                    {
    //                        train_id = reader.GetInt32("train_id"),
    //                        train_number = reader.GetInt32("train_number")
    //                    }
    //                },

    //                // Station information using existing navigation properties
    //                FromStation = new Stations
    //                {
    //                    station_id = reader.GetInt32("from_sequence_id"),
    //                    station_name = reader.GetString("from_station_name")
    //                },

    //                ToStation = new Stations
    //                {
    //                    station_id = reader.GetInt32("to_sequence_id"),
    //                    station_name = reader.GetString("to_station_name")
    //                },

    //                // Payment status (using Transactions list to indicate payment)
    //                Transactions = reader.GetBoolean("is_paid")
    //                    ? new List<Transactions> { new Transactions() }
    //                    : new List<Transactions>()
    //            };

    //            // Добавляем станции в StationSequences для совместимости
    //            if (ticket.Schedule.Train.StationSequences == null)
    //                ticket.Schedule.Train.StationSequences = new List<StationSequence>();

    //            var fromStationSeq = new StationSequence
    //            {
    //                station_id = reader.GetInt32("from_sequence_id"),
    //                train_id = reader.GetInt32("train_id"),
    //                Stations = ticket.FromStation
    //            };

    //            var toStationSeq = new StationSequence
    //            {
    //                station_id = reader.GetInt32("to_sequence_id"),
    //                train_id = reader.GetInt32("train_id"),
    //                Stations = ticket.ToStation
    //            };

    //            ticket.Schedule.Train.StationSequences.Add(fromStationSeq);
    //            ticket.Schedule.Train.StationSequences.Add(toStationSeq);

    //            tickets.Add(ticket);
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Ошибка при получении исторических билетов для клиента {ClientId}", clientId);
    //    }

    //    return tickets;
    //}
    public async Task<List<Ticket>> GetActiveTicketsAsync(int clientId)
    {
        var tickets = new List<Ticket>();

        try
        {
            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();

            // Устанавливаем контекст для безопасности доступа
            string setContextSql = $"SET search_path TO public; SET app.current_client = {clientId};";
            using (var setCmd = new NpgsqlCommand(setContextSql, conn))
            {
                await setCmd.ExecuteNonQueryAsync();
            }

            // Используем представление для получения данных с ограничением доступа
            var cmd = new NpgsqlCommand("SELECT * FROM v_client_active_tickets ORDER BY date ASC, departure_time ASC", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var ticket = new Ticket
                {
                    ticket_id = reader.GetInt32("ticket_id"),
                    seat_id = reader.GetInt32("seat_id"),
                    client_id = reader.GetInt32("client_id"),
                    schedule_id = reader.GetInt32("schedule_id"),
                    booking_date = reader.GetDateTime("booking_date"),
                    total_price = reader.GetDecimal("total_price"),
                    from_sequence_id = reader.GetInt32("from_sequence_id"),
                    to_sequence_id = reader.GetInt32("to_sequence_id"),

                    Seat = new Seat
                    {
                        seat_id = reader.GetInt32("seat_id"),
                        seat_number = reader.GetInt32("seat_number"),
                        train_carriage_types_id = reader.GetInt32("train_carriage_types_id"),
                        TrainCarriageTypes = new TrainCarriageTypes
                        {
                            train_carriage_types_id = reader.GetInt32("train_carriage_types_id"),
                            carriage_number = reader.GetInt32("carriage_number"),
                            train_id = reader.GetInt32("train_id"),
                            CarriageTypes = new CarriageTypes
                            {
                                carriage_type = reader.GetString("carriage_type")
                            }
                        }
                    },

                    Schedule = new Schedule
                    {
                        schedule_id = reader.GetInt32("schedule_id"),
                        train_id = reader.GetInt32("train_id"),
                        date = reader.GetDateTime("date"),
                        departure_time = reader.GetFieldValue<TimeSpan>("departure_time"),
                        Train = new Train
                        {
                            train_id = reader.GetInt32("train_id"),
                            train_number = reader.GetInt32("train_number")
                        }
                    },

                    FromStation = new Stations
                    {
                        station_id = reader.GetInt32("from_sequence_id"),
                        station_name = reader.GetString("from_station_name")
                    },

                    ToStation = new Stations
                    {
                        station_id = reader.GetInt32("to_sequence_id"),
                        station_name = reader.GetString("to_station_name")
                    },

                    // Определяем статус оплаты
                    Transactions = reader.GetBoolean("is_paid")
                        ? new List<Transactions> { new Transactions() }
                        : new List<Transactions>()
                };

                // Добавляем информацию о времени отправления и прибытия
                if (ticket.Schedule.Train.StationSequences == null)
                    ticket.Schedule.Train.StationSequences = new List<StationSequence>();

                ticket.Schedule.Train.StationSequences.Add(new StationSequence
                {
                    station_id = ticket.from_sequence_id,
                    train_id = ticket.Schedule.train_id,
                    Stations = ticket.FromStation
                });

                ticket.Schedule.Train.StationSequences.Add(new StationSequence
                {
                    station_id = ticket.to_sequence_id,
                    train_id = ticket.Schedule.train_id,
                    Stations = ticket.ToStation
                });

                tickets.Add(ticket);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении активных билетов для клиента {ClientId}", clientId);
            throw; // Перебрасываем исключение для обработки на верхнем уровне
        }
        finally
        {
            // Очищаем контекст (опционально)
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();
                using var resetCmd = new NpgsqlCommand("RESET app.current_client", conn);
                await resetCmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Игнорируем ошибки сброса контекста
            }
        }

        return tickets;
    }

    public async Task<List<Ticket>> GetHistoricalTicketsAsync(int clientId)
    {
        var tickets = new List<Ticket>();

        try
        {
            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();

            // Устанавливаем контекст для безопасности доступа
            string setContextSql = $"SET search_path TO public; SET app.current_client = {clientId};";
            using (var setCmd = new NpgsqlCommand(setContextSql, conn))
            {
                await setCmd.ExecuteNonQueryAsync();
            }

            // Используем представление для получения исторических данных с ограничением доступа
            var cmd = new NpgsqlCommand("SELECT * FROM v_client_historical_tickets ORDER BY date DESC, departure_time DESC", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var ticket = new Ticket
                {
                    ticket_id = reader.GetInt32("ticket_id"),
                    seat_id = reader.GetInt32("seat_id"),
                    client_id = reader.GetInt32("client_id"),
                    schedule_id = reader.GetInt32("schedule_id"),
                    booking_date = reader.GetDateTime("booking_date"),
                    total_price = reader.GetDecimal("total_price"),
                    from_sequence_id = reader.GetInt32("from_sequence_id"),
                    to_sequence_id = reader.GetInt32("to_sequence_id"),

                    Seat = new Seat
                    {
                        seat_id = reader.GetInt32("seat_id"),
                        seat_number = reader.GetInt32("seat_number"),
                        train_carriage_types_id = reader.GetInt32("train_carriage_types_id"),
                        TrainCarriageTypes = new TrainCarriageTypes
                        {
                            train_carriage_types_id = reader.GetInt32("train_carriage_types_id"),
                            carriage_number = reader.GetInt32("carriage_number"),
                            train_id = reader.GetInt32("train_id"),
                            CarriageTypes = new CarriageTypes
                            {
                                carriage_type = reader.GetString("carriage_type")
                            }
                        }
                    },

                    Schedule = new Schedule
                    {
                        schedule_id = reader.GetInt32("schedule_id"),
                        train_id = reader.GetInt32("train_id"),
                        date = reader.GetDateTime("date"),
                        departure_time = reader.GetFieldValue<TimeSpan>("departure_time"),
                        Train = new Train
                        {
                            train_id = reader.GetInt32("train_id"),
                            train_number = reader.GetInt32("train_number")
                        }
                    },

                    FromStation = new Stations
                    {
                        station_id = reader.GetInt32("from_sequence_id"),
                        station_name = reader.GetString("from_station_name")
                    },

                    ToStation = new Stations
                    {
                        station_id = reader.GetInt32("to_sequence_id"),
                        station_name = reader.GetString("to_station_name")
                    },

                    // Определяем статус оплаты для исторических билетов
                    Transactions = reader.GetBoolean("is_paid")
                        ? new List<Transactions> { new Transactions() }
                        : new List<Transactions>()
                };

                // Добавляем информацию о маршруте
                if (ticket.Schedule.Train.StationSequences == null)
                    ticket.Schedule.Train.StationSequences = new List<StationSequence>();

                ticket.Schedule.Train.StationSequences.Add(new StationSequence
                {
                    station_id = ticket.from_sequence_id,
                    train_id = ticket.Schedule.train_id,
                    Stations = ticket.FromStation
                });

                ticket.Schedule.Train.StationSequences.Add(new StationSequence
                {
                    station_id = ticket.to_sequence_id,
                    train_id = ticket.Schedule.train_id,
                    Stations = ticket.ToStation
                });

                tickets.Add(ticket);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении исторических билетов для клиента {ClientId}", clientId);
            throw; // Перебрасываем исключение для обработки на верхнем уровне
        }
        finally
        {
            // Очищаем контекст (опционально)
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();
                using var resetCmd = new NpgsqlCommand("RESET app.current_client", conn);
                await resetCmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Игнорируем ошибки сброса контекста
            }
        }

        return tickets;
    }
}