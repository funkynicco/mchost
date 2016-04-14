using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework
{
    public interface IDatabase
    {
        IEnumerable<Package> GetPackages();
    }

    public class Database : DatabaseManager, IDatabase
    {
        private Database(string connectionString) :
            base(connectionString)
        {
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

        public static IDatabase Create(string connectionString)
        {
            return new Database(connectionString);
        }
    }
}
