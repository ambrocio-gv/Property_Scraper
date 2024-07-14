using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace StageFive
{
    public class StageFiveContext:DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var str = AppSettingsJsonParser.GetConnectionString();
            optionsBuilder.UseSqlServer(str);
        }
    }
}
