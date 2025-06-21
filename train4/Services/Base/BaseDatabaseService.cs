using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace train4.Services.Base
{
    public abstract class BaseDatabaseService
    {
        protected readonly IConfiguration _configuration;
        protected readonly IHttpContextAccessor _httpContextAccessor;
        protected readonly ILogger _logger;

        // Отримання значень із конфігурації
        protected string DefaultHost => _configuration["DatabaseDefaults:Host"] ?? "localhost";
        protected string DefaultPort => _configuration["DatabaseDefaults:Port"] ?? "5432";
        protected string DefaultDatabase => _configuration["DatabaseDefaults:Database"] ?? "TrainTicketDb2";
        protected string GuestUser => _configuration["DatabaseDefaults:GuestUser"] ?? "guest_user";
        protected string GuestPassword => _configuration["DatabaseDefaults:GuestPassword"] ?? "guest123";

        protected BaseDatabaseService(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        protected string GetCurrentConnectionString()
        {
            var session = _httpContextAccessor.HttpContext?.Session;

            string login = session?.GetString("db_user") ?? GuestUser;
            string password = session?.GetString("db_password") ?? GuestPassword;

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = DefaultHost,
                Port = int.TryParse(DefaultPort, out var port) ? port : 5432,
                Database = DefaultDatabase,
                Username = login,
                Password = password,
            };

            return builder.ToString();
        }

    }
}
