using MCHost.Framework.Security;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework
{
    public interface IDatabase
    {
        void TestConnection();

        #region Account
        User Login(string email, string password_hash);
        User GetUser(int userid);
        User GetUser(string email);
        IEnumerable<User> GetUsers();
        #endregion

        #region Cookies
        void LoadUserCookieCache(ref Dictionary<string, UserCookieCacheItem> cookies);
        void UpdateUserCookieExpireDate(string key, DateTime expireDate);
        void AddUserCookie(string key, string ip, int userid, DateTime expireDate);
        void DeleteUserCookie(string key);
        void DeleteUserCookies(int userid);
        User ResumeSession(string key);
        #endregion

        void AddLog(string text);
        void AddUserLog(int userId, string text);
        Package GetPackage(string name);
        IEnumerable<Package> GetPackages();
    }

    public class Database : DatabaseManager, IDatabase
    {
        private readonly ServiceType _service;

        public Database(ISettings settings) :
            base(settings.ConnectionString)
        {
            _service = settings.ServiceType;
        }

        #region Account
        public User Login(string email, string password_hash)
        {
            using (var query = Query("Login")
                .CommandType(CommandType.StoredProcedure)
                .AddParameter("email", SqlDbType.VarChar, email)
                .AddParameter("passwordHash", SqlDbType.VarChar, password_hash)
                .Execute())
            {
                if (query.Read())
                    return User.FromResult(query);
            }

            return null;
        }

        public User GetUser(int userid)
        {
            using (var query = Query("GetUserById")
                .CommandType(CommandType.StoredProcedure)
                .AddParameter("id", SqlDbType.Int, userid)
                .Execute())
            {
                if (query.Read())
                    return User.FromResult(query);
            }

            return null;
        }

        public User GetUser(string email)
        {
            using (var query = Query("GetUserByEmail")
                .CommandType(CommandType.StoredProcedure)
                .AddParameter("email", SqlDbType.VarChar, email)
                .Execute())
            {
                if (query.Read())
                    return User.FromResult(query);
            }

            return null;
        }

        public IEnumerable<User> GetUsers()
        {
            var list = new List<User>();

            using (var query = Query("GetUsers")
                .CommandType(CommandType.StoredProcedure)
                .Execute())
            {
                while (query.Read())
                    list.Add(User.FromResult(query));
            }

            return list;
        }
        #endregion

        #region Cookies
        public void LoadUserCookieCache(ref Dictionary<string, UserCookieCacheItem> cookies)
        {
            using (var query = Query("LoadUserCookieCache")
                .CommandType(CommandType.StoredProcedure)
                .Execute())
            {
                while (query.Read())
                {
                    var item = UserCookieCacheItem.FromResult(query);
                    cookies.Add(item.Key, item);
                }
            }
        }

        public void UpdateUserCookieExpireDate(string key, DateTime expireDate)
        {
            using (var query = Query("UpdateUserCookieExpireDate")
                .CommandType(CommandType.StoredProcedure)
                .AddParameter("expireDate", SqlDbType.DateTime, expireDate)
                .AddParameter("key", SqlDbType.VarChar, key))
            {
                query.ExecuteNonQuery();
            }
        }

        public void AddUserCookie(string key, string ip, int userid, DateTime expireDate)
        {
            using (var query = Query("AddUserCookie")
                .CommandType(CommandType.StoredProcedure)
                .AddParameter("key", SqlDbType.VarChar, key)
                .AddParameter("ip", SqlDbType.VarChar, ip)
                .AddParameter("userid", SqlDbType.Int, userid)
                .AddParameter("expireDate", SqlDbType.DateTime, expireDate))
            {
                query.ExecuteNonQuery();
            }
        }

        public void DeleteUserCookie(string key)
        {
            using (var query = Query("DeleteUserCookie")
                .CommandType(CommandType.StoredProcedure)
                .AddParameter("key", SqlDbType.VarChar, key))
            {
                query.ExecuteNonQuery();
            }
        }

        public void DeleteUserCookies(int userid)
        {
            using (var query = Query("DeleteUserCookies")
                .CommandType(CommandType.StoredProcedure)
                .AddParameter("userid", SqlDbType.Int, userid))
            {
                query.ExecuteNonQuery();
            }
        }

        public User ResumeSession(string key)
        {
            using (var query = Query("ResumeSession")
                .CommandType(CommandType.StoredProcedure)
                .AddParameter("key", SqlDbType.VarChar, key)
                .Execute())
            {
                if (query.Read())
                    return User.FromResult(query);
            }

            return null;
        }
        #endregion

        public void AddLog(string text)
        {
            using (var query = Query("[dbo].[AddLog]")
                .CommandType(CommandType.StoredProcedure)
                .AddParameter("service", SqlDbType.Int, (int)_service)
                .AddParameter("text", SqlDbType.Text, text))
            {
                query.ExecuteNonQuery();
            }
        }

        public void AddUserLog(int userId, string message)
        {
            using (var query = Query("AddUserLog")
                .CommandType(CommandType.StoredProcedure)
                .AddParameter("userId", SqlDbType.Int, userId)
                .AddParameter("message", SqlDbType.Text, message))
            {
                query.ExecuteNonQuery();
            }
        }

        public Package GetPackage(string name)
        {
            using (var query = Query("[dbo].[GetPackage]")
                .CommandType(CommandType.StoredProcedure)
                .AddParameter("name", SqlDbType.VarChar, name)
                .Execute())
            {
                if (query.Read())
                    return Package.FromResult(query);
            }

            return null;
        }

        public IEnumerable<Package> GetPackages()
        {
            var list = new List<Package>();

            using (var query = Query("[dbo].[GetPackages]")
                .CommandType(CommandType.StoredProcedure)
                .Execute())
            {
                while (query.Read())
                    list.Add(Package.FromResult(query));
            }

            return list;
        }
    }

    public enum ServiceType : int
    {
        /// <summary>
        /// This is the minecraft instance hosting service.
        /// </summary>
        InstanceService = 1,
        /// <summary>
        /// This is the website remote interface for the hosting service.
        /// </summary>
        Web = 2
    }
}
