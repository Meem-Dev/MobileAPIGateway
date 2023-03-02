using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MobileAPIGateway.Models;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Reflection.PortableExecutable;

namespace MobileAPIGateway.Controllers
{
    public class HomeController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HomeController> _logger;
        private readonly IHttpContextAccessor _accessor;
        //private readonly ICo

        public HomeController(ILogger<HomeController> logger , HttpClient httpClient , IHttpContextAccessor accessor)
        {
            _logger = logger;
            _httpClient = httpClient;
            _accessor = accessor;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> token()
        {
            var accessToken = _accessor.HttpContext.GetTokenAsync("access_token").Result.ToString();
           _accessor.HttpContext.Response.Headers.Add("Access-token", accessToken);
            return View("Index");
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