using Microsoft.AspNetCore.Mvc;
using train2.Models;
using train4.Services.Implementation;
using train4.Services.Interfaces;

namespace train2.Controllers
{
    public class ScheduleController : Controller
    {
        private readonly ILogger<ScheduleController> _logger;
        private readonly IScheduleRouteService _scheduleRouteService;
        private readonly ITrainService _trainService;
        private readonly IStationService _stationService;

        public ScheduleController(
            ILogger<ScheduleController> logger,
            IScheduleRouteService scheduleRouteService,
            ITrainService trainService,
            IStationService stationService)
        {
            _logger = logger;
            _scheduleRouteService = scheduleRouteService;
            _trainService = trainService;
            _stationService = stationService;
        }

        // Головна сторінка розкладів з фільтрами
        [HttpGet]
        public async Task<IActionResult> Index(int? trainId = null, DateTime? fromDate = null, DateTime? toDate = null, bool? isActive = null)
        {
            var userRole = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            try
            {
                List<Dictionary<string, object>> schedules;

                // Если есть фильтры, используем их
                if (trainId.HasValue || fromDate.HasValue || toDate.HasValue || isActive.HasValue)
                {
                    schedules = await _scheduleRouteService.GetSchedulesWithFiltersAsync(trainId, fromDate, toDate, isActive);
                }
                else
                {
                    schedules = await _scheduleRouteService.GetAllSchedulesAsync();
                }

                // Получаем список активных поездов для фильтра
                var trains = await _scheduleRouteService.GetActiveTrainsAsync();
                ViewBag.Trains = trains;

                // Передаем текущие значения фильтров для сохранения состояния
                ViewBag.SelectedTrainId = trainId;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.IsActive = isActive;

                return View(schedules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні списку розкладів");
                TempData["ErrorMessage"] = "Помилка при завантаженні розкладів";

                // Возвращаем пустой список в случае ошибки
                ViewBag.Trains = new List<Dictionary<string, object>>();
                return View(new List<Dictionary<string, object>>());
            }
        }

        // API метод для получения списка активных поездов (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetActiveTrains()
        {
            try
            {
                var trains = await _scheduleRouteService.GetActiveTrainsAsync();
                return Json(trains);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні списку активних потягів");
                return Json(new List<object>());
            }
        }

        // Сторінка створення нового розкладу
        [HttpGet]
        public async Task<IActionResult> CreateSchedule()
        {
            var userRole = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            try
            {
                var trains = await _scheduleRouteService.GetActiveTrainsAsync();
                ViewBag.Trains = trains;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при завантаженні списку потягів для створення розкладу");
                TempData["ErrorMessage"] = "Помилка при завантаженні даних";
                return RedirectToAction("Index");
            }
        }

        // Обробка POST-запиту на створення розкладу
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSchedule(int trainId, DateTime scheduleDate, string departureTime, string[] selectedDays)
        {
            var userRole = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            try
            {
                // Валідація введених даних
                if (trainId <= 0)
                {
                    ModelState.AddModelError("", "Необхідно вибрати потяг");
                }

                if (scheduleDate.Date < DateTime.Today)
                {
                    ModelState.AddModelError("", "Дата не може бути в минулому");
                }

                if (!TimeSpan.TryParse(departureTime, out var parsedTime))
                {
                    ModelState.AddModelError("", "Невірний формат часу відправлення");
                }

                if (selectedDays == null || selectedDays.Length == 0)
                {
                    ModelState.AddModelError("", "Необхідно вибрати хоча б один день тижня");
                }

                if (!ModelState.IsValid)
                {
                    return await ReloadCreateScheduleView();
                }

                // Перевіряємо чи існує маршрут для цього потяга
                var routeInfo = await _scheduleRouteService.GetTrainRouteAsync(trainId);
                if (!routeInfo.HasRoute)
                {
                    ModelState.AddModelError("", $"Для потяга №{routeInfo.TrainNumber} не налаштовано маршрут. Спочатку створіть маршрут у розділі 'Маршрути'.");
                    return await ReloadCreateScheduleView();
                }

                // Формуємо рядок днів тижня
                string weekdaysString = string.Join(",", selectedDays);

                // Створюємо розклад
                var result = await _scheduleRouteService.CreateScheduleAsync(trainId, scheduleDate, parsedTime, weekdaysString);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = $"Розклад для потяга №{routeInfo.TrainNumber} успішно створено!";
                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", result.ErrorMessage);
                    return await ReloadCreateScheduleView();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при створенні розкладу");
                ModelState.AddModelError("", "Сталася помилка при створенні розкладу: " + ex.Message);
                return await ReloadCreateScheduleView();
            }
        }

        // Сторінка редагування розкладу
        [HttpGet]
        public async Task<IActionResult> EditSchedule(int id)
        {
            var userRole = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            try
            {
                var schedule = await _scheduleRouteService.GetScheduleByIdAsync(id);
                if (schedule == null)
                {
                    TempData["ErrorMessage"] = "Розклад не знайдено";
                    return RedirectToAction("Index");
                }

                // Перевіряємо чи можна редагувати цей розклад
                var canEdit = await _scheduleRouteService.CanEditScheduleAsync(id);
                if (!canEdit.CanEdit)
                {
                    TempData["ErrorMessage"] = canEdit.Reason;
                    return RedirectToAction("Index");
                }

                var trains = await _scheduleRouteService.GetActiveTrainsAsync();
                ViewBag.Trains = trains;
                ViewBag.CanEdit = canEdit;

                return View(schedule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при завантаженні розкладу для редагування {ScheduleId}", id);
                TempData["ErrorMessage"] = "Помилка при завантаженні розкладу";
                return RedirectToAction("Index");
            }
        }

        // Обробка POST-запиту на редагування розкладу
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSchedule(int id, int trainId, DateTime scheduleDate, string departureTime, string[] selectedDays)
        {
            var userRole = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            try
            {
                // Перевіряємо чи можна редагувати цей розклад
                var canEdit = await _scheduleRouteService.CanEditScheduleAsync(id);
                if (!canEdit.CanEdit)
                {
                    TempData["ErrorMessage"] = canEdit.Reason;
                    return RedirectToAction("Index");
                }

                // Валідація введених даних
                if (trainId <= 0)
                {
                    ModelState.AddModelError("", "Необхідно вибрати потяг");
                }

                if (scheduleDate.Date < DateTime.Today)
                {
                    ModelState.AddModelError("", "Дата не може бути в минулому");
                }

                if (!TimeSpan.TryParse(departureTime, out var parsedTime))
                {
                    ModelState.AddModelError("", "Невірний формат часу відправлення");
                }

                if (selectedDays == null || selectedDays.Length == 0)
                {
                    ModelState.AddModelError("", "Необхідно вибрати хоча б один день тижня");
                }

                if (!ModelState.IsValid)
                {
                    return await ReloadEditScheduleView(id);
                }

                // Формуємо рядок днів тижня
                string weekdaysString = string.Join(",", selectedDays);

                // Оновлюємо розклад
                var result = await _scheduleRouteService.UpdateScheduleAsync(id, trainId, scheduleDate, parsedTime, weekdaysString);

                if (result.Success)
                {
                    if (!string.IsNullOrEmpty(result.WarningMessage))
                    {
                        TempData["InfoMessage"] = result.WarningMessage;
                    }
                    else
                    {
                        TempData["SuccessMessage"] = "Розклад успішно оновлено!";
                    }
                    return RedirectToAction("ScheduleDetails", new { id = id });
                }
                else
                {
                    ModelState.AddModelError("", result.ErrorMessage);
                    return await ReloadEditScheduleView(id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при редагуванні розкладу {ScheduleId}", id);
                ModelState.AddModelError("", "Сталася помилка при редагуванні розкладу: " + ex.Message);
                return await ReloadEditScheduleView(id);
            }
        }

        // Деталі розкладу
        [HttpGet]
        public async Task<IActionResult> ScheduleDetails(int id)
        {
            var userRole = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            try
            {
                var schedule = await _scheduleRouteService.GetScheduleDetailsAsync(id);
                if (schedule == null)
                {
                    TempData["ErrorMessage"] = "Розклад не знайдено";
                    return RedirectToAction("Index");
                }

                return View(schedule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні деталей розкладу {ScheduleId}", id);
                TempData["ErrorMessage"] = "Помилка при завантаженні деталей розкладу";
                return RedirectToAction("Index");
            }
        }

        // API метод для переключения статуса расписания (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleScheduleStatus(int scheduleId)
        {
            try
            {
                var userRole = HttpContext.Session.GetString("user_role");
                if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
                {
                    return Json(new { success = false, message = "Немає доступу" });
                }

                if (scheduleId <= 0)
                {
                    return Json(new { success = false, message = "Некоректний ID розкладу" });
                }

                var result = await _scheduleRouteService.ToggleScheduleStatusAsync(scheduleId);

                if (result.Success)
                {
                    return Json(new
                    {
                        success = true,
                        message = result.Message,
                        newStatus = result.NewStatus,
                        statusText = result.NewStatus ? "Активний" : "Заморожений",
                        buttonText = result.NewStatus ? "Заморозити" : "Активувати",
                        buttonClass = result.NewStatus ? "btn-warning" : "btn-success"
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка зміни статусу розкладу {ScheduleId}", scheduleId);
                return Json(new
                {
                    success = false,
                    message = "Неочікувана помилка при зміні статусу розкладу"
                });
            }
        }

        // API метод для получения маршрута поезда (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetTrainRoute(int trainId)
        {
            try
            {
                var routeInfo = await _scheduleRouteService.GetTrainRouteAsync(trainId);

                return Json(new
                {
                    hasRoute = routeInfo.HasRoute,
                    trainNumber = routeInfo.TrainNumber,
                    route = routeInfo.Route
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні маршруту потяга {TrainId}", trainId);
                return Json(new
                {
                    hasRoute = false,
                    trainNumber = "Невідомо",
                    route = new List<object>(),
                    error = "Помилка при завантаженні маршруту"
                });
            }
        }

        // API метод для поиска расписаний с фильтрами (AJAX)
        [HttpGet]
        public async Task<IActionResult> SearchSchedules(int? trainId = null, DateTime? fromDate = null, DateTime? toDate = null, bool? isActive = null)
        {
            try
            {
                var schedules = await _scheduleRouteService.GetSchedulesWithFiltersAsync(trainId, fromDate, toDate, isActive);
                return Json(schedules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при пошуку розкладів з фільтрами");
                return Json(new List<object>());
            }
        }

        // Допоміжні приватні методи

        private async Task<IActionResult> ReloadCreateScheduleView()
        {
            try
            {
                var trains = await _scheduleRouteService.GetActiveTrainsAsync();
                ViewBag.Trains = trains;
                return View("CreateSchedule");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при перезавантаженні сторінки створення розкладу");
                ViewBag.Trains = new List<Dictionary<string, object>>();
                return View("CreateSchedule");
            }
        }

        private async Task<IActionResult> ReloadEditScheduleView(int scheduleId)
        {
            try
            {
                var schedule = await _scheduleRouteService.GetScheduleByIdAsync(scheduleId);
                var trains = await _scheduleRouteService.GetActiveTrainsAsync();
                var canEdit = await _scheduleRouteService.CanEditScheduleAsync(scheduleId);

                ViewBag.Trains = trains;
                ViewBag.CanEdit = canEdit;

                return View("EditSchedule", schedule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при перезавантаженні сторінки редагування розкладу {ScheduleId}", scheduleId);
                TempData["ErrorMessage"] = "Помилка при завантаженні даних для редагування";
                return RedirectToAction("Index");
            }
        }
    }
}
//using Microsoft.AspNetCore.Mvc;
//using train2.Models;
//using train4.Services.Implementation;
//using train4.Services.Interfaces;

//namespace train2.Controllers
//{
//    public class ScheduleController : Controller
//    {
//        private readonly ILogger<ScheduleController> _logger;
//        private readonly IScheduleRouteService _scheduleRouteService;
//        private readonly ITrainService _trainService;
//        private readonly IStationService _stationService;

//        public ScheduleController(
//            ILogger<ScheduleController> logger,
//            IScheduleRouteService scheduleRouteService,
//            ITrainService trainService,
//            IStationService stationService)
//        {
//            _logger = logger;
//            _scheduleRouteService = scheduleRouteService;
//            _trainService = trainService;
//            _stationService = stationService;
//        }

//        // Головна сторінка розкладів
//        [HttpGet]
//        public async Task<IActionResult> Index()
//        {
//            var userRole = HttpContext.Session.GetString("user_role");
//            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
//            {
//                return RedirectToAction("AccessDenied", "Account");
//            }

//            var schedules = await _scheduleRouteService.GetAllSchedulesAsync();
//            return View(schedules);
//        }

//        // Сторінка створення нового розкладу
//        [HttpGet]
//        public async Task<IActionResult> CreateSchedule()
//        {
//            var userRole = HttpContext.Session.GetString("user_role");
//            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
//            {
//                return RedirectToAction("AccessDenied", "Account");
//            }

//            var trains = await _trainService.GetAllTrainsAsync();
//            var activeTrains = trains.Where(t => t.is_active).ToList();

//            ViewBag.Trains = activeTrains;

//            return View();
//        }

//        // Обробка POST-запиту на створення розкладу
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> CreateSchedule(int trainId, DateTime scheduleDate, string departureTime, string[] selectedDays)
//        {
//            var userRole = HttpContext.Session.GetString("user_role");
//            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
//            {
//                return RedirectToAction("AccessDenied", "Account");
//            }

//            try
//            {
//                // Валідація
//                if (trainId <= 0)
//                {
//                    ModelState.AddModelError("", "Необхідно вибрати потяг");
//                    return await ReloadCreateScheduleView();
//                }

//                if (scheduleDate.Date < DateTime.Today)
//                {
//                    ModelState.AddModelError("", "Дата не може бути в минулому");
//                    return await ReloadCreateScheduleView();
//                }

//                if (!TimeSpan.TryParse(departureTime, out var parsedTime))
//                {
//                    ModelState.AddModelError("", "Невірний формат часу відправлення");
//                    return await ReloadCreateScheduleView();
//                }

//                if (selectedDays == null || selectedDays.Length == 0)
//                {
//                    ModelState.AddModelError("", "Необхідно вибрати хоча б один день тижня");
//                    return await ReloadCreateScheduleView();
//                }

//                // Перевіряємо чи існує маршрут для цього потяга
//                var routeInfo = await _scheduleRouteService.GetTrainRouteAsync(trainId);
//                if (!routeInfo.HasRoute)
//                {
//                    ModelState.AddModelError("", $"Для потяга №{routeInfo.TrainNumber} не налаштовано маршрут. Спочатку створіть маршрут у розділі 'Маршрути'.");
//                    return await ReloadCreateScheduleView();
//                }

//                // Формуємо рядок днів тижня
//                string weekdaysString = string.Join(",", selectedDays);

//                // Створюємо розклад
//                var result = await _scheduleRouteService.CreateScheduleAsync(trainId, scheduleDate, parsedTime, weekdaysString);

//                if (result.Success)
//                {
//                    TempData["SuccessMessage"] = $"Розклад для потяга №{routeInfo.TrainNumber} успішно створено!";
//                    return RedirectToAction("Index");
//                }
//                else
//                {
//                    ModelState.AddModelError("", result.ErrorMessage);
//                    return await ReloadCreateScheduleView();
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Помилка при створенні розкладу");
//                ModelState.AddModelError("", "Сталася помилка при створенні розкладу: " + ex.Message);
//                return await ReloadCreateScheduleView();
//            }
//        }

//        // Сторінка редагування розкладу
//        [HttpGet]
//        public async Task<IActionResult> EditSchedule(int id)
//        {
//            var userRole = HttpContext.Session.GetString("user_role");
//            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
//            {
//                return RedirectToAction("AccessDenied", "Account");
//            }

//            var schedule = await _scheduleRouteService.GetScheduleByIdAsync(id);
//            if (schedule == null)
//            {
//                TempData["ErrorMessage"] = "Розклад не знайдено";
//                return RedirectToAction("Index");
//            }

//            // Перевіряємо чи можна редагувати цей розклад
//            var canEdit = await _scheduleRouteService.CanEditScheduleAsync(id);
//            if (!canEdit.CanEdit)
//            {
//                TempData["ErrorMessage"] = canEdit.Reason;
//                return RedirectToAction("Index");
//            }

//            var trains = await _trainService.GetAllTrainsAsync();
//            var activeTrains = trains.Where(t => t.is_active).ToList();

//            ViewBag.Trains = activeTrains;
//            ViewBag.CanEdit = canEdit;

//            return View(schedule);
//        }

//        // Обробка POST-запиту на редагування розкладу
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> EditSchedule(int id, int trainId, DateTime scheduleDate, string departureTime, string[] selectedDays)
//        {
//            var userRole = HttpContext.Session.GetString("user_role");
//            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
//            {
//                return RedirectToAction("AccessDenied", "Account");
//            }

//            try
//            {
//                // Перевіряємо чи можна редагувати цей розклад
//                var canEdit = await _scheduleRouteService.CanEditScheduleAsync(id);
//                if (!canEdit.CanEdit)
//                {
//                    TempData["ErrorMessage"] = canEdit.Reason;
//                    return RedirectToAction("Index");
//                }

//                // Валідація
//                if (trainId <= 0)
//                {
//                    ModelState.AddModelError("", "Необхідно вибрати потяг");
//                    return await ReloadEditScheduleView(id);
//                }

//                if (scheduleDate.Date < DateTime.Today)
//                {
//                    ModelState.AddModelError("", "Дата не може бути в минулому");
//                    return await ReloadEditScheduleView(id);
//                }

//                if (!TimeSpan.TryParse(departureTime, out var parsedTime))
//                {
//                    ModelState.AddModelError("", "Невірний формат часу відправлення");
//                    return await ReloadEditScheduleView(id);
//                }

//                if (selectedDays == null || selectedDays.Length == 0)
//                {
//                    ModelState.AddModelError("", "Необхідно вибрати хоча б один день тижня");
//                    return await ReloadEditScheduleView(id);
//                }

//                // Формуємо рядок днів тижня
//                string weekdaysString = string.Join(",", selectedDays);

//                // Оновлюємо розклад
//                var result = await _scheduleRouteService.UpdateScheduleAsync(id, trainId, scheduleDate, parsedTime, weekdaysString);

//                if (result.Success)
//                {
//                    if (!string.IsNullOrEmpty(result.WarningMessage))
//                    {
//                        TempData["InfoMessage"] = result.WarningMessage;
//                    }
//                    else
//                    {
//                        TempData["SuccessMessage"] = "Розклад успішно оновлено!";
//                    }
//                    return RedirectToAction("Index");
//                }
//                else
//                {
//                    ModelState.AddModelError("", result.ErrorMessage);
//                    return await ReloadEditScheduleView(id);
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Помилка при редагуванні розкладу {ScheduleId}", id);
//                ModelState.AddModelError("", "Сталася помилка при редагуванні розкладу: " + ex.Message);
//                return await ReloadEditScheduleView(id);
//            }
//        }

//        // Заморозка/розморозка розкладу
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> ToggleScheduleStatus(int scheduleId)
//        {
//            try
//            {
//                var userRole = HttpContext.Session.GetString("user_role");
//                if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
//                {
//                    return Json(new { success = false, message = "Немає доступу" });
//                }

//                if (scheduleId <= 0)
//                {
//                    return Json(new { success = false, message = "Некоректний ID розкладу" });
//                }

//                var result = await _scheduleRouteService.ToggleScheduleStatusAsync(scheduleId);

//                if (result.Success)
//                {
//                    return Json(new
//                    {
//                        success = true,
//                        message = result.Message,
//                        newStatus = result.NewStatus,
//                        statusText = result.NewStatus ? "Активний" : "Заморожений",
//                        buttonText = result.NewStatus ? "Заморозити" : "Активувати",
//                        buttonClass = result.NewStatus ? "btn-warning" : "btn-success"
//                    });
//                }
//                else
//                {
//                    return Json(new
//                    {
//                        success = false,
//                        message = result.Message
//                    });
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Помилка зміни статусу розкладу {ScheduleId}", scheduleId);
//                return Json(new
//                {
//                    success = false,
//                    message = "Неочікувана помилка при зміні статусу розкладу"
//                });
//            }
//        }

//        // Деталі розкладу
//        [HttpGet]
//        public async Task<IActionResult> ScheduleDetails(int id)
//        {
//            var userRole = HttpContext.Session.GetString("user_role");
//            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
//            {
//                return RedirectToAction("AccessDenied", "Account");
//            }

//            var schedule = await _scheduleRouteService.GetScheduleDetailsAsync(id);
//            if (schedule == null)
//            {
//                TempData["ErrorMessage"] = "Розклад не знайдено";
//                return RedirectToAction("Index");
//            }

//            return View(schedule);
//        }

//        // Допоміжні методи
//        private async Task<IActionResult> ReloadCreateScheduleView()
//        {
//            var trains = await _trainService.GetAllTrainsAsync();
//            var activeTrains = trains.Where(t => t.is_active).ToList();
//            ViewBag.Trains = activeTrains;
//            return View("CreateSchedule");
//        }

//        private async Task<IActionResult> ReloadEditScheduleView(int scheduleId)
//        {
//            var schedule = await _scheduleRouteService.GetScheduleByIdAsync(scheduleId);
//            var trains = await _trainService.GetAllTrainsAsync();
//            var activeTrains = trains.Where(t => t.is_active).ToList();
//            var canEdit = await _scheduleRouteService.CanEditScheduleAsync(scheduleId);

//            ViewBag.Trains = activeTrains;
//            ViewBag.CanEdit = canEdit;

//            return View("EditSchedule", schedule);
//        }
//    }
//}
