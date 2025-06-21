using Microsoft.AspNetCore.Mvc;
using train2.Models;
using train4.Services.Interfaces;

namespace train2.Controllers
{
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly IAuthenticationService _authenticationService;
        private readonly ITicketService _ticketService;

        public AccountController(
            ILogger<AccountController> logger,
            IAuthenticationService authenticationService,
            ITicketService ticketService)
        {
            _logger = logger;
            _authenticationService = authenticationService;
            _ticketService = ticketService;
        }

        public IActionResult Login()
        {
            // Перевіряємо, чи користувач уже авторизований
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("db_user")))
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        //[HttpPost]
        //public async Task<IActionResult> Login(string login, string password, string email)
        //{
        //    if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        //    {
        //        ModelState.AddModelError("", "Логін та пароль обов'язкові");
        //        return View();
        //    }
        //    try
        //    {
        //        // Используем AuthenticationService
        //        var result = await _authenticationService.ConnectAsync(login, password, email);
        //        if (result.Success)
        //        {
        //            // Отримуємо інформацію про клієнта
        //            var client = await _authenticationService.GetClientByLoginAsync(login);
        //            if (client != null)
        //            {
        //                HttpContext.Session.SetString("client_name", client.client_name);
        //                HttpContext.Session.SetString("client_surname", client.client_surname);
        //                HttpContext.Session.SetInt32("client_id", client.client_id);
        //            }
        //            // Отримуємо поточну роль користувача
        //            var userInfo = await _authenticationService.GetCurrentUserInfoAsync();
        //            HttpContext.Session.SetString("user_role", userInfo.Role);
        //            // Якщо авторизація успішна, перенаправляємо на головну сторінку
        //            TempData["SuccessMessage"] = "Авторизація успішна!";
        //            return RedirectToAction("Index", "Home");
        //        }
        //        else
        //        {
        //            // Якщо виникла помилка, показуємо повідомлення
        //            ModelState.AddModelError("", result.ErrorMessage ?? "Помилка авторизації");
        //            return View();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Помилка при авторизації");
        //        ModelState.AddModelError("", "Сталася неочікувана помилка при авторизації");
        //        return View();
        //    }
        //}
        [HttpPost]
        public async Task<IActionResult> Login(string login, string password, string email)
        {
            // Сохраняем введенные данные для повторного отображения (кроме пароля)
            ViewBag.Login = login;
            ViewBag.Email = email;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            {
                TempData["ErrorMessage"] = "Будь ласка, заповніть всі поля";
                return View();
            }

            try
            {
                // Проверяем корректность логина и email
                var isValidUser = await _authenticationService.ValidateUserCredentialsAsync(login, email);
                if (!isValidUser)
                {
                    TempData["ErrorMessage"] = "Користувача з таким логіном і email не знайдено";
                    return View();
                }

                // Пытаемся подключиться
                var result = await _authenticationService.ConnectAsync(login, password, email);

                if (result.Success)
                {
                    // ДОБАВЛЯЕМ ОБРАТНО КОД ПОЛУЧЕНИЯ РОЛИ И ИНФОРМАЦИИ О КЛИЕНТЕ

                    // Отримуємо інформацію про клієнта
                    var client = await _authenticationService.GetClientByLoginAsync(login);
                    if (client != null)
                    {
                        HttpContext.Session.SetString("client_name", client.client_name);
                        HttpContext.Session.SetString("client_surname", client.client_surname);
                        HttpContext.Session.SetInt32("client_id", client.client_id);
                    }

                    // Отримуємо поточну роль користувача
                    var userInfo = await _authenticationService.GetCurrentUserInfoAsync();
                    HttpContext.Session.SetString("user_role", userInfo.Role);

                    // Логируем успешный вход
                    _logger.LogInformation("Успішний вхід користувача {Login} з роллю {Role}", login, userInfo.Role);

                    TempData["SuccessMessage"] = "Авторизація успішна!";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    // Определяем тип ошибки и показываем понятное сообщение
                    if (result.ErrorMessage.Contains("authentication failed") ||
                        result.ErrorMessage.Contains("password") ||
                        result.ErrorMessage.Contains("28P01"))
                    {
                        TempData["ErrorMessage"] = "Неправильний логін чи пароль";
                    }
                    else if (result.ErrorMessage.Contains("database") && result.ErrorMessage.Contains("does not exist"))
                    {
                        TempData["ErrorMessage"] = "Користувача не знайдено в системі";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Помилка входу в систему. Перевірте дані та спробуйте ще раз";
                    }

                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при спробі входу користувача {Login}", login);
                TempData["ErrorMessage"] = "Виникла помилка сервера. Спробуйте пізніше";
                return View();
            }
        }

        public async Task<IActionResult> Logout()
        {
            await _authenticationService.LogoutAsync();
            return RedirectToAction("Index", "Home");
        }

        //[HttpGet]
        //public async Task<IActionResult> Profile()
        //{
        //    // Перевіряємо, чи користувач авторизований
        //    var clientId = HttpContext.Session.GetInt32("client_id");
        //    var login = HttpContext.Session.GetString("db_user");

        //    if (clientId == null || string.IsNullOrEmpty(login))
        //    {
        //        return RedirectToAction("Login");
        //    }
        //    try
        //    {
        //        // Отримуємо інформацію про клієнта через AuthenticationService
        //        var client = await _authenticationService.GetClientByLoginAsync(login);
        //        if (client == null)
        //        {
        //            return RedirectToAction("Login");
        //        }

        //        // Отримуємо активні та історичні квитки через TicketService
        //        var activeTickets = await _ticketService.GetActiveTicketsAsync(clientId.Value);
        //        var historicalTickets = await _ticketService.GetHistoricalTicketsAsync(clientId.Value);

        //        // Передаємо дані у ViewBag для використання у представленні
        //        ViewBag.ActiveTickets = activeTickets;
        //        ViewBag.HistoricalTickets = historicalTickets;

        //        return View(client);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Помилка при завантаженні профілю користувача");
        //        return View("Error");
        //    }
        //}

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var clientId = HttpContext.Session.GetInt32("client_id");
            var login = HttpContext.Session.GetString("db_user");

            if (clientId == null || string.IsNullOrEmpty(login))
            {
                return RedirectToAction("Login");
            }

            try
            {
                // Получаем данные клиента
                var client = await _authenticationService.GetClientByLoginAsync(login);
                if (client == null)
                {
                    return RedirectToAction("Login");
                }

                // Логируем данные для отладки
                _logger.LogInformation("Client data: Name={Name}, Surname={Surname}, Patronymic={Patronymic}, Phone={Phone}, Email={Email}, PaymentInfo={PaymentInfo}",
                    client.client_name, client.client_surname, client.client_patronymic, client.phone_number, client.email, client.payment_info);

                // Получаем квитки
                var activeTickets = await _ticketService.GetActiveTicketsAsync(clientId.Value);
                var historicalTickets = await _ticketService.GetHistoricalTicketsAsync(clientId.Value);

                ViewBag.ActiveTickets = activeTickets;
                ViewBag.HistoricalTickets = historicalTickets;

                return View(client);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при завантаженні профілю користувача");
                return View("Error");
            }
        }
        //[HttpPost]
        //public async Task<IActionResult> UpdateProfile(Client model)
        //{
        //    try
        //    {
        //        // Используем AuthenticationService для обновления профиля
        //        var result = await _authenticationService.UpdateClientAsync(model);

        //        if (result.Success)
        //        {
        //            TempData["SuccessMessage"] = "Дані успішно оновлено!";
        //        }
        //        else
        //        {
        //            TempData["ErrorMessage"] = result.ErrorMessage;
        //        }

        //        return RedirectToAction("Profile");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Помилка при оновленні профілю");
        //        TempData["ErrorMessage"] = "Сталася неочікувана помилка при оновленні профілю";
        //        return RedirectToAction("Profile");
        //    }
        //}
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(Client model)
        {
            try
            {
                var clientId = HttpContext.Session.GetInt32("client_id");
                if (clientId == null)
                {
                    return RedirectToAction("Login");
                }

                model.client_id = clientId.Value;

                // Получаем кортеж, а не bool
                var result = await _authenticationService.UpdateClientAsync(model);

                if (result.Success)
                {
                    HttpContext.Session.SetString("client_name", model.client_name);
                    HttpContext.Session.SetString("client_surname", model.client_surname);
                    TempData["SuccessMessage"] = "Дані успішно оновлено!";
                }
                else
                {
                    TempData["ErrorMessage"] = result.ErrorMessage ?? "Помилка при оновленні профілю або клієнта не знайдено";
                }

                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при оновленні профілю");
                TempData["ErrorMessage"] = "Сталася неочікувана помилка при оновленні профілю";
                return RedirectToAction("Profile");
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            // Перевіряємо, чи користувач уже авторизований
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("db_user")))
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Client client, string password, string confirmPassword)
        {
            try
            {
                // Перевірка паролів
                if (password != confirmPassword)
                {
                    ModelState.AddModelError("", "Паролі не співпадають");
                    return View(client);
                }

                // Перевірка обов'язковості платіжної інформації
                if (string.IsNullOrWhiteSpace(client.payment_info))
                {
                    ModelState.AddModelError(nameof(client.payment_info), "Платіжна інформація обов'язкова для заповнення");
                    return View(client);
                }

                if (!ModelState.IsValid)
                {
                    return View(client);
                }

                // Створення клієнта через AuthenticationService
                var result = await _authenticationService.CreateClientAsync(client, password);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = "Реєстрація успішно завершена! Можете увійти в систему.";
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError("", result.ErrorMessage);
                    return View(client);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при реєстрації");
                ModelState.AddModelError("", "Сталася неочікувана помилка при реєстрації");
                return View(client);
            }
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
