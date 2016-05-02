using MCHost.Framework;
using MCHost.Framework.MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MCHost.Web.Controllers.Api
{
    public class PackagesController : BaseApiController
    {
        private readonly IDatabase _database;

        public PackagesController(IDatabase database) :
            base(database)
        {
            _database = database;
        }
        
        public object Get()
        {
            if (!IsAuthenticated)
                return null;
            
            return new { packages = GetCachedObject(() => _database.GetPackages()) };
        }
    }
}