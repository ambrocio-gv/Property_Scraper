using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace StepThree
{
    public class StageThreeContext:DbContext
    {
        IConfiguration? _configuration;
        public StageThreeContext(IConfiguration? configuration)
        {
            _configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (_configuration is not null)
            {
                var str = _configuration.GetConnectionString("DefaultConnection");
                optionsBuilder.UseSqlServer(str);
            }
        }

    }
}
