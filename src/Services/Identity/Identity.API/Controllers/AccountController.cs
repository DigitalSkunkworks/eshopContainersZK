using IdentityModel;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopOnContainers.Services.Identity.API.Models;
using Microsoft.eShopOnContainers.Services.Identity.API.Models.AccountViewModels;
using Microsoft.eShopOnContainers.Services.Identity.API.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using zipkin4net;

namespace Microsoft.eShopOnContainers.Services.Identity.API.Controllers
{
    /// <summary>
    /// This sample controller implements a typical login/logout/provision workflow for local and external accounts.
    /// The login service encapsulates the interactions with the user data store. This data store is in-memory only and cannot be used for production!
    /// The interaction service provides a way for the UI to communicate with identityserver for validation and context retrieval
    /// </summary>
    public class AccountController : Controller
    {
        //private readonly InMemoryUserLoginService _loginService;
        private readonly ILoginService<ApplicationUser> _loginService;
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IClientStore _clientStore;
        private readonly ILogger<AccountController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private zipkin4net.Trace trace;
        public AccountController()
        {
            trace = zipkin4net.Trace.Create();
        }

        public AccountController(
            //InMemoryUserLoginService loginService,
            ILoginService<ApplicationUser> loginService,
            IIdentityServerInteractionService interaction,
            IClientStore clientStore,
            ILogger<AccountController> logger,
            UserManager<ApplicationUser> userManager)
        {
            trace = zipkin4net.Trace.Create();
            _loginService = loginService;
            _interaction = interaction;
            _clientStore = clientStore;
            _logger = logger;
            _userManager = userManager;
        }

