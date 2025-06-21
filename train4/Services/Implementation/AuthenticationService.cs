using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using train2.Models;
using train4.Services.Base;
using train4.Services.Interfaces;


namespace train4.Services.Implementation
{
    public class AuthenticationService : BaseDatabaseService, Interfaces.IAuthenticationService
    {
        public AuthenticationService(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuthenticationService> logger)
            : base(configuration, httpContextAccessor, logger) { }

        public async Task<(bool Success, string ErrorMessage)> ConnectAsync(string login, string password, string email = null)
        {
            try
            {
                // 1. Побудова нового рядка підключення без пулінгу
                var builder = new Npgsql.NpgsqlConnectionStringBuilder
                {
                    Host = DefaultHost,
                    Port = int.TryParse(DefaultPort, out var port) ? port : 5432,
                    Database = DefaultDatabase,
                    Username = login,
                    Password = password,
                    Pooling = false // ⬅️ це головне
                };

                using var conn = new NpgsqlConnection(builder.ToString());

                // 2. Відкрити з'єднання (воно точно нове, буде в логах)
                await conn.OpenAsync();

                // 3. Перевірка користувача
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

                // 4. Зберегти авторизацію в сесію
                var session = _httpContextAccessor.HttpContext?.Session;
                session?.SetString("db_user", login);
                session?.SetString("db_password", password);

                // 5. Отримати коротку інформацію про клієнта
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
        public async Task LogoutAsync()
        {
            try
            {
                // 1. Очистити сесію
                _httpContextAccessor.HttpContext?.Session?.Clear();

                // 2. Побудувати рядок підключення з Pooling=false
                var builder = new Npgsql.NpgsqlConnectionStringBuilder
                {
                    Host = DefaultHost,
                    Port = int.TryParse(DefaultPort, out var port) ? port : 5432,
                    Database = DefaultDatabase,
                    Username = GuestUser,
                    Password = GuestPassword,
                    Pooling = false // ⬅️ ключова частина
                };

                // 3. Відкрити нове з’єднання, яке гарантовано потрапить у логи PostgreSQL
                using var conn = new NpgsqlConnection(builder.ToString());
                await conn.OpenAsync();

                // 4. Виконати запит (щоб з’єднання не закрилось моментально без активності)
                using var cmd = new NpgsqlCommand("SELECT current_user", conn);
                var guestUser = (await cmd.ExecuteScalarAsync())?.ToString();

                // 5. Лог в твоїй системі
                _logger.LogInformation($"Вихід з системи. Тепер підключено як {guestUser}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при виході з системи");
            }
        }


        //public async Task<Client?> GetClientByLoginAsync(string login)
        //{
        //    try
        //    {
        //        using var connection = new NpgsqlConnection(GetCurrentConnectionString());
        //        await connection.OpenAsync();

        //        string query = @"
        //                         SELECT client_id, client_name, client_surname, login
        //                         FROM my_client_info
        //                         LIMIT 1";

        //        using var command = new NpgsqlCommand(query, connection);
        //        using var reader = await command.ExecuteReaderAsync();

        //        if (await reader.ReadAsync())
        //        {
        //            return new Client
        //            {
        //                client_id = reader.GetInt32(reader.GetOrdinal("client_id")),
        //                client_name = reader.GetString(reader.GetOrdinal("client_name")),
        //                client_surname = reader.GetString(reader.GetOrdinal("client_surname")),
        //                login = reader.GetString(reader.GetOrdinal("login")),

        //                // Інші поля залишаємо порожніми або null
        //                client_patronymic = null,
        //                email = null,
        //                phone_number = null,
        //                payment_info = null
        //            };
        //        }

        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Помилка при отриманні клієнта через представлення my_client_info");
        //        return null;
        //    }
        //}
        public async Task<Client> GetClientByLoginAsync(string login)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();


                const string query = @"
        SELECT client_id, client_name, client_surname, client_patronymic, 
               email, phone_number, payment_info, login
        FROM my_full_client_info";
                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("login", login);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var client = new Client
                    {
                        client_id = reader.GetInt32("client_id"),
                        client_name = reader.GetString("client_name"),
                        client_surname = reader.GetString("client_surname"),
                        client_patronymic = reader.IsDBNull("client_patronymic") ? null : reader.GetString("client_patronymic"),
                        email = reader.IsDBNull("email") ? null : reader.GetString("email"),
                        phone_number = reader.IsDBNull("phone_number") ? null : reader.GetString("phone_number"),
                        payment_info = reader.IsDBNull("payment_info") ? null : reader.GetString("payment_info"),
                        login = reader.GetString("login")
                    };

                    _logger.LogInformation("Retrieved client from DB: Name={Name}, Surname={Surname}, Patronymic={Patronymic}, Phone={Phone}, Email={Email}, PaymentInfo={PaymentInfo}",
                        client.client_name, client.client_surname, client.client_patronymic, client.phone_number, client.email, client.payment_info);

                    return client;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні клієнта за логіном {Login}", login);
                return null;
            }
        }
        //public async Task<(bool Success, string ErrorMessage)> UpdateClientAsync(Client client)
        //{
        //    try
        //    {
        //        using var conn = new NpgsqlConnection(GetCurrentConnectionString());
        //        await conn.OpenAsync();

