using MCHost.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

public class UserIdentity : IIdentity
{
    public User User { get; set; }
    public string SessionKey { get; set; }

    public UserIdentity(User user, string sessionKey)
    {
        User = user;
        SessionKey = sessionKey;
    }

    public string AuthenticationType
    {
        get { return "Basic"; }
    }

    public bool IsAuthenticated
    {
        get
        {
            return
                User != null
                //&& User.Role >= User.Registered;
                ;
        }
    }

    public string Name
    {
        get
        {
            return User != null ? User.DisplayName : "Anonymous";
        }
    }
    /*
    /// <summary>
    /// Gets a datetime representing the time for the users set time zone.
    /// </summary>
    /// <param name="date">Date to convert</param>
    /// <param name="kind">Type (UTC or Local)</param>
    /// <returns></returns>
    public DateTime GetLocalDateTime(DateTime date, DateTimeKind kind = DateTimeKind.Utc)
    {
        return User != null ? User.GetLocalDateTime(date, kind) : date;
    }
    */
}
