using Npgsql;
using System.Data;
using train4.Services.Base;

namespace train4.Services.Implementation
{
    public class AccountingService : BaseDatabaseService, Interfaces.IAccountingService
    {
        public AccountingService(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AccountingService> logger)
            : base(configuration, httpContextAccessor, logger) { }

        public async Task<List<Dictionary<string, object>>> GetUnpaidTicketsAsync(int page, int pageSize, string searchQuery = "")
        {
            var tickets = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT * FROM get_unpaid_tickets(@search, @limit, @offset)", conn);
                cmd.Parameters.AddWithValue("search", searchQuery ?? string.Empty);
                cmd.Parameters.AddWithValue("limit", pageSize);
                cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var ticket = new Dictionary<string, object>
                    {
                        ["ticket_id"] = reader.GetInt32(reader.GetOrdinal("ticket_id")),
                        ["booking_date"] = reader.GetDateTime(reader.GetOrdinal("booking_date")),
                        ["total_price"] = reader.GetDecimal(reader.GetOrdinal("total_price")),
                        ["client_full_name"] = reader.GetString(reader.GetOrdinal("client_full_name")),
                        ["client_login"] = reader.GetString(reader.GetOrdinal("client_login")),
                        ["client_phone"] = reader.IsDBNull(reader.GetOrdinal("client_phone")) ? "" : reader.GetString(reader.GetOrdinal("client_phone")),
                        ["client_email"] = reader.IsDBNull(reader.GetOrdinal("client_email")) ? "" : reader.GetString(reader.GetOrdinal("client_email")),
                        ["train_number"] = reader.GetInt32(reader.GetOrdinal("train_number")),
                        ["travel_date"] = reader.GetDateTime(reader.GetOrdinal("travel_date")),
                        ["departure_time"] = reader.GetTimeSpan(reader.GetOrdinal("departure_time")),
                        ["from_station"] = reader.GetString(reader.GetOrdinal("from_station")),
                        ["to_station"] = reader.GetString(reader.GetOrdinal("to_station")),
                        ["seat_number"] = reader.GetInt32(reader.GetOrdinal("seat_number")),
                        ["carriage_number"] = reader.GetInt32(reader.GetOrdinal("carriage_number")),
                        ["carriage_type"] = reader.GetString(reader.GetOrdinal("carriage_type")),
                        ["days_since_booking"] = reader.GetInt32(reader.GetOrdinal("days_since_booking"))
                    };

                    tickets.Add(ticket);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні неоплачених квитків через функцію");
            }

            return tickets;
        }


