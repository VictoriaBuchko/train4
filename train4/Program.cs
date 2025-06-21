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

            // ������ �������� ����
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // ������ ������ �� HttpContext
            builder.Services.AddHttpContextAccessor();

            // ��������� ������
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            builder.Services.AddScoped<IStationService, StationService>();
            builder.Services.AddScoped<ITrainService, TrainService>();
            builder.Services.AddScoped<IScheduleService, ScheduleService>();
            builder.Services.AddScoped<ITicketService, TicketService>();
            builder.Services.AddScoped<IAccountingService, AccountingService>();
            builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
            builder.Services.AddScoped<IScheduleRouteService, ScheduleRouteService>();

            // �������� ���������� connection string ��� RailwayDbContext
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
            app.UseSession(); // �� ���� �� UseRouting � UseAuthorization
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
//            // ������������ ����
//            builder.Services.AddDistributedMemoryCache();
//            builder.Services.AddSession(options =>
//            {
//                options.IdleTimeout = TimeSpan.FromMinutes(30);
//                options.Cookie.HttpOnly = true;
//                options.Cookie.IsEssential = true;
//            });
//            // ��������� ��� ������� �� HttpContext � �������
//            builder.Services.AddHttpContextAccessor();

//            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
//            builder.Services.AddScoped<IStationService, StationService>();
//            builder.Services.AddScoped<ITrainService, TrainService>();
//            builder.Services.AddScoped<IScheduleService, ScheduleService>();
//            builder.Services.AddScoped<ITicketService, TicketService>();
//            builder.Services.AddScoped<IAccountingService, AccountingService>();
//            builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();


//            //// ��������� ������ ������ � ����� �����
//            //builder.Services.AddScoped<IDatabaseService, PostgreSqlDatabaseService>();
//            //// ��������� ������ ������ � ��������
//            //builder.Services.AddScoped<ITicketService, PostgreSqlTicketService>();
//            // ������ DbContext � ���������� ����������� ���������� (��� ������ ����� ����)
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
//            // �������: UseSession �� ���� ���� UseRouting, ��� ����� UseEndpoints
//            app.UseSession();
//            app.UseAuthorization();
//            app.MapControllerRoute(
//                name: "default",
//                pattern: "{controller=Home}/{action=Index}/{id?}");
//            app.Run();
//        }
//    }
//}