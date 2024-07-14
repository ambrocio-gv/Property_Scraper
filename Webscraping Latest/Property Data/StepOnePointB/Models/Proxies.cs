using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StageOne.Models
{
    [Table("tblkp_ProxyIP")]
    public class Proxies
    {
        [Key]
        public int? Id { get; set; }
        public string? ProxyIP { get; set; }
        public string? Port { get; set; }
        public DateTime? DateAdded { get; set; }
        public DateTime? RecentSuccess { get; set; }
        public DateTime? RecentUsed { get; set; }
        public string? Status { get; set; }

    }
}
