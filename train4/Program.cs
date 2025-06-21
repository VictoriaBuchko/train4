using Microsoft.EntityFrameworkCore;
using train2.Data;
using train4.Services.Implementation;
using train4.Services.Interfaces;

namespace train2
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Додати підтримку сесій
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Додати доступ до HttpContext
            builder.Services.AddHttpContextAccessor();

            // Реєстрація сервісів
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            builder.Services.AddScoped<IStationService, StationService>();
            builder.Services.AddScoped<ITrainService, TrainService>();
            builder.Services.AddScoped<IScheduleService, ScheduleService>();
            builder.Services.AddScoped<ITicketService, TicketService>();
            builder.Services.AddScoped<IAccountingService, AccountingService>();
            builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
            builder.Services.AddScoped<IScheduleRouteService, ScheduleRouteService>();

            // Динамічне формування connection string для RailwayDbContext
            builder.Services.AddScoped(provider =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
                var session = httpContextAccessor.HttpContext?.Session;

                string baseConnectionString = configuration.GetConnectionString("DefaultConnection");

                var builder = new Npgsql.NpgsqlConnectionStringBuilder(baseConnectionString)
                {
                    Username = session?.GetString("db_user") ?? "guest_user",
                    Password = session?.GetString("db_password") ?? "guest123"
                };

                var optionsBuilder = new DbContextOptionsBuilder<RailwayDbContext>();
                optionsBuilder.UseNpgsql(builder.ToString());

                return new RailwayDbContext(optionsBuilder.Options);
            });

            // MVC
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseSession(); // має бути між UseRouting і UseAuthorization
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}


//using Microsoft.EntityFrameworkCore;
//using train2.Data;
//using train2.Services;
//using train4.Services.Implementation;
//using train4.Services.Interfaces;

//namespace train2
//{
//    public class Program
//    {
//        public static void Main(string[] args)
//        {
//            var builder = WebApplication.CreateBuilder(args);
//            // Налаштування сесій
//            builder.Services.AddDistributedMemoryCache();
//            builder.Services.AddSession(options =>
//            {
//                options.IdleTimeout = TimeSpan.FromMinutes(30);
//                options.Cookie.HttpOnly = true;
//                options.Cookie.IsEssential = true;
//            });
//            // Необхідно для доступу до HttpContext в сервісах
//            builder.Services.AddHttpContextAccessor();

//            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
//            builder.Services.AddScoped<IStationService, StationService>();
//            builder.Services.AddScoped<ITrainService, TrainService>();
//            builder.Services.AddScoped<IScheduleService, ScheduleService>();
//            builder.Services.AddScoped<ITicketService, TicketService>();
//            builder.Services.AddScoped<IAccountingService, AccountingService>();
//            builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();


//            //// Реєстрація сервісу роботи з базою даних
//            //builder.Services.AddScoped<IDatabaseService, PostgreSqlDatabaseService>();
//            //// Реєстрація сервісу роботи з квитками
//            //builder.Services.AddScoped<ITicketService, PostgreSqlTicketService>();
//            // Додаємо DbContext з динамічними параметрами підключення (для роботи через сесію)
//            builder.Services.AddScoped(provider =>
//            {
//                var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
//                var session = httpContextAccessor.HttpContext?.Session;
//                string login = session?.GetString("db_user") ?? "guest_user";
//                string password = session?.GetString("db_password") ?? "guest123";
//                var optionsBuilder = new DbContextOptionsBuilder<RailwayDbContext>();
//                optionsBuilder.UseNpgsql($"Host=localhost;Port=5432;Database=TrainTicketDb2;Username={login};Password={password}");
//                return new RailwayDbContext(optionsBuilder.Options);
//            });
//            builder.Services.AddControllersWithViews();
//            var app = builder.Build();
//            if (!app.Environment.IsDevelopment())
//            {
//                app.UseExceptionHandler("/Home/Error");
//                app.UseHsts();
//            }
//            app.UseHttpsRedirection();
//            app.UseStaticFiles();
//            app.UseRouting();
//            // Важливо: UseSession має бути після UseRouting, але перед UseEndpoints
//            app.UseSession();
//            app.UseAuthorization();
//            app.MapControllerRoute(
//                name: "default",
//                pattern: "{controller=Home}/{action=Index}/{id?}");
//            app.Run();
//        }
//    }
//}