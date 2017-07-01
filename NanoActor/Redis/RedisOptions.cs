using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor.Redis
{
    public class RedisOptions
    {
        public String ConnectionString { get; set; } = "localhost";

        public Int32 Database = -1;

    }
}
