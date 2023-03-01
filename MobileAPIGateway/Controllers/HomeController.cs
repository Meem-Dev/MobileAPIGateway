using Microsoft.AspNetCore.Mvc;
using MobileAPIGateway.Models;
using System.Diagnostics;

namespace MobileAPIGateway.Controllers
{
    public class HomeController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger , HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public  IActionResult Index()
        {
            var Url = "http://apigate-test-hrsd.meemdev.com/booking/api/v1/Counselor/Comments?id=d684ed69-9c2b-4b54-e09d-08d9d99f5582&PageSize=10&PageIndex=0";
            var responseString = _httpClient.GetStringAsync(Url).Result;
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}