        public async Task<int> GetUnpaidTicketsCountAsync(string searchQuery = "")
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT get_unpaid_tickets_count(@search)", conn);
                cmd.Parameters.AddWithValue("search", searchQuery ?? string.Empty);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при підрахунку неоплачених квитків через функцію");
                return 0;
            }
        }


        public async Task<Dictionary<string, object>> ConfirmTicketPaymentAsync(int ticketId, decimal paidAmount, string paymentMethod)
        {
            var result = new Dictionary<string, object>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // Используем функцию вместо процедуры
                using var cmd = new NpgsqlCommand(
                    "SELECT * FROM confirm_ticket_payment_func(@ticketId, @paidAmount, @paymentMethod)",
                    conn);

                cmd.Parameters.AddWithValue("ticketId", ticketId);
                cmd.Parameters.AddWithValue("paidAmount", paidAmount);
                cmd.Parameters.AddWithValue("paymentMethod", paymentMethod);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    result["success"] = reader.GetBoolean("success");
                    result["message"] = reader.GetString("message");

                    if ((bool)result["success"])
                    {
                        result["transaction_id"] = reader.IsDBNull("transaction_id") ?
                            null : reader.GetInt32("transaction_id");
                        result["total_price"] = reader.IsDBNull("expected_price") ?
                            null : reader.GetDecimal("expected_price");
                        result["change"] = reader.IsDBNull("change") ?
                            null : reader.GetDecimal("change");

                        _logger.LogInformation("Бухгалтер підтвердив оплату квитка {TicketId} на суму {Amount} грн",
                            ticketId, result["total_price"]);
                    }
                    else
                    {
                        _logger.LogWarning("Не вдалося підтвердити оплату квитка {TicketId}: {Message}",
                            ticketId, result["message"]);

                        // Устанавливаем null для неуспешных случаев
                        result["transaction_id"] = null;
                        result["total_price"] = reader.IsDBNull("expected_price") ?
                            null : reader.GetDecimal("expected_price");
                        result["change"] = null;
                    }
                }
                else
                {
                    result["success"] = false;
                    result["message"] = "Функция не вернула результат";
                    result["transaction_id"] = null;
                    result["total_price"] = null;
                    result["change"] = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при підтвердженні оплати квитка {TicketId}", ticketId);
                result["success"] = false;
                result["message"] = "Помилка сервера при підтвердженні оплати";
                result["transaction_id"] = null;
                result["total_price"] = null;
                result["change"] = null;
            }

            return result;
        }

        public async Task<Dictionary<string, object>> GetFinancialReportAsync(DateTime startDate, DateTime endDate, string reportType)
        {
            var report = new Dictionary<string, object>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                // 📌 ВЫЗОВ ФУНКЦИИ ДЛЯ ОСНОВНОГО ОТЧЕТА
                using var summaryCmd = new NpgsqlCommand("SELECT * FROM get_summary_report(@startDate, @endDate)", conn);
                summaryCmd.Parameters.AddWithValue("startDate", startDate.Date);
                summaryCmd.Parameters.AddWithValue("endDate", endDate.Date.AddDays(1).AddTicks(-1));

                using var reader = await summaryCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    report["summary"] = new Dictionary<string, object>
                    {
                        ["total_tickets_booked"] = Convert.ToInt32(reader.GetInt64("total_tickets_booked")),
                        ["total_amount_booked"] = reader.GetDecimal("total_amount_booked"),
                        ["total_tickets_paid"] = Convert.ToInt32(reader.GetInt64("total_tickets_paid")),
                        ["total_amount_paid"] = reader.GetDecimal("total_amount_paid"),
                        ["unpaid_tickets"] = Convert.ToInt32(reader.GetInt64("unpaid_tickets")),
                        ["unpaid_amount"] = reader.GetDecimal("unpaid_amount"),
                        ["period_start"] = startDate.ToString("yyyy-MM-dd"),
                        ["period_end"] = endDate.ToString("yyyy-MM-dd")
                    };
                }

                if (reportType == "detailed" || reportType == "trains")
                {
                    await reader.CloseAsync();

                    // 📌 ВЫЗОВ ФУНКЦИИ ДЛЯ ДЕТАЛЬНОГО ОТЧЕТА ПО ПОЕЗДАМ
                    using var trainsCmd = new NpgsqlCommand("SELECT * FROM get_train_stats_report(@startDate, @endDate)", conn);
                    trainsCmd.Parameters.AddWithValue("startDate", startDate.Date);
                    trainsCmd.Parameters.AddWithValue("endDate", endDate.Date.AddDays(1).AddTicks(-1));

                    var trainStats = new List<Dictionary<string, object>>();
                    using var trainsReader = await trainsCmd.ExecuteReaderAsync();
                    while (await trainsReader.ReadAsync())
                    {
                        trainStats.Add(new Dictionary<string, object>
                        {
                            ["train_number"] = trainsReader.GetInt32("train_number"),
                            ["tickets_booked"] = Convert.ToInt32(trainsReader.GetInt64("tickets_booked")),
                            ["amount_booked"] = trainsReader.GetDecimal("amount_booked"),
                            ["tickets_paid"] = Convert.ToInt32(trainsReader.GetInt64("tickets_paid")),
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



        public async Task<List<Dictionary<string, object>>> GetDailyStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            var statistics = new List<Dictionary<string, object>>();

            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = "SELECT * FROM get_daily_ticket_statistics(@startDate::date, @endDate::date)";


                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("startDate", startDate.Date);
                cmd.Parameters.AddWithValue("endDate", endDate.Date);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    statistics.Add(new Dictionary<string, object>
                    {
                        ["date"] = reader.GetDateTime(reader.GetOrdinal("date")).ToString("yyyy-MM-dd"),
                        ["tickets_booked"] = reader.GetInt32(reader.GetOrdinal("tickets_booked")),
                        ["amount_booked"] = reader.GetDecimal(reader.GetOrdinal("amount_booked")),
                        ["tickets_paid"] = reader.GetInt32(reader.GetOrdinal("tickets_paid")),
                        ["amount_paid"] = reader.GetDecimal(reader.GetOrdinal("amount_paid"))
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


        public async Task<List<Dictionary<string, object>>> GetDetailedFinancialReportAsync(DateTime startDate, DateTime endDate)
        {
            var reportData = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string query = "SELECT * FROM get_detailed_financial_report(@startDate, @endDate)";
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
                        ["payment_date"] = reader.IsDBNull("payment_date")
                            ? ""
                            : reader.GetDateTime("payment_date").ToString("yyyy-MM-dd")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні детального фінансового звіту для періоду {StartDate} - {EndDate}",
                    startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
                throw;
            }
            return reportData;
        }

    }
}