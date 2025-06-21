using train2.Models;

namespace train2.Services
{
    public interface IDatabaseService
    {
        // Підключення до бази даних під певним користувачем ---
        Task<(bool Success, string ErrorMessage)> ConnectAsync(string login, string password, string email = null);

        // Отримання інформації про поточного користувача бази даних---
        Task<(string Username, string Role)> GetCurrentUserInfoAsync();

        // Отримання списку всіх станцій
        Task<List<Stations>> GetAllStationsAsync();

        // Пошук розкладів за параметрами
        Task<List<Schedule>> SearchSchedulesAsync(string fromStation, string toStation, DateTime date);

        // Отримання інформації про клієнта за його логіном---
        Task<Client> GetClientByLoginAsync(string login);
        // Оновлення інформації про клієнта
        Task<(bool Success, string ErrorMessage)> UpdateClientAsync(Client client);


        Task<(bool Success, string ErrorMessage)> CreateClientAsync(Client client, string password);

        // Вихід із системи (повернення до гостьового доступу)---
        Task LogoutAsync();

        // Отримання списку всіх потягів
        Task<List<Train>> GetAllTrainsAsync();

        // Отримання списку всіх типів вагонів
        Task<List<CarriageTypes>> GetAllCarriageTypesAsync();

        // Створення потяга з вагонами
        Task<(bool Success, string ErrorMessage)> CreateTrainWithCarriagesAsync(Train train, int[] carriageTypeIds, int[] carriageNumbers);

        // Отримання деталей потяга
        Task<(Train Train, Dictionary<int, int> SeatCounts)> GetTrainDetailsAsync(int trainId);

        // Створення маршруту
        Task<(bool Success, string ErrorMessage)> CreateRouteAsync(int trainId, List<int> stationIds);

        // Отримання маршруту за ID потяга
        Task<(string TrainNumber, List<StationSequence> Route)> GetRouteByTrainIdAsync(int trainId);

        // Метод для отримання потяга для редагування
        Task<(Train train, List<Dictionary<string, object>> carriages)> GetTrainForEditAsync(int trainId);

        // Метод для оновлення потяга
        Task<(bool Success, string ErrorMessage)> UpdateTrainAsync(int trainId, int trainNumber, List<Dictionary<string, object>> carriageData);

        // Метод для перевірки вагонів з бронюваннями
        Task<List<Dictionary<string, object>>> GetCarriagesWithBookingsAsync(int trainId);

        // Методи для управління статусом потяга
        Task<(bool CanChange, string Reason, int ActiveTickets)> CanChangeTrainStatusAsync(int trainId);
        Task<(bool Success, string Message, bool NewStatus)> ToggleTrainStatusAsync(int trainId);
        Task<Train?> GetTrainByIdAsync(int trainId);




        Task<string> AddStationAsync(string name);
        Task<string> DeleteStationAsync(int id);
        Task<string> UpdateStationNameAsync(int id, string newName);



        Task<List<Dictionary<string, object>>> GetTopFilledRoutesAsync(DateTime start, DateTime end);
        Task<List<Dictionary<string, object>>> GetTopIncomeRoutesAsync(DateTime start, DateTime end);
        Task<List<Dictionary<string, object>>> GetMostPopularRoutesAsync(DateTime start, DateTime end);



       // Task<bool> CreateTicketAsync(int seatId, int clientId, int trainId, DateTime travelDate);
      //  Task<TicketPreviewDto> GetTicketPreviewAsync(int seatId, int trainId, DateTime travelDate);


        //// Методи для роботи з місцями
        //Task<List<Dictionary<string, object>>> GetSeatStatusAsync(int trainId, DateTime travelDate);
        Task<bool> BookSeatAsync(int seatId, int clientId, DateTime travelDate);
        Task<int> GetScheduleIdAsync(int trainId, DateTime travelDate);

        // Нові методи для отримання місць
        Task<List<Seat>> GetAvailableSeatsAsync(int trainId, DateTime travelDate, int fromStationId, int toStationId);
        Task<List<Dictionary<string, object>>> GetSeatsWithDetailsAsync(int trainId, DateTime travelDate, int fromStationId, int toStationId);
        Task<int?> GetStationIdByNameAsync(string stationName);
        Task<Dictionary<string, object>> GetTicketInfoAsync(int trainId, DateTime date, int seatId, int fromStationId, int toStationId);

        // Также добавить этот метод в интерфейс IDatabaseService.cs
        Task<Dictionary<string, object>> CreateTicketBookingAsync(int clientId, int seatId, int trainId, DateTime date, int fromStationId, int toStationId);




        // Поиск клиентов для менеджера
        Task<List<Client>> SearchClientsAsync(string query);

        // Поиск билетов по клиенту
        Task<List<Dictionary<string, object>>> SearchTicketsByClientAsync(string clientQuery);

        // Отмена билета
        Task<Dictionary<string, object>> CancelTicketAsync(int ticketId);


        //Task<bool> ReserveTicketAsync(int clientId, int seatId, int scheduleId, DateTime bookingDate, int fromStationId, int toStationId);

        //Task<List<Train>> GetAllTrainsAsync();
        //Task<List<CarriageTypes>> GetAllCarriageTypesAsync();
        //Task<(bool Success, string ErrorMessage)> CreateTrainWithCarriagesAsync(Train train, int[] carriageTypeIds, int[] carriageNumbers);
        //Task<(Train Train, Dictionary<int, int> SeatCounts)> GetTrainDetailsAsync(int trainId);

        //// Метод для отримання потяга для редагування
        //Task<(Train train, List<Dictionary<string, object>> carriages)> GetTrainForEditAsync(int trainId);

        //// Метод для оновлення потяга
        //Task<(bool Success, string ErrorMessage)> UpdateTrainAsync(int trainId, int trainNumber, List<Dictionary<string, object>> carriageData);

        //// Метод для перевірки вагонів з бронюваннями
        //Task<List<Dictionary<string, object>>> GetCarriagesWithBookingsAsync(int trainId);



        //Task<(bool Success, string ErrorMessage)> CreateRouteAsync(int trainId, List<int> stationIds);
        //Task<(string TrainNumber, List<StationSequence> Route)> GetRouteByTrainIdAsync(int trainId);


        //// Методи для управління статусом потяга
        //Task<(bool CanChange, string Reason, int ActiveTickets)> CanChangeTrainStatusAsync(int trainId);
        //Task<(bool Success, string Message, bool NewStatus)> ToggleTrainStatusAsync(int trainId);

        Task<List<Dictionary<string, object>>> GetUnpaidTicketsAsync(int page, int pageSize, string searchQuery = "");
        Task<int> GetUnpaidTicketsCountAsync(string searchQuery = "");
        Task<Dictionary<string, object>> ConfirmTicketPaymentAsync(int ticketId, decimal paidAmount, string paymentMethod);
        Task<Dictionary<string, object>> GetFinancialReportAsync(DateTime startDate, DateTime endDate, string reportType);
        Task<List<Dictionary<string, object>>> GetDailyStatisticsAsync(DateTime startDate, DateTime endDate);
        Task<List<Dictionary<string, object>>> GetDetailedFinancialReportAsync(DateTime startDate, DateTime endDate);
    }
}