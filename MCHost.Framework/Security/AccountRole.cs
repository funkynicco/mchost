using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Security
{
    public enum AccountRole : int
    {
        /// <summary>
        /// Represents a user that is not logged in.
        /// </summary>
        User = 0,
        /// <summary>
        /// Represents a user that is logged in with the least amount of authority.
        /// </summary>
        Registered = 1,
        /// <summary>
        /// Represents an operator.
        /// </summary>
        Operator = 10,
        /// <summary>
        /// Represents a supervisor that have privileges of controlling operators and below.
        /// </summary>
        Supervisor = 100,
        /// <summary>
        /// Represents administrative privileges who also have access to internal debugging.
        /// </summary>
        Administrator = 1000
    }
}