        //        string query = "SELECT update_client_info(@client_id, @client_name, @client_surname, @client_email)";

        //        using var cmd = new NpgsqlCommand(query, conn);
        //        cmd.Parameters.AddWithValue("client_id", client.client_id);
        //        cmd.Parameters.AddWithValue("client_name", client.client_name);
        //        cmd.Parameters.AddWithValue("client_surname", client.client_surname);
        //        cmd.Parameters.AddWithValue("client_email", (object?)client.email ?? DBNull.Value);

        //        await cmd.ExecuteNonQueryAsync();

        //        return (true, null);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Помилка при оновленні даних клієнта через функцію");
        //        return (false, "Помилка при оновленні даних клієнта");
        //    }
        //}


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


        //работает запрос, предоставила доступ администратору 
        public async Task<List<Client>> SearchClientsAsync(string query) 
        {
            var clients = new List<Client>();
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();

                string sql = "SELECT * FROM search_clients(@query)";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("query", query);


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

        public async Task<(bool Success, string ErrorMessage)> UpdateClientAsync(Client client)
        {
            try
            {
                using var conn = new NpgsqlConnection(GetCurrentConnectionString());
                await conn.OpenAsync();


                //    const string query = @"
                //UPDATE client 
                //SET client_name = @name, 
                //    client_surname = @surname, 
                //    client_patronymic = @patronymic,
                //    email = @email, 
                //    phone_number = @phone, 
                //    payment_info = @paymentInfo
                //WHERE client_id = @clientId";
                const string query = "SELECT update_client_profile(@clientId, @name, @surname, @patronymic, @email, @phone, @paymentInfo)";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("name", client.client_name);
                cmd.Parameters.AddWithValue("surname", client.client_surname);
                cmd.Parameters.AddWithValue("patronymic", client.client_patronymic ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("email", client.email ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("phone", client.phone_number ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("paymentInfo", client.payment_info ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("clientId", client.client_id);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Профіль клієнта {ClientId} успішно оновлено", client.client_id);
                    return (true, null);
                }
                else
                {
                    _logger.LogWarning("Клієнта з ID {ClientId} не знайдено для оновлення", client.client_id);
                    return (false, "Клієнта не знайдено");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при оновленні профілю клієнта {ClientId}", client.client_id);
                return (false, "Помилка при оновленні профілю");
            }
        }

        public async Task<bool> ValidateUserCredentialsAsync(string login, string email)
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = DefaultHost,
                    Port = int.TryParse(DefaultPort, out var port) ? port : 5432,
                    Database = DefaultDatabase,
                    Username = GuestUser,
                    Password = GuestPassword,
                    Pooling = false
                };

                using var conn = new NpgsqlConnection(builder.ToString());
                await conn.OpenAsync();

                // Используем представление вместо прямого доступа к таблице
                const string query = @"
            SELECT COUNT(*) 
            FROM v_client_login_email_check
            WHERE login = @login AND email = @email";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("login", login);
                cmd.Parameters.AddWithValue("email", email);

                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при перевірці даних користувача {Login}", login);
                return false;
            }
        }

    }
}
