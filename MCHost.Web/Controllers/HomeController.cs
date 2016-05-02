using MCHost.Framework;
using MCHost.Framework.Minecraft;
using MCHost.Framework.MVC;
using MCHost.Framework.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace MCHost.Web.Controllers
{
    public class HomeController : BaseController
    {
        private readonly IDatabase _database;
        private readonly IHostClient _hostClient;

        public HomeController(IDatabase database, IHostClient hostClient) :
            base(database)
        {
            _database = database;
            _hostClient = hostClient;
        }

        [Restrict(AccountRole.Registered)]
        public ActionResult Index()
        {
            return View();
        }
        
        [Route("login")]
        public ActionResult Login()
        {
            if (IsAuthenticated)
                return Redirect("/");

            return View("LoginLayout");
        }
    }
}