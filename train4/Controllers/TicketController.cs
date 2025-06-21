using Microsoft.AspNetCore.Mvc;

namespace train2.Controllers
{
    public class TicketController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
