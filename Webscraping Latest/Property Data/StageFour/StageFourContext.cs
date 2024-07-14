using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace StageFour
{
    public class StageFourContext:DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var str = AppSettingsJsonParser.GetConnectionString();
            optionsBuilder.UseSqlServer(str);
        }
    }
}
