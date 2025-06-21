
using Microsoft.AspNetCore.Mvc;
using train2.Models;
using train4.Services.Interfaces;

namespace train3.Controllers
{
    public class ManagerController : Controller
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly IStationService _stationService;
        private readonly IScheduleService _scheduleService;
        private readonly ITicketService _ticketService;
        private readonly ITrainService _trainService;
        private readonly ILogger<ManagerController> _logger;

        public ManagerController(
            IAuthenticationService authenticationService,
            IStationService stationService,
            IScheduleService scheduleService,
            ITicketService ticketService,
            ITrainService trainService,
            ILogger<ManagerController> logger)
        {
            _authenticationService = authenticationService;
            _stationService = stationService;
            _scheduleService = scheduleService;
            _ticketService = ticketService;
            _trainService = trainService;
            _logger = logger;
        }

        // Проверка авторизации менеджера
        private bool IsManagerAuthorized()
        {
            var userRole = HttpContext.Session.GetString("user_role");
            return userRole == "group_managers" || userRole == "group_admins";
        }

        // Главная панель менеджера
        public async Task<IActionResult> Index()
        {
            if (!IsManagerAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу до панелі менеджера.";
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // Создание нового клиента - GET
        [HttpGet]
        public async Task<IActionResult> CreateClient()
        {
            if (!IsManagerAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу до панелі менеджера.";
                return RedirectToAction("Index", "Home");
            }

            return View(new Client());
        }

        // Создание нового клиента - POST
        [HttpPost]
        public async Task<IActionResult> CreateClient(Client client, string password, string confirmPassword)
        {
            if (!IsManagerAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу до панелі менеджера.";
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrEmpty(password) || password != confirmPassword)
            {
                ModelState.AddModelError("", "Паролі не співпадають або порожні.");
                return View(client);
            }

            if (!ModelState.IsValid)
            {
                return View(client);
            }

            // Используем AuthenticationService
            var result = await _authenticationService.CreateClientAsync(client, password);

            if (result.Success)
            {
                TempData["SuccessMessage"] = $"Клієнт {client.client_name} {client.client_surname} успішно створений.";
                return RedirectToAction("Index");
            }
            else
            {
                ModelState.AddModelError("", result.ErrorMessage);
                return View(client);
            }
        }

        // Поиск билетов для бронирования - GET
        [HttpGet]
        public async Task<IActionResult> BookTicket()
        {
            if (!IsManagerAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу до панелі менеджера.";
                return RedirectToAction("Index", "Home");
            }

            // Используем StationService
            var stations = await _stationService.GetAllStationsAsync();
            ViewBag.Stations = stations;

            return View();
        }

        // Поиск билетов для бронирования - POST
        [HttpPost]
        public async Task<IActionResult> BookTicket(string fromStation, string toStation, DateTime date)
        {
            if (!IsManagerAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу до панелі менеджера.";
                return RedirectToAction("Index", "Home");
            }

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
                var stations = await _stationService.GetAllStationsAsync();
                ViewBag.Stations = stations;
                return View();
            }

            return RedirectToAction("SelectSeatsForClient", new { fromStation, toStation, date = date.ToString("yyyy-MM-dd") });
        }

        // ИСПРАВЛЕННЫЙ метод: Выбор мест для клиента
        public async Task<IActionResult> SelectSeatsForClient(string fromStation, string toStation, DateTime date)
        {
            if (!IsManagerAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу до панелі менеджера.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                // ИСПРАВЛЕНИЕ: Используем метод SearchSchedulesAsync, который возвращает List<Schedule>
                var results = await _scheduleService.SearchSchedulesAsync(fromStation, toStation, date);

                ViewBag.From = fromStation;
                ViewBag.To = toStation;
                ViewBag.Date = date.ToString("yyyy-MM-dd");

                // Используем StationService для получения ID станций
                var fromStationId = await _stationService.GetStationIdByNameAsync(fromStation);
                var toStationId = await _stationService.GetStationIdByNameAsync(toStation);
                ViewBag.FromStationId = fromStationId;
                ViewBag.ToStationId = toStationId;

                // Передаем List<Schedule> в View
                return View(results);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при пошуку розкладу для менеджера");
                TempData["ErrorMessage"] = "Виникла помилка при пошуку розкладу. Спробуйте ще раз.";
                return RedirectToAction("BookTicket");
            }
        }

        // Выбор мест в поезде для клиента
        public async Task<IActionResult> SeatsForClient(int trainId, DateTime travelDate, int fromStationId, int toStationId)
        {
            if (!IsManagerAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу до панелі менеджера.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                // Используем TrainService для получения информации о поезде
                var trainInfo = await _trainService.GetTrainByIdAsync(trainId);

                // Используем TicketService для получения мест
                var seatsData = await _ticketService.GetSeatsWithDetailsAsync(trainId, travelDate, fromStationId, toStationId);

                if (seatsData == null || !seatsData.Any())
                {
                    ViewBag.NoSeatsMessage = "Немає інформації про місця для вказаного поїзда або дати.";
                    return View(new List<Seat>());
                }

                var seats = seatsData.Select(data => new Seat
                {
                    seat_id = Convert.ToInt32(data["seat_id"]),
                    seat_number = Convert.ToInt32(data["seat_number"]),
                    train_carriage_types_id = Convert.ToInt32(data["train_carriage_types_id"])
                }).ToList();

                ViewBag.SeatsData = seatsData;
                ViewBag.TrainId = trainId;
                ViewBag.TrainNumber = trainInfo?.train_number ?? trainId;
                ViewBag.TravelDate = travelDate.ToString("dd.MM.yyyy");
                ViewBag.FromStationId = fromStationId;
                ViewBag.ToStationId = toStationId;

                return View(seats);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при отриманні інформації про місця для поїзда {TrainId} на дату {TravelDate}", trainId, travelDate);
                ViewBag.NoSeatsMessage = "Виникла помилка при завантаженні інформації про місця.";
                return View(new List<Seat>());
            }
        }

        // API для поиска клиентов
        [HttpGet]
        public async Task<IActionResult> SearchClients(string query)
        {
            if (!IsManagerAuthorized())
            {
                return Json(new { error = "Unauthorized" });
            }

            try
            {
                // Используем AuthenticationService для поиска клиентов
                var clients = await _authenticationService.SearchClientsAsync(query);
                var result = clients.Select(c => new
                {
                    id = c.client_id,
                    name = $"{c.client_name} {c.client_surname} {c.client_patronymic}",
                    login = c.login,
                    email = c.email,
                    phone = c.phone_number
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при пошуку клієнтів");
                return Json(new { error = "Помилка пошуку" });
            }
        }

        // Бронирование билета для клиента
        [HttpPost]
        public async Task<IActionResult> BookSeatForClient(int clientId, int seatId, int trainId, DateTime travelDate, int fromStationId, int toStationId)
        {
            if (!IsManagerAuthorized())
            {
                return Json(new { success = false, message = "У вас немає прав доступу." });
            }

            try
            {
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
                _logger?.LogError(ex, "Ошибка при бронировании билета для клиента");
                return Json(new
                {
                    success = false,
                    message = "Ошибка сервера при бронировании билета"
                });
            }
        }

        // Управление билетами (отмена)
        [HttpGet]
        public async Task<IActionResult> ManageTickets()
        {
            if (!IsManagerAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу до панелі менеджера.";
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // Поиск билетов по клиенту
        [HttpGet]
        public async Task<IActionResult> SearchTicketsByClient(string clientQuery)
        {
            if (!IsManagerAuthorized())
            {
                return Json(new { error = "Unauthorized" });
            }

            try
            {
                // Используем TicketService для поиска билетов
                var tickets = await _ticketService.SearchTicketsByClientAsync(clientQuery);
                return Json(tickets);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при пошуку квитків");
                return Json(new { error = "Помилка пошуку квитків" });
            }
        }

        // Отмена билета
        [HttpPost]
        public async Task<IActionResult> CancelTicket(int ticketId)
        {
            if (!IsManagerAuthorized())
            {
                return Json(new { success = false, message = "У вас немає прав доступу." });
            }

            try
            {
                // Используем TicketService для отмены билета
                var result = await _ticketService.CancelTicketAsync(ticketId);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при скасуванні квитка {TicketId}", ticketId);
                return Json(new { success = false, message = "Помилка при скасуванні квитка" });
            }
        }
    }
}