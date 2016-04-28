using MCHost.Framework;
using MCHost.Framework.MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;

namespace MCHost.Web.Controllers.Api
{
    public class TestController : BaseApiController
    {
        public TestController(IDatabase database) :
            base(database)
        {
        }

        public object Get()
        {
            return CurrentUser;
        }
    }
}