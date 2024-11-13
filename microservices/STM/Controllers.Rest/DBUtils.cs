using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Controllers.Controllers
{
    public class DBUtils
    {
        public static IDatabase Db { get; private set; }
        public static bool IsLeader { get; set; }

        static DBUtils()
            {
                var redis = ConnectionMultiplexer.Connect("redis:6379");
                Db = redis.GetDatabase();
                IsLeader = false;
            }
    }

}
