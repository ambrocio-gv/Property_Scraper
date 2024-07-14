using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StepTwo.Models;

namespace StepTwo
{
    public class StepTwoContext : DbContext
    {
        public DbSet<PostalOutward>? PostalOutwards { get; set; }
        public DbSet<Models.Proxy>? Proxies { get; set; }

        public DbSet<Models.ThoroughfareModel>? Thoroughfares { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = AppSettingsJsonParser.GetConnectionString();

            //Console.WriteLine(connectionString);

            optionsBuilder.UseSqlServer(connectionString);
        }

    }

}
