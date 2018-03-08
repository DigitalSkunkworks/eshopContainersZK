
using IdentityServer4.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopOnContainers.Services.Identity.API.Models;
using Microsoft.eShopOnContainers.Services.Identity.API.Services;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using zipkin4net;

namespace Microsoft.eShopOnContainers.Services.Identity.API.Controllers
{
    public class HomeController : Controller
    {
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IOptionsSnapshot<AppSettings> _settings;
        private readonly IRedirectService _redirectSvc;
        private zipkin4net.Trace trace;
        public HomeController()
        {
            trace = zipkin4net.Trace.Create();
        }

        public HomeController(IIdentityServerInteractionService interaction, IOptionsSnapshot<AppSettings> settings,IRedirectService redirectSvc)
        {
            trace = zipkin4net.Trace.Create();
            _interaction = interaction;
            _settings = settings;
            _redirectSvc = redirectSvc;
        }

        public IActionResult Index(string returnUrl)
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("HomeController:Index"));
            trace.Record(Annotations.Rpc("GET"));
            trace.Record(Annotations.ServerSend());

            return View();
        }

        public IActionResult ReturnToOriginalApplication(string returnUrl)
        {
            trace.Record(Annotations.LocalOperationStart("HomeController:ReturnToOriginalApplication"));
            if (returnUrl != null)
            {
                trace.Record(Annotations.LocalOperationStop());
                return Redirect(_redirectSvc.ExtractRedirectUriFromReturnUrl(returnUrl));
            }
            else
            {
                trace.Record(Annotations.LocalOperationStop());
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// Shows the error page
        /// </summary>
        public async Task<IActionResult> Error(string errorId)
        {
            trace.Record(Annotations.LocalOperationStart("HomeController:Error"));
            var vm = new ErrorViewModel();

            // retrieve error details from identityserver
            var message = await _interaction.GetErrorContextAsync(errorId);
            if (message != null)
            {
                vm.Error = message;
            }

            trace.Record(Annotations.LocalOperationStop());
            return View("Error", vm);
        }
    }
}