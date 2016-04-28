using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace MCHost.Framework.Security
{
    /// <summary>
    /// Restricts access to the Web API 2 Controller and its methods.
    /// <para>For MVC Controllers, use [Restrict].</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RestrictApiAttribute : AuthorizeAttribute
    {
    }
}
