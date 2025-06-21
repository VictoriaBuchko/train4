using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using train2.Models;
using train4.Services.Interfaces;

namespace train2.Controllers
{
    public class HomeController : Controller
    {
        private readonly IStationService _stationService;
        private readonly IScheduleService _scheduleService;
        private readonly ITicketService _ticketService;
        private readonly ITrainService _trainService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IStationService stationService,
            IScheduleService scheduleService,
            ITicketService ticketService,
            ITrainService trainService,
            ILogger<HomeController> logger)
        {
            _stationService = stationService;
            _scheduleService = scheduleService;
            _ticketService = ticketService;
            _trainService = trainService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Получаем станции через StationService
            var stations = await _stationService.GetAllStationsAsync();
            ViewBag.Stations = stations;

            var isLoggedIn = !string.IsNullOrEmpty(HttpContext.Session.GetString("db_user"));
            ViewBag.IsLoggedIn = isLoggedIn;

            var role = HttpContext.Session.GetString("user_role");
            ViewBag.UserRole = role;

            return View();
        }

        [HttpPost]
        public IActionResult Index(string fromStation, string toStation, DateTime date)
        {
            if (fromStation == toStation)
            {
                ModelState.AddModelError("", "Станції не можуть бути однаковими.");
            }

            if (date < DateTime.Today)
            {
                ModelState.AddModelError("", "Дата поїздки не може бути раніше сьогодні.");
            }

            if (!ModelState.IsValid)
            {
                return RedirectToAction("Index");
            }

            return RedirectToAction("Schedule", new { fromStation, toStation, date = date.ToString("yyyy-MM-dd") });
        }

        //public async Task<IActionResult> Schedule(string fromStation, string toStation, DateTime date)
        //{
        //    var results = await _scheduleService.SearchSchedulesAsync(fromStation, toStation, date);
        //    ViewBag.From = fromStation;
        //    ViewBag.To = toStation;
        //    ViewBag.Date = date.ToString("yyyy-MM-dd");

        //    var fromStationId = await _stationService.GetStationIdByNameAsync(fromStation);
        //    var toStationId = await _stationService.GetStationIdByNameAsync(toStation);
        //    ViewBag.FromStationId = fromStationId;
        //    ViewBag.ToStationId = toStationId;


        //    // Перевіряємо, чи є дані сесії про користувача
        //    var isLoggedIn = !string.IsNullOrEmpty(HttpContext.Session.GetString("db_user"));
        //    ViewBag.IsLoggedIn = isLoggedIn;

        //    return View(results);
        //}
        public async Task<IActionResult> Schedule(string fromStation, string toStation, DateTime date)
        {
            // ИСПРАВЛЕНИЕ: Проверяем, что дата корректно парсится
            if (date == DateTime.MinValue || date == default(DateTime))
            {
                _logger.LogWarning("Получена некорректная дата: {Date}", date);
                return RedirectToAction("Index");
            }

            var results = await _scheduleService.SearchSchedulesAsync(fromStation, toStation, date);
            ViewBag.From = fromStation;
            ViewBag.To = toStation;
            ViewBag.Date = date.ToString("yyyy-MM-dd"); // ИСПРАВЛЕНИЕ: Стандартный формат

            var fromStationId = await _stationService.GetStationIdByNameAsync(fromStation);
            var toStationId = await _stationService.GetStationIdByNameAsync(toStation);
            ViewBag.FromStationId = fromStationId;
            ViewBag.ToStationId = toStationId;

            var isLoggedIn = !string.IsNullOrEmpty(HttpContext.Session.GetString("db_user"));
            ViewBag.IsLoggedIn = isLoggedIn;

            return View(results);
        }

