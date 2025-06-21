using Npgsql;
using System.Data;
using train2.Models;
using train4.Services.Base;

namespace train4.Services.Implementation
{
    public class TicketService : BaseDatabaseService, Interfaces.ITicketService
    {
        public TicketService(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<TicketService> logger)
            : base(configuration, httpContextAccessor, logger) { }


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
        public async Task<Dictionary<string, object>> GetTicketInfoAsync(int trainId, DateTime date, int seatId, int fromStationId, int toStationId)
        {
            var result = new Dictionary<string, object>();

            try
            {
                // ИСПРАВЛЕНИЕ: Более детальная отладка с проверками
                _logger.LogInformation("----- DEBUG: Значення параметрів -----");
                _logger.LogInformation("Train ID: {TrainId}", trainId);
                _logger.LogInformation("Date (original): {Date}", date);
                _logger.LogInformation("Date (formatted): {DateFormatted}", date.ToString("yyyy-MM-dd"));
                _logger.LogInformation("Date.Date: {DateOnly}", date.Date);
                _logger.LogInformation("Seat ID: {SeatId}", seatId);
                _logger.LogInformation("From Station ID: {FromStationId}", fromStationId);
                _logger.LogInformation("To Station ID: {ToStationId}", toStationId);
                _logger.LogInformation("--------------------------------------");

                // ИСПРАВЛЕНИЕ: Проверяем корректность даты перед вызовом функции
                if (date == DateTime.MinValue || date == default(DateTime))
                {
                    _logger.LogError("GetTicketInfoAsync: Дата равна MinValue или default");
                    result["error"] = "Некорректная дата поездки";
                    return result;
                }

                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // ИСПРАВЛЕНИЕ: Используем явное приведение типов и проверяем параметры
                var cmd = new NpgsqlCommand("SELECT * FROM get_ticket_info(@train_id, @date::date, @seat_id, @from_station_id, @to_station_id)", conn);

                cmd.Parameters.AddWithValue("train_id", trainId);
                cmd.Parameters.AddWithValue("date", date.Date); // ИСПРАВЛЕНИЕ: Обязательно .Date
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
                else
                {
                    _logger.LogWarning("get_ticket_info вернула пустой результат для поезда {TrainId} на дату {Date}", trainId, date);
                    result["error"] = "Информация о билете не найдена";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка під час виклику get_ticket_info для поезда {TrainId} на дату {Date}", trainId, date);
                result["error"] = "Ошибка получения информации о билете";
            }

            return result;
        }
        //public async Task<Dictionary<string, object>> GetTicketInfoAsync(int trainId, DateTime date, int seatId, int fromStationId, int toStationId)
        //{
        //    var result = new Dictionary<string, object>();

        //    try
        //    {
        //        // Вивід у консоль
        //        Console.WriteLine("----- DEBUG: Значення параметрів -----");
        //        Console.WriteLine("Train ID: " + trainId);
        //        Console.WriteLine("Date: " + date.ToString("yyyy-MM-dd"));
        //        Console.WriteLine("Seat ID: " + seatId);
        //        Console.WriteLine("From Station ID: " + fromStationId);
        //        Console.WriteLine("To Station ID: " + toStationId);
        //        Console.WriteLine("--------------------------------------");

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

        //   public async Task<Dictionary<string, object>> CreateTicketBookingAsync(
        //int clientId, int seatId, int trainId, DateTime travelDate, int fromStationId, int toStationId)
        //   {
        //       var result = new Dictionary<string, object>();

        //       try
        //       {
        //           using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //           await conn.OpenAsync();

        //           using var cmd = new NpgsqlCommand("SELECT * FROM book_ticket_if_available(@clientId, @trainId, @seatId, @travelDate, @fromStationId, @toStationId)", conn);

        //           cmd.Parameters.AddWithValue("clientId", clientId);
        //           cmd.Parameters.AddWithValue("trainId", trainId);
        //           cmd.Parameters.AddWithValue("seatId", seatId);
        //           cmd.Parameters.AddWithValue("travelDate", travelDate.Date);
        //           cmd.Parameters.AddWithValue("fromStationId", fromStationId);
        //           cmd.Parameters.AddWithValue("toStationId", toStationId);

        //           using var reader = await cmd.ExecuteReaderAsync();

        //           if (await reader.ReadAsync())
        //           {
        //               result["success"] = reader.GetBoolean(reader.GetOrdinal("success"));
        //               result["message"] = reader.GetString(reader.GetOrdinal("message"));
        //               result["ticket_id"] = reader.IsDBNull(reader.GetOrdinal("ticket_id")) ? null : reader.GetInt32(reader.GetOrdinal("ticket_id"));
        //               result["total_price"] = reader.IsDBNull(reader.GetOrdinal("total_price")) ? null : reader.GetDecimal(reader.GetOrdinal("total_price"));
        //           }
        //       }
        //       catch (Exception ex)
        //       {
        //           _logger.LogError(ex, "Помилка при створенні квитка через збережену функцію");
        //           result["success"] = false;
        //           result["message"] = "Помилка сервера при бронюванні квитка";
        //       }

        //       return result;
        //   }
        public async Task<Dictionary<string, object>> CreateTicketBookingAsync(
       int clientId, int seatId, int trainId, DateTime travelDate, int fromStationId, int toStationId)
        {
            var result = new Dictionary<string, object>();

            try
            {
                _logger.LogInformation("Creating ticket booking for client {ClientId}, seat {SeatId}, train {TrainId}, date {TravelDate}",
                    clientId, seatId, trainId, travelDate.Date);

                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Используем правильный порядок параметров согласно функции PostgreSQL
                using var cmd = new NpgsqlCommand(
                    "SELECT * FROM book_ticket_if_availables(@client_id, @train_id, @seat_id, @travel_date::date, @from_station_id, @to_station_id)",
                    conn);

                // Добавляем параметры с правильными именами
                cmd.Parameters.AddWithValue("client_id", clientId);
                cmd.Parameters.AddWithValue("train_id", trainId);
                cmd.Parameters.AddWithValue("seat_id", seatId);
                cmd.Parameters.AddWithValue("travel_date", travelDate.Date);
                cmd.Parameters.AddWithValue("from_station_id", fromStationId);
                cmd.Parameters.AddWithValue("to_station_id", toStationId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    result["success"] = reader.GetBoolean("success");
                    result["message"] = reader.GetString("message");

                    // Проверяем на NULL перед получением значений
                    if (!reader.IsDBNull("ticket_id"))
                    {
                        result["ticket_id"] = reader.GetInt32("ticket_id");
                    }
                    else
                    {
                        result["ticket_id"] = null;
                    }

                    if (!reader.IsDBNull("total_price"))
                    {
                        result["total_price"] = reader.GetDecimal("total_price");
                    }
                    else
                    {
                        result["total_price"] = null;
                    }

                    _logger.LogInformation("Ticket booking result: Success={Success}, Message={Message}, TicketId={TicketId}",
                        result["success"], result["message"], result["ticket_id"]);
                }
                else
                {
                    _logger.LogWarning("book_ticket_if_available returned no results");
                    result["success"] = false;
                    result["message"] = "Функция не вернула результат";
                    result["ticket_id"] = null;
                    result["total_price"] = null;
                }
            }
            catch (PostgresException pgEx)
            {
                _logger.LogError(pgEx, "PostgreSQL error during ticket booking: {SqlState} - {MessageText}",
                    pgEx.SqlState, pgEx.MessageText);
                result["success"] = false;
                result["message"] = $"Ошибка базы данных: {pgEx.MessageText}";
                result["ticket_id"] = null;
                result["total_price"] = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "General error during ticket booking");
                result["success"] = false;
                result["message"] = "Помилка сервера при бронюванні квитка";
                result["ticket_id"] = null;
                result["total_price"] = null;
            }

            return result;
        }

