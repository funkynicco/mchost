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
        private readonly HostClient _client = Global.HostClient;

        public ActionResult Index()
        {
            return View();
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

            try
            {
                return Json(new
                {
                    result = true,
                    instanceId = _client.CreateInstance("Test", configuration)
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
                    instances = _client.GetInstances()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}