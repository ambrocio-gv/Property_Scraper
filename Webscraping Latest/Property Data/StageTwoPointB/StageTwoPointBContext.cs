using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StageTwoPointB
{
    public class StageTwoPointBContext:DbContext
    {



        public DbSet<Models.Proxy>? Proxies { get; set; }



        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var str = AppSettingsJsonParser.GetConnectionString();
            optionsBuilder.UseSqlServer(str);
        }
    }
}