        public async Task<bool> BookSeatAsync(int seatId, int clientId, DateTime travelDate)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT booked_seat_if_available(@seatId, @clientId, @travelDate)", conn);
                cmd.Parameters.AddWithValue("seatId", seatId);
                cmd.Parameters.AddWithValue("clientId", clientId);
                cmd.Parameters.AddWithValue("travelDate", travelDate.Date);

                var result = await cmd.ExecuteScalarAsync();
                return result is bool success && success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при бронюванні місця {SeatId} для клієнта {ClientId}", seatId, clientId);
                return false;
            }
        }


        public async Task<List<Dictionary<string, object>>> SearchTicketsByClientAsync(string clientQuery)
        {
            var tickets = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string sqlQuery = "SELECT * FROM search_tickets_by_client(@query)";
                using var cmd = new NpgsqlCommand(sqlQuery, conn);
                cmd.Parameters.AddWithValue("query", clientQuery);

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


        public async Task<Dictionary<string, object>> CancelTicketAsync(int ticketId)
        {
            var result = new Dictionary<string, object>();

            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // Виклик процедури
                using var cmd = new NpgsqlCommand("CALL cancel_ticket(@ticketId)", conn, transaction);
                cmd.Parameters.AddWithValue("ticketId", ticketId);
                await cmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                result["success"] = true;
                result["message"] = $"Квиток #{ticketId} успішно скасовано";
                _logger.LogInformation("Квиток {TicketId} скасовано менеджером", ticketId);
            }
            catch (PostgresException pgex)
            {
                await transaction.RollbackAsync();
                result["success"] = false;
                result["message"] = pgex.Message; // або обробити текст, щоб було зручно для користувача
                _logger.LogWarning(pgex, "Логічна помилка при скасуванні квитка {TicketId}", ticketId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                result["success"] = false;
                result["message"] = "Помилка сервера при скасуванні квитка";
                _logger.LogError(ex, "Помилка при скасуванні квитка {TicketId}", ticketId);
            }

            return result;
        }
        public async Task<List<Ticket>> GetActiveTicketsAsync(int clientId)
        {
            var tickets = new List<Ticket>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // ВИПРАВЛЕНО: використовуємо правильну назву параметра
                var cmd = new NpgsqlCommand("SELECT * FROM v_client_active_tickets_by_id(@p_client_id)", conn);
                cmd.Parameters.AddWithValue("p_client_id", clientId);

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
                                train_number = reader.GetInt32("train_number"),
                                // Ініціалізуємо StationSequences для часів
                                StationSequences = new List<StationSequence>()
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

                        Transactions = reader.GetBoolean("is_paid")
                            ? new List<Transactions> { new Transactions() }
                            : new List<Transactions>()
                    };

                    // ВАЖЛИВО: Отримуємо реальні часи з функції БД
                    var actualDepartureTime = reader.GetFieldValue<TimeSpan>("actual_departure_time");
                    var actualArrivalTime = reader.GetFieldValue<TimeSpan>("actual_arrival_time");

                    // Додаємо StationSequences для відображення часів у View
                    ticket.Schedule.Train.StationSequences.Add(new StationSequence
                    {
                        station_id = ticket.from_sequence_id,
                        train_id = ticket.Schedule.train_id,
                        travel_duration = actualDepartureTime,
                        Stations = ticket.FromStation
                    });

                    ticket.Schedule.Train.StationSequences.Add(new StationSequence
                    {
                        station_id = ticket.to_sequence_id,
                        train_id = ticket.Schedule.train_id,
                        travel_duration = actualArrivalTime,
                        Stations = ticket.ToStation
                    });

                    tickets.Add(ticket);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні активних квитків для клієнта {ClientId}", clientId);
                throw;
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

                // ВИПРАВЛЕНО: використовуємо правильну назву параметра
                var cmd = new NpgsqlCommand("SELECT * FROM v_client_historical_tickets_by_id(@p_client_id)", conn);
                cmd.Parameters.AddWithValue("p_client_id", clientId);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var ticket = new Ticket
                    {
                        // ... аналогічна логіка як у GetActiveTicketsAsync
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
                                train_number = reader.GetInt32("train_number"),
                                StationSequences = new List<StationSequence>()
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

                        Transactions = reader.GetBoolean("is_paid")
                            ? new List<Transactions> { new Transactions() }
                            : new List<Transactions>()
                    };

                    // Отримуємо реальні часи з функції БД
                    var actualDepartureTime = reader.GetFieldValue<TimeSpan>("actual_departure_time");
                    var actualArrivalTime = reader.GetFieldValue<TimeSpan>("actual_arrival_time");

                    // Додаємо StationSequences для відображення часів у View
                    ticket.Schedule.Train.StationSequences.Add(new StationSequence
                    {
                        station_id = ticket.from_sequence_id,
                        train_id = ticket.Schedule.train_id,
                        travel_duration = actualDepartureTime,
                        Stations = ticket.FromStation
                    });

                    ticket.Schedule.Train.StationSequences.Add(new StationSequence
                    {
                        station_id = ticket.to_sequence_id,
                        train_id = ticket.Schedule.train_id,
                        travel_duration = actualArrivalTime,
                        Stations = ticket.ToStation
                    });

                    tickets.Add(ticket);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні історичних квитків для клієнта {ClientId}", clientId);
                throw;
            }

            return tickets;
        }
        //public async Task<List<Ticket>> GetActiveTicketsAsync(int clientId)
        //{
        //    var tickets = new List<Ticket>();

        //    try
        //    {
        //        using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //        await conn.OpenAsync();

        //        // Устанавливаем контекст для безопасности доступа
        //        string setContextSql = $"SET search_path TO public; SET app.current_client = {clientId};";
        //        using (var setCmd = new NpgsqlCommand(setContextSql, conn))
        //        {
        //            await setCmd.ExecuteNonQueryAsync();
        //        }

        //        // Используем представление для получения данных с ограничением доступа
        //        //var cmd = new NpgsqlCommand("SELECT * FROM v_client_active_tickets ORDER BY date ASC, departure_time ASC", conn);
        //        var cmd = new NpgsqlCommand("SELECT * FROM v_client_active_tickets_by_id(@clientId)", conn);
        //        using var reader = await cmd.ExecuteReaderAsync();

        //        while (await reader.ReadAsync())
        //        {
        //            var ticket = new Ticket
        //            {
        //                ticket_id = reader.GetInt32("ticket_id"),
        //                seat_id = reader.GetInt32("seat_id"),
        //                client_id = reader.GetInt32("client_id"),
        //                schedule_id = reader.GetInt32("schedule_id"),
        //                booking_date = reader.GetDateTime("booking_date"),
        //                total_price = reader.GetDecimal("total_price"),
        //                from_sequence_id = reader.GetInt32("from_sequence_id"),
        //                to_sequence_id = reader.GetInt32("to_sequence_id"),

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
        //                    schedule_id = reader.GetInt32("schedule_id"),
        //                    train_id = reader.GetInt32("train_id"),
        //                    date = reader.GetDateTime("date"),
        //                    departure_time = reader.GetFieldValue<TimeSpan>("departure_time"),
        //                    Train = new Train
        //                    {
        //                        train_id = reader.GetInt32("train_id"),
        //                        train_number = reader.GetInt32("train_number")
        //                    }
        //                },

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

        //                // Определяем статус оплаты
        //                Transactions = reader.GetBoolean("is_paid")
        //                    ? new List<Transactions> { new Transactions() }
        //                    : new List<Transactions>()
        //            };

        //            // ПОЛУЧАЕМ РАСЧЕТНОЕ ВРЕМЯ ОТПРАВЛЕНИЯ И ПРИБЫТИЯ ИЗ ПРЕДСТАВЛЕНИЯ
        //            var actualDepartureTime = reader.GetFieldValue<TimeSpan>("actual_departure_time");
        //            var actualArrivalTime = reader.GetFieldValue<TimeSpan>("actual_arrival_time");

        //            // Инициализируем коллекцию если её нет
        //            if (ticket.Schedule.Train.StationSequences == null)
        //                ticket.Schedule.Train.StationSequences = new List<StationSequence>();

        //            // Создаем StationSequence для станции отправления
        //            var departureSequence = new StationSequence
        //            {
        //                station_id = ticket.from_sequence_id,
        //                train_id = ticket.Schedule.train_id,
        //                travel_duration = TimeSpan.Zero, // Время в пути от начальной станции до этой
        //                Stations = ticket.FromStation
        //            };

        //            // Создаем StationSequence для станции прибытия  
        //            var arrivalSequence = new StationSequence
        //            {
        //                station_id = ticket.to_sequence_id,
        //                train_id = ticket.Schedule.train_id,
        //                travel_duration = actualArrivalTime - actualDepartureTime, // Время в пути между станциями
        //                Stations = ticket.ToStation
        //            };

        //            ticket.Schedule.Train.StationSequences.Add(departureSequence);
        //            ticket.Schedule.Train.StationSequences.Add(arrivalSequence);

        //            // СОХРАНЯЕМ ВРЕМЕНА В ДИНАМИЧЕСКИХ СВОЙСТВАХ (используем ViewBag подход)
        //            ticket.Schedule.Train.StationSequences.First().travel_duration = actualDepartureTime;
        //            ticket.Schedule.Train.StationSequences.Last().travel_duration = actualArrivalTime;

        //            tickets.Add(ticket);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Ошибка при получении активных билетов для клиента {ClientId}", clientId);
        //        throw; // Перебрасываем исключение для обработки на верхнем уровне
        //    }
        //    finally
        //    {
        //        // Очищаем контекст (опционально)
        //        try
        //        {
        //            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //            await conn.OpenAsync();
        //            using var resetCmd = new NpgsqlCommand("RESET app.current_client", conn);
        //            await resetCmd.ExecuteNonQueryAsync();
        //        }
        //        catch
        //        {
        //            // Игнорируем ошибки сброса контекста
        //        }
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

        //        // Устанавливаем контекст для безопасности доступа
        //        string setContextSql = $"SET search_path TO public; SET app.current_client = {clientId};";
        //        using (var setCmd = new NpgsqlCommand(setContextSql, conn))
        //        {
        //            await setCmd.ExecuteNonQueryAsync();
        //        }

        //        // Используем представление для получения исторических данных с ограничением доступа
        //        var cmd = new NpgsqlCommand("SELECT * FROM v_client_historical_tickets ORDER BY date DESC, departure_time DESC", conn);
        //        using var reader = await cmd.ExecuteReaderAsync();

        //        while (await reader.ReadAsync())
        //        {
        //            var ticket = new Ticket
        //            {
        //                ticket_id = reader.GetInt32("ticket_id"),
        //                seat_id = reader.GetInt32("seat_id"),
        //                client_id = reader.GetInt32("client_id"),
        //                schedule_id = reader.GetInt32("schedule_id"),
        //                booking_date = reader.GetDateTime("booking_date"),
        //                total_price = reader.GetDecimal("total_price"),
        //                from_sequence_id = reader.GetInt32("from_sequence_id"),
        //                to_sequence_id = reader.GetInt32("to_sequence_id"),

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
        //                    schedule_id = reader.GetInt32("schedule_id"),
        //                    train_id = reader.GetInt32("train_id"),
        //                    date = reader.GetDateTime("date"),
        //                    departure_time = reader.GetFieldValue<TimeSpan>("departure_time"),
        //                    Train = new Train
        //                    {
        //                        train_id = reader.GetInt32("train_id"),
        //                        train_number = reader.GetInt32("train_number")
        //                    }
        //                },

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

        //                // Определяем статус оплаты для исторических билетов
        //                Transactions = reader.GetBoolean("is_paid")
        //                    ? new List<Transactions> { new Transactions() }
        //                    : new List<Transactions>()
        //            };

        //            // ПОЛУЧАЕМ РАСЧЕТНОЕ ВРЕМЯ ОТПРАВЛЕНИЯ И ПРИБЫТИЯ ИЗ ПРЕДСТАВЛЕНИЯ
        //            var actualDepartureTime = reader.GetFieldValue<TimeSpan>("actual_departure_time");
        //            var actualArrivalTime = reader.GetFieldValue<TimeSpan>("actual_arrival_time");

        //            // Инициализируем коллекцию если её нет
        //            if (ticket.Schedule.Train.StationSequences == null)
        //                ticket.Schedule.Train.StationSequences = new List<StationSequence>();

        //            // Создаем StationSequence для станции отправления
        //            var departureSequence = new StationSequence
        //            {
        //                station_id = ticket.from_sequence_id,
        //                train_id = ticket.Schedule.train_id,
        //                travel_duration = TimeSpan.Zero, // Время в пути от начальной станции до этой
        //                Stations = ticket.FromStation
        //            };

        //            // Создаем StationSequence для станции прибытия  
        //            var arrivalSequence = new StationSequence
        //            {
        //                station_id = ticket.to_sequence_id,
        //                train_id = ticket.Schedule.train_id,
        //                travel_duration = actualArrivalTime - actualDepartureTime, // Время в пути между станциями
        //                Stations = ticket.ToStation
        //            };

        //            ticket.Schedule.Train.StationSequences.Add(departureSequence);
        //            ticket.Schedule.Train.StationSequences.Add(arrivalSequence);

        //            // СОХРАНЯЕМ ВРЕМЕНА В ДИНАМИЧЕСКИХ СВОЙСТВАХ (используем ViewBag подход)
        //            ticket.Schedule.Train.StationSequences.First().travel_duration = actualDepartureTime;
        //            ticket.Schedule.Train.StationSequences.Last().travel_duration = actualArrivalTime;

        //            tickets.Add(ticket);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Ошибка при получении исторических билетов для клиента {ClientId}", clientId);
        //        throw; // Перебрасываем исключение для обработки на верхнем уровне
        //    }
        //    finally
        //    {
        //        // Очищаем контекст (опционально)
        //        try
        //        {
        //            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //            await conn.OpenAsync();
        //            using var resetCmd = new NpgsqlCommand("RESET app.current_client", conn);
        //            await resetCmd.ExecuteNonQueryAsync();
        //        }
        //        catch
        //        {
        //            // Игнорируем ошибки сброса контекста
        //        }
        //    }

        //    return tickets;
        //}

        //public async Task<List<Ticket>> GetActiveTicketsAsync(int clientId)
        //{
        //    var tickets = new List<Ticket>();

        //    try
        //    {
        //        using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //        await conn.OpenAsync();

        //        // Устанавливаем контекст для безопасности доступа
        //        string setContextSql = $"SET search_path TO public; SET app.current_client = {clientId};";
        //        using (var setCmd = new NpgsqlCommand(setContextSql, conn))
        //        {
        //            await setCmd.ExecuteNonQueryAsync();
        //        }

        //        // Используем представление для получения данных с ограничением доступа
        //        var cmd = new NpgsqlCommand("SELECT * FROM v_client_active_tickets ORDER BY date ASC, departure_time ASC", conn);
        //        using var reader = await cmd.ExecuteReaderAsync();

        //        while (await reader.ReadAsync())
        //        {
        //            var ticket = new Ticket
        //            {
        //                ticket_id = reader.GetInt32("ticket_id"),
        //                seat_id = reader.GetInt32("seat_id"),
        //                client_id = reader.GetInt32("client_id"),
        //                schedule_id = reader.GetInt32("schedule_id"),
        //                booking_date = reader.GetDateTime("booking_date"),
        //                total_price = reader.GetDecimal("total_price"),
        //                from_sequence_id = reader.GetInt32("from_sequence_id"),
        //                to_sequence_id = reader.GetInt32("to_sequence_id"),

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
        //                    schedule_id = reader.GetInt32("schedule_id"),
        //                    train_id = reader.GetInt32("train_id"),
        //                    date = reader.GetDateTime("date"),
        //                    departure_time = reader.GetFieldValue<TimeSpan>("departure_time"),
        //                    Train = new Train
        //                    {
        //                        train_id = reader.GetInt32("train_id"),
        //                        train_number = reader.GetInt32("train_number")
        //                    }
        //                },

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

        //                // Определяем статус оплаты
        //                Transactions = reader.GetBoolean("is_paid")
        //                    ? new List<Transactions> { new Transactions() }
        //                    : new List<Transactions>()
        //            };

        //            // Добавляем информацию о времени отправления и прибытия
        //            if (ticket.Schedule.Train.StationSequences == null)
        //                ticket.Schedule.Train.StationSequences = new List<StationSequence>();

        //            ticket.Schedule.Train.StationSequences.Add(new StationSequence
        //            {
        //                station_id = ticket.from_sequence_id,
        //                train_id = ticket.Schedule.train_id,
        //                Stations = ticket.FromStation
        //            });

        //            ticket.Schedule.Train.StationSequences.Add(new StationSequence
        //            {
        //                station_id = ticket.to_sequence_id,
        //                train_id = ticket.Schedule.train_id,
        //                Stations = ticket.ToStation
        //            });

        //            tickets.Add(ticket);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Ошибка при получении активных билетов для клиента {ClientId}", clientId);
        //        throw; // Перебрасываем исключение для обработки на верхнем уровне
        //    }
        //    finally
        //    {
        //        // Очищаем контекст (опционально)
        //        try
        //        {
        //            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //            await conn.OpenAsync();
        //            using var resetCmd = new NpgsqlCommand("RESET app.current_client", conn);
        //            await resetCmd.ExecuteNonQueryAsync();
        //        }
        //        catch
        //        {
        //            // Игнорируем ошибки сброса контекста
        //        }
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

        //        // Устанавливаем контекст для безопасности доступа
        //        string setContextSql = $"SET search_path TO public; SET app.current_client = {clientId};";
        //        using (var setCmd = new NpgsqlCommand(setContextSql, conn))
        //        {
        //            await setCmd.ExecuteNonQueryAsync();
        //        }

        //        // Используем представление для получения исторических данных с ограничением доступа
        //        var cmd = new NpgsqlCommand("SELECT * FROM v_client_historical_tickets ORDER BY date DESC, departure_time DESC", conn);
        //        using var reader = await cmd.ExecuteReaderAsync();

        //        while (await reader.ReadAsync())
        //        {
        //            var ticket = new Ticket
        //            {
        //                ticket_id = reader.GetInt32("ticket_id"),
        //                seat_id = reader.GetInt32("seat_id"),
        //                client_id = reader.GetInt32("client_id"),
        //                schedule_id = reader.GetInt32("schedule_id"),
        //                booking_date = reader.GetDateTime("booking_date"),
        //                total_price = reader.GetDecimal("total_price"),
        //                from_sequence_id = reader.GetInt32("from_sequence_id"),
        //                to_sequence_id = reader.GetInt32("to_sequence_id"),

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
        //                    schedule_id = reader.GetInt32("schedule_id"),
        //                    train_id = reader.GetInt32("train_id"),
        //                    date = reader.GetDateTime("date"),
        //                    departure_time = reader.GetFieldValue<TimeSpan>("departure_time"),
        //                    Train = new Train
        //                    {
        //                        train_id = reader.GetInt32("train_id"),
        //                        train_number = reader.GetInt32("train_number")
        //                    }
        //                },

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

        //                // Определяем статус оплаты для исторических билетов
        //                Transactions = reader.GetBoolean("is_paid")
        //                    ? new List<Transactions> { new Transactions() }
        //                    : new List<Transactions>()
        //            };

        //            // Добавляем информацию о маршруте
        //            if (ticket.Schedule.Train.StationSequences == null)
        //                ticket.Schedule.Train.StationSequences = new List<StationSequence>();

        //            ticket.Schedule.Train.StationSequences.Add(new StationSequence
        //            {
        //                station_id = ticket.from_sequence_id,
        //                train_id = ticket.Schedule.train_id,
        //                Stations = ticket.FromStation
        //            });

        //            ticket.Schedule.Train.StationSequences.Add(new StationSequence
        //            {
        //                station_id = ticket.to_sequence_id,
        //                train_id = ticket.Schedule.train_id,
        //                Stations = ticket.ToStation
        //            });

        //            tickets.Add(ticket);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Ошибка при получении исторических билетов для клиента {ClientId}", clientId);
        //        throw; // Перебрасываем исключение для обработки на верхнем уровне
        //    }
        //    finally
        //    {
        //        // Очищаем контекст (опционально)
        //        try
        //        {
        //            using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //            await conn.OpenAsync();
        //            using var resetCmd = new NpgsqlCommand("RESET app.current_client", conn);
        //            await resetCmd.ExecuteNonQueryAsync();
        //        }
        //        catch
        //        {
        //            // Игнорируем ошибки сброса контекста
        //        }
        //    }

        //    return tickets;
        //}
    }
}
//public async Task<Dictionary<string, object>> GetTicketInfoAsync(int trainId, DateTime date, int seatId, int fromStationId, int toStationId)