        public async Task<ActionResult> SelectSeats(int trainId, DateTime travelDate, int fromStationId, int toStationId)
        {
            // Используем TicketService для получения мест
            var seatsWithDetails = await _ticketService.GetSeatsWithDetailsAsync(trainId, travelDate, fromStationId, toStationId);

            ViewBag.SeatsData = seatsWithDetails;
            ViewBag.TrainId = trainId;
            ViewBag.TravelDate = travelDate.ToString("yyyy-MM-dd");
            ViewBag.FromStationId = fromStationId;
            ViewBag.ToStationId = toStationId;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> CheckSeatAvailability(int seatId, int trainId, DateTime travelDate, int fromStationId, int toStationId)
        {
            try
            {
                var seatsData = await _ticketService.GetSeatsWithDetailsAsync(trainId, travelDate, fromStationId, toStationId);
                var seat = seatsData?.FirstOrDefault(s => Convert.ToInt32(s["seat_id"]) == seatId);

                var isAvailable = seat != null && seat["status"].ToString() == "available";
                return Json(new { available = isAvailable });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при перевірці доступності місця {SeatId}", seatId);
                return Json(new { available = false });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTicketDetails(int trainId, DateTime travelDate, int seatId, int fromStationId, int toStationId)
        {
            try
            {
                // ИСПРАВЛЕНИЕ: Добавляем детальную отладку
                _logger.LogInformation("GetTicketDetails called with: trainId={TrainId}, travelDate={TravelDate}, seatId={SeatId}, fromStationId={FromStationId}, toStationId={ToStationId}",
                    trainId, travelDate, seatId, fromStationId, toStationId);

                // ИСПРАВЛЕНИЕ: Проверяем корректность даты
                if (travelDate == DateTime.MinValue || travelDate == default(DateTime))
                {
                    _logger.LogError("GetTicketDetails: Получена некорректная дата: {TravelDate}", travelDate);
                    return Json(new { error = "Некорректная дата поездки" });
                }

                var info = await _ticketService.GetTicketInfoAsync(trainId, travelDate, seatId, fromStationId, toStationId);
                return Json(info);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка в GetTicketDetails для поезда {TrainId} на дату {TravelDate}", trainId, travelDate);
                return Json(new { error = "Ошибка получения информации о билете" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTrainNumber(int trainId)
        {
            try
            {
                // Используем TrainService для получения информации о поезде
                var train = await _trainService.GetTrainByIdAsync(trainId);
                var trainNumber = train?.train_number ?? trainId;
                return Json(new { trainNumber = trainNumber });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при отриманні номера поїзда {TrainId}", trainId);
                return Json(new { trainNumber = trainId });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        [HttpGet]
        public async Task<IActionResult> Seats(int trainId, DateTime travelDate, int fromStationId, int toStationId)
        {
            try
            {
                // ИСПРАВЛЕНИЕ: Добавляем детальную отладку
                _logger.LogInformation("Seats method called with: trainId={TrainId}, travelDate={TravelDate}, fromStationId={FromStationId}, toStationId={ToStationId}",
                    trainId, travelDate, fromStationId, toStationId);

                // ИСПРАВЛЕНИЕ: Проверяем корректность даты
                if (travelDate == DateTime.MinValue || travelDate == default(DateTime))
                {
                    _logger.LogError("Получена некорректная дата поездки: {TravelDate}", travelDate);
                    return RedirectToAction("Schedule", new { fromStation = "Error", toStation = "Error", date = DateTime.Today.ToString("yyyy-MM-dd") });
                }

                // Получаем информацию о поезде для отображения реального номера
                var trainInfo = await _trainService.GetTrainByIdAsync(trainId);

                // Используем метод с Dictionary для получения полной информации
                var seatsData = await _ticketService.GetSeatsWithDetailsAsync(trainId, travelDate, fromStationId, toStationId);

                if (seatsData == null || !seatsData.Any())
                {
                    ViewBag.NoSeatsMessage = "Немає інформації про місця для вказаного поїзда або дати.";
                    return View(new List<Seat>());
                }

                // Конвертируем Dictionary в список Seat для View
                var seats = seatsData.Select(data => new Seat
                {
                    seat_id = Convert.ToInt32(data["seat_id"]),
                    seat_number = Convert.ToInt32(data["seat_number"]),
                    train_carriage_types_id = Convert.ToInt32(data["train_carriage_types_id"])
                }).ToList();

                // Передаем дополнительную информацию через ViewBag
                ViewBag.SeatsData = seatsData;
                ViewBag.TrainId = trainId;
                ViewBag.TrainNumber = trainInfo?.train_number ?? trainId;
                ViewBag.TravelDate = travelDate.ToString("yyyy-MM-dd"); // ИСПРАВЛЕНИЕ: Стандартный формат
                ViewBag.FromStationId = fromStationId;
                ViewBag.ToStationId = toStationId;

                // ИСПРАВЛЕНИЕ: Добавляем отладочную информацию в ViewBag
                ViewBag.TravelDateFormatted = travelDate.ToString("dd.MM.yyyy");
                ViewBag.TravelDateISO = travelDate.ToString("yyyy-MM-dd");

                return View(seats);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при отриманні інформації про місця для поїзда {TrainId} на дату {TravelDate}", trainId, travelDate);
                ViewBag.NoSeatsMessage = "Виникла помилка при завантаженні інформації про місця.";
                return View(new List<Seat>());
            }
        }
        //[HttpGet]
        //public async Task<IActionResult> Seats(int trainId, DateTime travelDate, int fromStationId, int toStationId)
        //{
        //    try
        //    {
        //        // Получаем информацию о поезде для отображения реального номера
        //        var trainInfo = await _trainService.GetTrainByIdAsync(trainId);

        //        // Используем метод с Dictionary для получения полной информации
        //        var seatsData = await _ticketService.GetSeatsWithDetailsAsync(trainId, travelDate, fromStationId, toStationId);


        //        if (seatsData == null || !seatsData.Any())
        //        {
        //            ViewBag.NoSeatsMessage = "Немає інформації про місця для вказаного поїзда або дати.";
        //            return View(new List<Seat>());
        //        }

        //        // Конвертируем Dictionary в список Seat для View
        //        var seats = seatsData.Select(data => new Seat
        //        {
        //            seat_id = Convert.ToInt32(data["seat_id"]),
        //            seat_number = Convert.ToInt32(data["seat_number"]),
        //            train_carriage_types_id = Convert.ToInt32(data["train_carriage_types_id"])
        //        }).ToList();

        //        // Передаем дополнительную информацию через ViewBag
        //        ViewBag.SeatsData = seatsData; // Полная информация для JavaScript
        //        ViewBag.TrainId = trainId;
        //        ViewBag.TrainNumber = trainInfo?.train_number ?? trainId; // РЕАЛЬНЫЙ номер поезда
        //        ViewBag.TravelDate = travelDate.ToString("dd.MM.yyyy");
        //        ViewBag.FromStationId = fromStationId;
        //        ViewBag.ToStationId = toStationId;

        //        return View(seats);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger?.LogError(ex, "Помилка при отриманні інформації про місця для поїзда {TrainId} на дату {TravelDate}", trainId, travelDate);
        //        ViewBag.NoSeatsMessage = "Виникла помилка при завантаженні інформації про місця.";
        //        return View(new List<Seat>());
        //    }

        //}


        [HttpPost]
        public async Task<IActionResult> BookSeatConfirmed(int seatId, int trainId, DateTime travelDate, int fromStationId, int toStationId)
        {
            try
            {
                int clientId = GetCurrentClientId();

                // Используем TicketService для создания бронирования
                var result = await _ticketService.CreateTicketBookingAsync(clientId, seatId, trainId, travelDate, fromStationId, toStationId);

                if ((bool)result["success"])
                {
                    return Json(new
                    {
                        success = true,
                        message = result["message"].ToString(),
                        ticket_id = result["ticket_id"],
                        total_price = result["total_price"]
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = result["message"].ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при бронировании билета");
                return Json(new
                {
                    success = false,
                    message = "Ошибка сервера при бронировании билета"
                });
            }
        }

        private int GetCurrentClientId()
        {
            var clientId = HttpContext.Session.GetInt32("client_id");

            if (clientId == null)
            {
                throw new UnauthorizedAccessException("Пользователь не авторизован. Необходимо войти в систему.");
            }

            return clientId.Value;
        }
    }
}

