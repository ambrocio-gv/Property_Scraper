using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StageOne.Models;

namespace StageOne
{
    public class ProxyContext:DbContext
    {
        public ProxyContext()
        {

        }


        public DbSet<Proxies>? ProxiesList { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("Data Source=DHPHSQL4;Initial Catalog=SD0092_ForSaleProperty_Scraping;User ID=NPAppUser;Password=NP@ppU$3r;TrustServerCertificate=True;");
        }

    }
}
