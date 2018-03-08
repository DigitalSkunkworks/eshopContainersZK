using Microsoft.AspNetCore.Mvc;
using zipkin4net;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Microsoft.eShopOnContainers.Services.Locations.API.Controllers
{
    public class HomeController : Controller
    {
        private zipkin4net.Trace trace;
        public HomeController()
        {
            trace = zipkin4net.Trace.Create();
        }

        // GET: /<controller>/
        public IActionResult Index()
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("HomeController:Index"));
            trace.Record(Annotations.Rpc("GET"));

            var tmp = new RedirectResult("~/swagger");
            trace.Record(Annotations.ServerSend());
            return tmp;
        }
    }
}
