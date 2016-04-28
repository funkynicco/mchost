using MCHost.Framework.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace MCHost.Framework.MVC
{
    public class BaseApiController : ApiController
    {
        private readonly IDatabase _database;
        private User _user;

        protected User CurrentUser { get { return _user; } }
        protected bool IsAuthenticated { get { return CurrentUser != null; } }

        public BaseApiController(IDatabase database)
        {
            _database = database;
        }

        public override async Task<HttpResponseMessage> ExecuteAsync(HttpControllerContext controllerContext, CancellationToken cancellationToken)
        {
            _user = RequestAccessAuthorizer.Authorize(_database);

            return await base.ExecuteAsync(controllerContext, cancellationToken);
        }
    }
}
