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

        public ActionResult Index()
        {
            return View();
        }

        [ActionName("get-log")]
        public ActionResult GetLog(long id)
        {
            return Json(_hostClient.GetLog(id), JsonRequestBehavior.AllowGet);
        }

        public ActionResult Test()
        {
            return View();
        }

        [ActionName("start-instance")]
        public ActionResult StartInstance()
        {
            var configuration = InstanceConfiguration.Default;

            try
            {
                return Json(new
                {
                    result = true,
                    instanceId = _hostClient.CreateInstance("Test", configuration)
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [ActionName("list-instances")]
        public ActionResult ListInstances()
        {
            try
            {
                return Json(new
                {
                    result = true,
                    instances = _hostClient.GetInstances()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /////////////////////////////////////////

        public ActionResult Login()
        {
            if (IsAuthenticated)
                return Redirect("/");

            return View("LoginLayout");
        }
    }
}