        /// <summary>
        /// Show login page
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl)
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("AccountController:Login"));
            trace.Record(Annotations.Rpc("GET"));
            var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
            if (context?.IdP != null)
            {
                trace.Record(Annotations.ServerSend());
                // if IdP is passed, then bypass showing the login screen
                return ExternalLogin(context.IdP, returnUrl);
            }

            var vm = await BuildLoginViewModelAsync(returnUrl, context);

            ViewData["ReturnUrl"] = returnUrl;

            trace.Record(Annotations.ServerSend());
            return View(vm);
        }

        /// <summary>
        /// Handle postback from username/password login
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("AccountController:Login"));
            trace.Record(Annotations.Rpc("POST"));

            if (ModelState.IsValid)
            {
                var user = await _loginService.FindByUsername(model.Email);
                if (await _loginService.ValidateCredentials(user, model.Password))
                {
                    AuthenticationProperties props = null;
                    if (model.RememberMe)
                    {
                        props = new AuthenticationProperties
                        {
                            IsPersistent = true,
                            ExpiresUtc = DateTimeOffset.UtcNow.AddYears(10)
                        };
                    };

                    await _loginService.SignIn(user);
                   
                    // make sure the returnUrl is still valid, and if yes - redirect back to authorize endpoint
                    if (_interaction.IsValidReturnUrl(model.ReturnUrl))
                    {
                        trace.Record(Annotations.ServerSend());
                        return Redirect(model.ReturnUrl);
                    }

                    trace.Record(Annotations.ServerSend());
                    return Redirect("~/");
                }

                ModelState.AddModelError("", "Invalid username or password.");
            }

            // something went wrong, show form with error
            var vm = await BuildLoginViewModelAsync(model);

            ViewData["ReturnUrl"] = model.ReturnUrl;

            trace.Record(Annotations.ServerSend());
            return View(vm);
        }

        async Task<LoginViewModel> BuildLoginViewModelAsync(string returnUrl, AuthorizationRequest context)
        {
            trace.Record(Annotations.LocalOperationStart("AccountController:BuildLoginViewModelAsync"));

            var allowLocal = true;
            if (context?.ClientId != null)
            {
                var client = await _clientStore.FindEnabledClientByIdAsync(context.ClientId);
                if (client != null)
                {
                    allowLocal = client.EnableLocalLogin;
                }
            }

            var tmp = new LoginViewModel
            {
                ReturnUrl = returnUrl,
                Email = context?.LoginHint,
            };
            trace.Record(Annotations.LocalOperationStop());
            return tmp;
        }

        async Task<LoginViewModel> BuildLoginViewModelAsync(LoginViewModel model)
        {
            trace.Record(Annotations.LocalOperationStart("AccountController:BuildLoginViewModelAsync"));

            var context = await _interaction.GetAuthorizationContextAsync(model.ReturnUrl);
            var vm = await BuildLoginViewModelAsync(model.ReturnUrl, context);
            vm.Email = model.Email;
            vm.RememberMe = model.RememberMe;
            trace.Record(Annotations.LocalOperationStop());
            return vm;
        }

        /// <summary>
        /// Show logout page
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Logout(string logoutId)
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("AccountController:Logout"));
            trace.Record(Annotations.Rpc("GET"));

            if (User.Identity.IsAuthenticated == false)
            {
                trace.Record(Annotations.ServerSend());
                // if the user is not authenticated, then just show logged out page
                return await Logout(new LogoutViewModel { LogoutId = logoutId });
            }

            //Test for Xamarin. 
            var context = await _interaction.GetLogoutContextAsync(logoutId);
            if (context?.ShowSignoutPrompt == false)
            {
                trace.Record(Annotations.ServerSend());
                //it's safe to automatically sign-out
                return await Logout(new LogoutViewModel { LogoutId = logoutId });
            }

            // show the logout prompt. this prevents attacks where the user
            // is automatically signed out by another malicious web page.
            var vm = new LogoutViewModel
            {
                LogoutId = logoutId
            };
            trace.Record(Annotations.ServerSend());
            return View(vm);
        }

        /// <summary>
        /// Handle logout page postback
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(LogoutViewModel model)
        {
            trace.Record(Annotations.ServiceName("AccountController:Logout"));
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.Rpc("POST"));

            var idp = User?.FindFirst(JwtClaimTypes.IdentityProvider)?.Value;

            if (idp != null && idp != IdentityServerConstants.LocalIdentityProvider)
            {
                if (model.LogoutId == null)
                {
                    // if there's no current logout context, we need to create one
                    // this captures necessary info from the current logged in user
                    // before we signout and redirect away to the external IdP for signout
                    model.LogoutId = await _interaction.CreateLogoutContextAsync();
                }

                string url = "/Account/Logout?logoutId=" + model.LogoutId;

                try
                {
                    
                    // hack: try/catch to handle social providers that throw
                    await HttpContext.SignOutAsync(idp, new AuthenticationProperties
                    {
                        RedirectUri = url
                    });
                }
                catch (Exception ex)
                {
                    trace.Record(Annotations.ServerSend());
                    _logger.LogCritical(ex.Message);
                }
            }

            // delete authentication cookie
            await HttpContext.SignOutAsync();

            // set this so UI rendering sees an anonymous user
            HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // get context information (client name, post logout redirect URI and iframe for federated signout)
            var logout = await _interaction.GetLogoutContextAsync(model.LogoutId);

            trace.Record(Annotations.ServerSend());
            return Redirect(logout?.PostLogoutRedirectUri);
        }

        public async Task<IActionResult> DeviceLogOut(string redirectUrl)
        {
            trace.Record(Annotations.LocalOperationStart("AccountController:DeviceLogOut"));
            // delete authentication cookie
            await HttpContext.SignOutAsync();

            // set this so UI rendering sees an anonymous user
            HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            trace.Record(Annotations.LocalOperationStop());
            return Redirect(redirectUrl);
        }

        /// <summary>
        /// initiate roundtrip to external authentication provider
        /// </summary>
        [HttpGet]
        public IActionResult ExternalLogin(string provider, string returnUrl)
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("AccountController:ExternalLogin"));
            trace.Record(Annotations.Rpc("GET"));

            if (returnUrl != null)
            {
                returnUrl = UrlEncoder.Default.Encode(returnUrl);
            }
            returnUrl = "/account/externallogincallback?returnUrl=" + returnUrl;

            // start challenge and roundtrip the return URL
            var props = new AuthenticationProperties
            {
                RedirectUri = returnUrl,
                Items = { { "scheme", provider } }
            };
            trace.Record(Annotations.ServerSend());
            return new ChallengeResult(provider, props);
        }


        // GET: /Account/Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string returnUrl = null)
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("AccountController:Register"));
            trace.Record(Annotations.Rpc("GET"));

            ViewData["ReturnUrl"] = returnUrl;
            trace.Record(Annotations.ServerSend());
            return View();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnUrl = null)
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("AccountController:Register"));
            trace.Record(Annotations.Rpc("POST"));

            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    CardHolderName = model.User.CardHolderName,
                    CardNumber = model.User.CardNumber,
                    CardType = model.User.CardType,
                    City = model.User.City,
                    Country = model.User.Country,
                    Expiration = model.User.Expiration,
                    LastName = model.User.LastName,
                    Name = model.User.Name,
                    Street = model.User.Street,
                    State = model.User.State,
                    ZipCode = model.User.ZipCode,
                    PhoneNumber = model.User.PhoneNumber,
                    SecurityNumber = model.User.SecurityNumber
                };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Errors.Count() > 0)
                {
                    AddErrors(result);
                    trace.Record(Annotations.ServerSend());
                    // If we got this far, something failed, redisplay form
                    return View(model);
                }
            }

            if (returnUrl != null)
            {
                if (HttpContext.User.Identity.IsAuthenticated)
                {
                    trace.Record(Annotations.ServerSend());
                    return Redirect(returnUrl);
                }
                else
                    if (ModelState.IsValid)
                {
                    trace.Record(Annotations.ServerSend());
                    return RedirectToAction("login", "account", new { returnUrl = returnUrl });
                }
                else
                {
                    trace.Record(Annotations.ServerSend());
                    return View(model);
                }
            }

            trace.Record(Annotations.ServerSend());
            return RedirectToAction("index", "home");
        }

        [HttpGet]
        public IActionResult Redirecting()
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("AccountController:Redirecting"));
            trace.Record(Annotations.Rpc("GET"));
            trace.Record(Annotations.ServerSend());

            return View();
        }

        private void AddErrors(IdentityResult result)
        {
            trace.Record(Annotations.LocalOperationStart("AccountController:AddErrors"));
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            trace.Record(Annotations.LocalOperationStop());
        }
    }
}