using Microsoft.AspNetCore.Mvc;
using train2.Models;
using train4.Services.Interfaces;

namespace train3.Controllers
{
    public class AccountantController : Controller
    {
        private readonly IAccountingService _accountingService;
        private readonly ILogger<AccountantController> _logger;

        public AccountantController(
            IAccountingService accountingService,
            ILogger<AccountantController> logger)
        {
            _accountingService = accountingService;
            _logger = logger;
        }

        // Проверка авторизации бухгалтера
        private bool IsAccountantAuthorized()
        {
            var userRole = HttpContext.Session.GetString("user_role");
            return userRole == "group_accountants" || userRole == "group_admins";
        }

        // Главная панель бухгалтера
        public async Task<IActionResult> Index()
        {
            if (!IsAccountantAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу до панелі бухгалтера.";
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // Страница подтверждения оплат
        [HttpGet]
        public async Task<IActionResult> PaymentConfirmation()
        {
            if (!IsAccountantAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу до панелі бухгалтера.";
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // API для получения неоплаченных билетов
        [HttpGet]
        public async Task<IActionResult> GetUnpaidTickets(int page = 1, int pageSize = 20, string searchQuery = "")
        {
            if (!IsAccountantAuthorized())
            {
                return Json(new { error = "Unauthorized" });
            }

            try
            {
                // Используем AccountingService
                var tickets = await _accountingService.GetUnpaidTicketsAsync(page, pageSize, searchQuery);
                var totalCount = await _accountingService.GetUnpaidTicketsCountAsync(searchQuery);

                return Json(new
                {
                    tickets = tickets,
                    totalCount = totalCount,
                    currentPage = page,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при отриманні неоплачених квитків");
                return Json(new { error = "Помилка при отриманні даних" });
            }
        }

        // API для подтверждения оплаты билета
        [HttpPost]
        public async Task<IActionResult> ConfirmPayment(int ticketId, decimal paidAmount)
        {
            if (!IsAccountantAuthorized())
            {
                return Json(new { success = false, message = "У вас немає прав доступу." });
            }

            try
            {
                // Используем AccountingService
                var result = await _accountingService.ConfirmTicketPaymentAsync(ticketId, paidAmount, "cash");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при підтвердженні оплати квитка {TicketId}", ticketId);
                return Json(new { success = false, message = "Помилка при підтвердженні оплати" });
            }
        }

        // Страница финансовых отчетов
        [HttpGet]
        public async Task<IActionResult> FinancialReports()
        {
            if (!IsAccountantAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу до панелі бухгалтера.";
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // API для получения финансового отчета
        [HttpGet]
        public async Task<IActionResult> GetFinancialReport(DateTime? startDate, DateTime? endDate, string reportType = "summary")
        {
            if (!IsAccountantAuthorized())
            {
                return Json(new { error = "Unauthorized" });
            }

            try
            {
                // По умолчанию за текущий месяц
                if (!startDate.HasValue) startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                if (!endDate.HasValue) endDate = DateTime.Now.Date;

                // Используем AccountingService
                var report = await _accountingService.GetFinancialReportAsync(startDate.Value, endDate.Value, reportType);
                return Json(report);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при генерації фінансового звіту");
                return Json(new { error = "Помилка при генерації звіту" });
            }
        }

        // API для получения подробной статистики по дням
        [HttpGet]
        public async Task<IActionResult> GetDailyStatistics(DateTime startDate, DateTime endDate)
        {
            if (!IsAccountantAuthorized())
            {
                return Json(new { error = "Unauthorized" });
            }

            try
            {
                // Используем AccountingService
                var statistics = await _accountingService.GetDailyStatisticsAsync(startDate, endDate);
                return Json(statistics);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при отриманні денної статистики");
                return Json(new { error = "Помилка при отриманні статистики" });
            }
        }





        // API для экспорта отчета в CSV
        [HttpGet]
        public async Task<IActionResult> ExportReport(DateTime startDate, DateTime endDate, string format = "csv")
        {
            if (!IsAccountantAuthorized())
            {
                TempData["ErrorMessage"] = "У вас немає прав доступу.";
                return RedirectToAction("Index");
            }

            try
            {
                // Используем AccountingService
                var reportData = await _accountingService.GetDetailedFinancialReportAsync(startDate, endDate);

                if (format.ToLower() == "csv")
                {
                    var csv = GenerateCsvReport(reportData);
                    var fileName = $"financial_report_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}.csv";

                    return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
                }

                return BadRequest("Непідтримуваний формат");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Помилка при експорті звіту");
                TempData["ErrorMessage"] = "Помилка при експорті звіту";
                return RedirectToAction("FinancialReports");
            }
        }

        private string GenerateCsvReport(List<Dictionary<string, object>> reportData)
        {
            var csv = new System.Text.StringBuilder();

            // Заголовки
            csv.AppendLine("Дата,Номер квитка,Клієнт,Поїзд,Маршрут,Сума,Статус,Дата оплати");

            // Данные
            foreach (var row in reportData)
            {
                csv.AppendLine($"{row["travel_date"]},{row["ticket_id"]},{row["client_name"]},{row["train_number"]},{row["route"]},{row["total_price"]},{row["status"]},{row["payment_date"]}");
            }

            return csv.ToString();
        }

       
    }
}