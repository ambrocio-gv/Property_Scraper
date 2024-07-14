using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StepTwo.Models
{
    [Table("tblkp_Thoroughfare")]
    public class ThoroughfareModel
    {
        [Key]
        public int ID { get; set; }
        public string? PostO { get; set; }
        public string? PostCode { get; set; }
        public string? Town { get; set; }
        public string? Thoroughfare { get; set; }
    }
}