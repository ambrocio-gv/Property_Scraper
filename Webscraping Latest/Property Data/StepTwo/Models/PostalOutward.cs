using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace StepTwo.Models
{
    [Keyless]
    [Table("tblkp_PostO")]
    public class PostalOutward
    {
        public string? Posto { get; set; }

    }
}
