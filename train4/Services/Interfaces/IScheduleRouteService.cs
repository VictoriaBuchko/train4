using train2.Models;

namespace train4.Services.Interfaces
{
    public interface IScheduleRouteService
    {
        // Получение всех расписаний
        Task<List<Dictionary<string, object>>> GetAllSchedulesAsync();

        // Создание нового расписания
        Task<(bool Success, string ErrorMessage)> CreateScheduleAsync(int trainId, DateTime date, TimeSpan departureTime, string weekdays);

        // Получение расписания по ID
        Task<Dictionary<string, object>?> GetScheduleByIdAsync(int scheduleId);

        // Проверка возможности редактирования расписания
        Task<(bool CanEdit, string Reason)> CanEditScheduleAsync(int scheduleId);

        // Обновление расписания
        Task<(bool Success, string ErrorMessage, string WarningMessage)> UpdateScheduleAsync(int scheduleId, int trainId, DateTime date, TimeSpan departureTime, string weekdays);

        // Заморозка/активация расписания
        Task<(bool Success, string Message, bool NewStatus)> ToggleScheduleStatusAsync(int scheduleId);

        // Детальная информация о расписании
        Task<Dictionary<string, object>?> GetScheduleDetailsAsync(int scheduleId);

        // Получение активных поездов для выбора
        Task<List<Dictionary<string, object>>> GetActiveTrainsAsync();

        // Проверка существования маршрута для поезда
        Task<(bool HasRoute, string TrainNumber, List<Dictionary<string, object>> Route)> GetTrainRouteAsync(int trainId);

        // Получение расписаний с фильтрацией
        Task<List<Dictionary<string, object>>> GetSchedulesWithFiltersAsync(int? trainId = null, DateTime? fromDate = null, DateTime? toDate = null, bool? isActive = null);

        //// Основные методы для управления расписанием

        ///// <summary>
        ///// Получение всех расписаний с информацией о поездах и маршрутах
        ///// </summary>
        //Task<List<Dictionary<string, object>>> GetAllSchedulesAsync();

        ///// <summary>
        ///// Создание нового расписания с маршрутом для поезда
        ///// </summary>
        ///// <param name="trainId">ID поезда</param>
        ///// <param name="date">Дата начала действия расписания</param>
        ///// <param name="weekdays">Дни недели в формате "Пн,Ср,Пт"</param>
        ///// <param name="departureTime">Время отправления</param>
        ///// <param name="stationIds">Список ID станций маршрута</param>
        ///// <param name="travelDurations">Время в пути между станциями</param>
        ///// <param name="distances">Расстояния между станциями в км</param>
        //Task<(bool Success, string ErrorMessage)> CreateScheduleWithRouteAsync(
        //    int trainId,
        //    DateTime date,
        //    string weekdays,
        //    TimeSpan departureTime,
        //    List<int> stationIds,
        //    List<TimeSpan> travelDurations,
        //    List<int> distances);

        ///// <summary>
        ///// Получение расписания для редактирования
        ///// </summary>
        //Task<Dictionary<string, object>> GetScheduleForEditAsync(int scheduleId);

        ///// <summary>
        ///// Обновление расписания с маршрутом
        ///// Если есть билеты - маршрут не изменяется, только время и дни
        ///// </summary>
        //Task<(bool Success, string ErrorMessage, string Message)> UpdateScheduleWithRouteAsync(
        //    int scheduleId,
        //    DateTime date,
        //    string weekdays,
        //    TimeSpan departureTime,
        //    List<int> stationIds,
        //    List<TimeSpan> travelDurations,
        //    List<int> distances);

        ///// <summary>
        ///// Получение детальной информации о расписании
        ///// </summary>
        //Task<Dictionary<string, object>> GetScheduleDetailsAsync(int scheduleId);

        ///// <summary>
        ///// Проверка наличия купленных билетов на расписание
        ///// </summary>
        //Task<bool> CheckScheduleHasBookingsAsync(int scheduleId);

        ///// <summary>
        ///// Изменение статуса расписания (активное/неактивное)
        ///// Нельзя деактивировать если есть билеты
        ///// </summary>
        //Task<(bool Success, string Message, bool NewStatus)> ToggleScheduleStatusAsync(int scheduleId);

        //// Дополнительные методы для работы с расписанием

        ///// <summary>
        ///// Получение станций конкретного расписания
        ///// </summary>
        //Task<List<Dictionary<string, object>>> GetScheduleStationsAsync(int scheduleId);

        ///// <summary>
        ///// Проверка возможности редактирования расписания
        ///// </summary>
        //Task<bool> CanEditScheduleAsync(int scheduleId);

        ///// <summary>
        ///// Получение количества активных бронирований для расписания
        ///// </summary>
        //Task<int> GetActiveBookingsCountForScheduleAsync(int scheduleId);

        ///// <summary>
        ///// Поиск расписаний между станциями на дату
        ///// </summary>
        //Task<List<Dictionary<string, object>>> SearchSchedulesAsync(string fromStation, string toStation, DateTime date);

        ///// <summary>
        ///// Получение ID расписания по ID поезда и дате
        ///// </summary>
        //Task<int> GetScheduleIdAsync(int trainId, DateTime travelDate);
    }
}
