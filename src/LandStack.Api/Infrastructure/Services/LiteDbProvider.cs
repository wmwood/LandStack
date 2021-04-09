using System;
using System.IO;
using LiteDB;

namespace LandStack.Api.Infrastructure.Services
{
    public class LiteDbProvider : IDisposable
    {
        private readonly LiteDatabase _db;

        public LiteDbProvider()
        {
            string dataDir = AppDomain.CurrentDomain.GetData("DataDirectory").ToString();

            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);

            _db = new LiteDatabase(Path.Combine(dataDir, "database.db"));
        }

        public LiteDatabase Database => _db;

        public void Dispose()
        {
            _db.Dispose();
        }
    }
}