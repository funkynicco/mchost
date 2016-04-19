using MCHost.Framework.Minecraft;
using MCHost.Framework.MVC;
using MCHost.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MCHost.Web.Controllers
{
    public class HomeController : BaseController
    {
        public ActionResult Index()
        {
            return View();// (object)Global.HostClient.GetLog());
        }

        [ActionName("get-log")]
        public ActionResult GetLog(long id)
        {
            return Json(Global.HostClient.GetLog(id), JsonRequestBehavior.AllowGet);
        }

        [ActionName("start-instance")]
        public ActionResult StartInstance()
        {
            var configuration = InstanceConfiguration.Default;

            Global.HostClient.Send($"NEW Test:{configuration.Serialize()}|");

            return Json(new { result = true }, JsonRequestBehavior.AllowGet);
        }

        [ActionName("list-instances")]
        public ActionResult ListInstances()
        {
            Global.HostClient.Send("LST|");

            return Json(new { result = true }, JsonRequestBehavior.AllowGet);
        }
    }
}