using Microsoft.AspNetCore.Mvc;
using zipkin4net;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Controllers
{
    public class HomeController : Controller
    {
        private zipkin4net.Trace trace;

        // methods
        public HomeController()
        {
            trace = zipkin4net.Trace.Create();
        }

        // GET: /<controller>/
        public IActionResult Index()
        {
            trace.Record(Annotations.ServiceName("HomeController:Index"));
            trace.Record(Annotations.ServerRecv());
            return new RedirectResult("~/swagger");
        }
    }
}
