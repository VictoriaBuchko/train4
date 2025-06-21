using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using train2.Models;
using train4.Services.Implementation;
using train4.Services.Interfaces;

namespace train2.Controllers
{
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly ITrainService _trainService;
        private readonly IStationService _stationService;
        private readonly IScheduleService _scheduleService;
        private readonly IAnalyticsService _analyticsService;

        public AdminController(
            ILogger<AdminController> logger,
            ITrainService trainService,
            IStationService stationService,
            IScheduleService scheduleService,
            IAnalyticsService analyticsService)
        {
            _logger = logger;
            _trainService = trainService;
            _stationService = stationService;
            _scheduleService = scheduleService;
            _analyticsService = analyticsService;
        }

        // Головна сторінка адміністратора
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Перевірка ролі користувача (адміністратор)
            var userRole = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // Отримання списку всіх потягів для відображення
            var trains = await _trainService.GetAllTrainsAsync();
            return View(trains);
        }

        // Сторінка створення нового потяга
        [HttpGet]
        public async Task<IActionResult> CreateTrain()
        {
            // Перевірка ролі користувача (адміністратор)
            var userRole = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // Отримання типів вагонів для вибору
            var carriageTypes = await _trainService.GetAllCarriageTypesAsync();
            ViewBag.CarriageTypes = carriageTypes;

            return View();
        }

        // Обробка POST-запиту на створення потяга
        [HttpPost]
        public async Task<IActionResult> CreateTrain(int trainNumber, int carriageCount, List<int> carriageTypeIds, List<int> carriageNumbers)
        {
            // Перевірка ролі користувача (адміністратор)
            var userRole = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // Перевірка наявності даних
            if (carriageTypeIds == null || carriageTypeIds.Count == 0 || carriageNumbers == null || carriageNumbers.Count == 0)
            {
                ModelState.AddModelError("", "Необхідно вказати типи вагонів та їх номери");
                var carriageTypes = await _trainService.GetAllCarriageTypesAsync();
                ViewBag.CarriageTypes = carriageTypes;
                return View();
            }

            // Перевірка кількості вагонів
            if (carriageTypeIds.Count != carriageCount || carriageNumbers.Count != carriageCount)
            {
                ModelState.AddModelError("", "Кількість типів вагонів та їх номерів повинна відповідати загальній кількості вагонів");
                var carriageTypes = await _trainService.GetAllCarriageTypesAsync();
                ViewBag.CarriageTypes = carriageTypes;
                return View();
            }

            try
            {
                // Створення нового потяга
                var train = new Train
                {
                    train_number = trainNumber,
                    carriage_count = carriageCount
                };

                // Виклик сервісу для створення потяга з вагонами
                var result = await _trainService.CreateTrainWithCarriagesAsync(trainNumber, carriageTypeIds.ToArray(), carriageNumbers.ToArray());

                if (result.Success)
                {
                    TempData["SuccessMessage"] = "Потяг успішно створено!";
                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", result.ErrorMessage);
                    var carriageTypes = await _trainService.GetAllCarriageTypesAsync();
                    ViewBag.CarriageTypes = carriageTypes;
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при створенні потяга");
                ModelState.AddModelError("", "Сталася помилка при створенні потяга: " + ex.Message);
                var carriageTypes = await _trainService.GetAllCarriageTypesAsync();
                ViewBag.CarriageTypes = carriageTypes;
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> TrainDetails(int id)
        {
            var userRole = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var (train, seatCounts) = await _trainService.GetTrainDetailsAsync(id);
            if (train == null)
            {
                return NotFound();
            }

            ViewBag.SeatCounts = seatCounts;
            return View(train);
        }

        [HttpGet]
        public IActionResult AdminMenu()
        {
            var userRole = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> EditTrain(int id)
        {
            try
            {
                var userRole = HttpContext.Session.GetString("user_role");
                if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
                {
                    return RedirectToAction("AccessDenied", "Account");
                }

                var (train, carriages) = await _trainService.GetTrainForEditAsync(id);

                if (train == null)
                {
                    TempData["ErrorMessage"] = "Потяг не знайдено";
                    return RedirectToAction("Index");
                }

                // Отримуємо список типів вагонів для вибору
                var carriageTypes = await _trainService.GetAllCarriageTypesAsync();
                ViewBag.CarriageTypes = carriageTypes;

                // Передаємо інформацію про існуючі вагони
                ViewBag.ExistingCarriages = carriages;

                // Отримуємо інформацію про вагони з бронюваннями
                var carriagesWithBookings = await _trainService.GetCarriagesWithBookingsAsync(id);
                ViewBag.CarriagesWithBookings = carriagesWithBookings;

                return View(train);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка при отриманні потяга для редагування {id}");
                TempData["ErrorMessage"] = "Помилка при завантаженні потяга для редагування";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTrain(int id, int trainNumber, int[] carriageTypeIds, int[] carriageNumbers, int[] existingCarriageIds)
        {
            try
            {
                var userRole = HttpContext.Session.GetString("user_role");
                if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
                {
                    return RedirectToAction("AccessDenied", "Account");
                }

                // Логирование полученных данных для диагностики
                _logger.LogInformation($"EditTrain POST: id={id}, trainNumber={trainNumber}");
                _logger.LogInformation($"carriageTypeIds: [{string.Join(", ", carriageTypeIds ?? new int[0])}]");
                _logger.LogInformation($"carriageNumbers: [{string.Join(", ", carriageNumbers ?? new int[0])}]");
                _logger.LogInformation($"existingCarriageIds: [{string.Join(", ", existingCarriageIds ?? new int[0])}]");

                // Базовая валидация
                if (trainNumber <= 0)
                {
                    TempData["ErrorMessage"] = "Номер потяга має бути більше 0";
                    return await ReloadEditTrainView(id);
                }

                if (carriageTypeIds == null || carriageTypeIds.Length == 0)
                {
                    TempData["ErrorMessage"] = "Потяг має мати хоча б один вагон";
                    return await ReloadEditTrainView(id);
                }

                if (carriageTypeIds.Length != carriageNumbers.Length)
                {
                    TempData["ErrorMessage"] = "Кількість типів вагонів не відповідає кількості номерів вагонів";
                    return await ReloadEditTrainView(id);
                }

                // Исправляем массив existingCarriageIds если нужно
                if (existingCarriageIds == null)
                {
                    existingCarriageIds = new int[carriageTypeIds.Length];
                }
                else if (existingCarriageIds.Length != carriageTypeIds.Length)
                {
                    var newArray = new int[carriageTypeIds.Length];
                    Array.Copy(existingCarriageIds, newArray, Math.Min(existingCarriageIds.Length, carriageTypeIds.Length));
                    existingCarriageIds = newArray;
                }

                // Проверяем уникальность номеров вагонов
                if (carriageNumbers.Length != carriageNumbers.Distinct().Count())
                {
                    TempData["ErrorMessage"] = "Номери вагонів повинні бути унікальними";
                    return await ReloadEditTrainView(id);
                }

                // Формируем данные для обновления
                var carriageData = new List<Dictionary<string, object>>();

                for (int i = 0; i < carriageTypeIds.Length; i++)
                {
                    var carriage = new Dictionary<string, object>
                    {
                        ["carriage_type_id"] = carriageTypeIds[i],
                        ["carriage_number"] = carriageNumbers[i]
                    };

                    // Если это существующий вагон, добавляем его ID
                    if (existingCarriageIds[i] > 0)
                    {
                        carriage["train_carriage_types_id"] = existingCarriageIds[i];
                    }

                    carriageData.Add(carriage);
                }

                // Логирование данных перед отправкой
                _logger.LogInformation($"Carriage data count: {carriageData.Count}");
                foreach (var item in carriageData)
                {
                    _logger.LogInformation($"Carriage: {string.Join(", ", item.Select(kv => $"{kv.Key}={kv.Value}"))}");
                }

                // Обновляем поезд
                var result = await _trainService.UpdateTrainAsync(id, trainNumber, carriageData);

                if (result.Success)
                {
                    // Показываем информационное сообщение о результате
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        TempData["InfoMessage"] = result.ErrorMessage; // Информация о защищенных вагонах
                    }
                    else
                    {
                        TempData["SuccessMessage"] = $"Потяг №{trainNumber} успішно оновлено";
                    }
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return await ReloadEditTrainView(id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка при оновленні потяга {id}");
                TempData["ErrorMessage"] = "Неочікувана помилка при оновленні потяга";
                return await ReloadEditTrainView(id);
            }
        }

        private async Task<IActionResult> ReloadEditTrainView(int trainId)
        {
            try
            {
                var (train, carriages) = await _trainService.GetTrainForEditAsync(trainId);
                var carriageTypes = await _trainService.GetAllCarriageTypesAsync();
                var carriagesWithBookings = await _trainService.GetCarriagesWithBookingsAsync(trainId);

                ViewBag.CarriageTypes = carriageTypes;
                ViewBag.ExistingCarriages = carriages ?? new List<Dictionary<string, object>>();
                ViewBag.CarriagesWithBookings = carriagesWithBookings;

                return View("EditTrain", train);
            }
            catch
            {
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTrainStatus(int trainId)
        {
            try
            {
                var userRole = HttpContext.Session.GetString("user_role");
                if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
                {
                    return Json(new { success = false, message = "Немає доступу" });
                }

                // Логування для діагностики
                _logger.LogInformation($"Attempting to toggle train status for ID: {trainId}");

                if (trainId <= 0)
                {
                    _logger.LogWarning($"Invalid train ID received: {trainId}");
                    return Json(new { success = false, message = "Некоректний ID потяга" });
                }

                var result = await _trainService.ToggleTrainStatusAsync(trainId);

                _logger.LogInformation($"Toggle result: Success={result.Success}, Message={result.Message}, NewStatus={result.NewStatus}");

                if (result.Success)
                {
                    return Json(new
                    {
                        success = true,
                        message = result.Message,
                        newStatus = result.NewStatus,
                        statusText = result.NewStatus ? "Активний" : "Неактивний",
                        buttonText = result.NewStatus ? "Деактивувати" : "Активувати",
                        buttonClass = result.NewStatus ? "btn-danger" : "btn-success"
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
                _logger.LogError(ex, $"Помилка зміни статусу потяга {trainId}");
                return Json(new
                {
                    success = false,
                    message = "Неочікувана помилка при зміні статусу потяга"
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> RouteIndex()
        {
            var trains = await _trainService.GetAllTrainsAsync();
            return View(trains);
        }

        [HttpGet]
        public async Task<IActionResult> RouteDetails(int trainId)
        {
            var (trainNumber, route) = await _scheduleService.GetRouteByTrainIdAsync(trainId);
            ViewBag.TrainNumber = trainNumber;
            return View(route);
        }

        [HttpGet]
        public async Task<IActionResult> ManageStations()
        {
            var stations = await _stationService.GetAllStationsAsync();
            return View(stations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStation(string newStationName)
        {
            if (string.IsNullOrWhiteSpace(newStationName))
            {
                TempData["ErrorMessage"] = "Назва станції не може бути порожньою.";
                return RedirectToAction("ManageStations");
            }

            var result = await _stationService.AddStationAsync(newStationName.Trim());
            TempData["ResultMessage"] = result;
            return RedirectToAction("ManageStations");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStation(int stationId)
        {
            var result = await _stationService.DeleteStationAsync(stationId);
            TempData["ResultMessage"] = result;
            return RedirectToAction("ManageStations");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStationName(int stationId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                TempData["ErrorMessage"] = "Назва станції не може бути порожньою.";
                return RedirectToAction("ManageStations");
            }

            var result = await _stationService.UpdateStationNameAsync(stationId, newName.Trim());
            TempData["ResultMessage"] = result;
            return RedirectToAction("ManageStations");
        }

        [HttpGet]
        public async Task<IActionResult> CreateRoute()
        {
            var trains = await _trainService.GetAllTrainsAsync();
            var stations = await _stationService.GetAllStationsAsync();

            ViewBag.Trains = trains;
            ViewBag.Stations = stations;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRoute(int trainId, List<int> stationIds)
        {
            if (stationIds == null || stationIds.Count < 2)
            {
                TempData["ErrorMessage"] = "Маршрут повинен містити щонайменше дві станції.";
                return RedirectToAction("CreateRoute");
            }

            var result = await _scheduleService.CreateRouteAsync(trainId, stationIds);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "Маршрут успішно створено!";
                return RedirectToAction("RouteIndex");
            }

            TempData["ErrorMessage"] = "Помилка створення маршруту: " + result.ErrorMessage;
            return RedirectToAction("CreateRoute");
        }


        //[HttpGet]
        //public async Task<IActionResult> EditRoute(int trainId)
        //{
        //    var (route, canFullyEdit) = await _scheduleService.GetRouteWithEditPermissionAsync(trainId);

        //    ViewBag.TrainId = trainId;
        //    ViewBag.CanFullyEdit = canFullyEdit;

        //    return View(route); // передаємо список станцій маршруту
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> EditRoute(int trainId, List<StationSequence> updatedRoute)
        //{
        //    if (!ModelState.IsValid)
        //        return View(updatedRoute);

        //    bool success = await _scheduleService.UpdateRouteDurationsAndPricesAsync(trainId, updatedRoute);

        //    if (success)
        //    {
        //        TempData["SuccessMessage"] = "Маршрут оновлено успішно.";
        //        return RedirectToAction("RouteDetails", new { trainId });
        //    }

        //    TempData["ErrorMessage"] = "Помилка при оновленні маршруту.";
        //    return View(updatedRoute);
        //}




        [HttpGet]
        public IActionResult Analytics()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Analytics(DateTime startDate, DateTime endDate)
        {
            ViewBag.Start = startDate;
            ViewBag.End = endDate;

            ViewBag.TopFilled = await _analyticsService.GetTopFilledRoutesAsync(startDate, endDate);
            ViewBag.TopIncome = await _analyticsService.GetTopIncomeRoutesAsync(startDate, endDate);
            ViewBag.PopularRoutes = await _analyticsService.GetMostPopularRoutesAsync(startDate, endDate);

            return View();
        }


        // GET метод для відображення форми редагування
        [HttpGet]
        public async Task<IActionResult> EditRoute(int trainId)
        {
            try
            {
                var userRole = HttpContext.Session.GetString("user_role");
                if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
                {
                    return RedirectToAction("AccessDenied", "Account");
                }

                _logger.LogInformation($"Отримання маршруту для редагування, trainId: {trainId}");

                // Викликаємо метод з сервісу
                var (route, canFullyEdit) = await _scheduleService.GetRouteWithEditPermissionAsync(trainId);

                if (route == null || route.Count == 0)
                {
                    TempData["ErrorMessage"] = "Маршрут для цього потяга не знайдено.";
                    return RedirectToAction("RouteIndex");
                }

                // Отримуємо номер потяга для відображення
                var trains = await _trainService.GetAllTrainsAsync();
                var currentTrain = trains.FirstOrDefault(t => t.train_id == trainId);

                ViewBag.TrainId = trainId;
                ViewBag.TrainNumber = currentTrain?.train_number ?? trainId;
                ViewBag.CanFullyEdit = canFullyEdit;

                _logger.LogInformation($"Передача {route.Count} станцій для редагування потяга {trainId}");

                return View(route);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка при завантаженні сторінки редагування маршруту для потяга {trainId}");
                TempData["ErrorMessage"] = "Помилка при завантаженні маршруту для редагування.";
                return RedirectToAction("RouteIndex");
            }
        }

        // POST метод для збереження змін
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoute(int trainId, List<StationSequence> updatedRoute)
        {
            try
            {
                var userRole = HttpContext.Session.GetString("user_role");
                if (string.IsNullOrEmpty(userRole) || userRole != "group_admins")
                {
                    return RedirectToAction("AccessDenied", "Account");
                }

                _logger.LogInformation($"POST EditRoute викликано для trainId: {trainId}");
                _logger.LogInformation($"Кількість станцій для оновлення: {updatedRoute?.Count ?? 0}");

                // Видаляємо помилки валідації для навігаційних властивостей, які не є критичними
                var keysToRemove = ModelState.Keys.Where(k =>
                    k.Contains("NextStation") ||
                    k.Contains("PreviousStation") ||
                    k.Contains("NextStations") ||
                    k.Contains("PreviousStations") ||
                    k.Contains("Train.carriage_count") ||
                    k.Contains("Train.TrainCarriageTypes") ||
                    k.Contains("Train.Schedules") ||
                    k.Contains("Train.StationSequences") ||
                    k.Contains("Stations.StationSequences") ||
                    k.Contains("Stations.TicketsFrom") ||
                    k.Contains("Stations.TicketsTo") ||
                    k.Contains("Stations.PricesFrom") ||
                    k.Contains("Stations.PricesTo")).ToList();

                foreach (var key in keysToRemove)
                {
                    ModelState.Remove(key);
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState не валідний");
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning($"ModelState помилка: {error.ErrorMessage}");
                    }
                    TempData["ErrorMessage"] = "Некоректні дані для оновлення маршруту.";
                    return RedirectToAction("EditRoute", new { trainId });
                }

                if (updatedRoute == null || updatedRoute.Count == 0)
                {
                    _logger.LogWarning("Оновлений маршрут порожній");
                    TempData["ErrorMessage"] = "Маршрут не може бути порожнім.";
                    return RedirectToAction("EditRoute", new { trainId });
                }

                // Валідація даних
                foreach (var station in updatedRoute)
                {
                    if (station.travel_duration.TotalMinutes <= 0)
                    {
                        _logger.LogWarning($"Некоректна тривалість для станції {station.station_id}");
                        TempData["ErrorMessage"] = "Тривалість подорожі повинна бути більше 0.";
                        return RedirectToAction("EditRoute", new { trainId });
                    }

                    if (station.distance_km <= 0)
                    {
                        _logger.LogWarning($"Некоректна відстань для станції {station.station_id}");
                        TempData["ErrorMessage"] = "Відстань повинна бути більше 0 км.";
                        return RedirectToAction("EditRoute", new { trainId });
                    }

                    // Перевіряємо, що sequence_id та train_id відповідають
                    if (station.train_id != trainId)
                    {
                        _logger.LogWarning($"train_id станції {station.station_id} не співпадає з переданим trainId");
                        TempData["ErrorMessage"] = "Помилка валідації даних маршруту.";
                        return RedirectToAction("EditRoute", new { trainId });
                    }
                }

                _logger.LogInformation("Валідація пройдена успішно, викликаємо сервіс оновлення");

                // Викликаємо сервіс для оновлення
                bool success = await _scheduleService.UpdateRouteDurationsAndPricesAsync(trainId, updatedRoute);

                if (success)
                {
                    _logger.LogInformation($"Маршрут для поїзда {trainId} успішно оновлено");
                    TempData["SuccessMessage"] = "Маршрут успішно оновлено!";
                    return RedirectToAction("RouteDetails", new { trainId });
                }
                else
                {
                    _logger.LogError($"Не вдалося оновити маршрут для поїзда {trainId}");
                    TempData["ErrorMessage"] = "Помилка при оновленні маршруту.";
                    return RedirectToAction("EditRoute", new { trainId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Виняток при редагуванні маршруту для поїзда {trainId}");
                TempData["ErrorMessage"] = "Неочікувана помилка при оновленні маршруту.";
                return RedirectToAction("EditRoute", new { trainId });
            }
        }
    }
}