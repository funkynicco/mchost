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
        void AddLog(string text);
        void AddLog(string text, bool logConsole);
        IEnumerable<Package> GetPackages();
    }
    
    public class Database : DatabaseManager, IDatabase
    {
        private readonly ServiceType _service;

        private Database(string connectionString, ServiceType service) :
            base(connectionString)
        {
            _service = service;
        }

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

        public void AddLog(string text, bool logConsole)
        {
            AddLog(text);
            Console.WriteLine(text);
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

        // static

        public static IDatabase Create(string connectionString, ServiceType service)
        {
            return new Database(connectionString, service);
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
