using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StageFive.Models
{
    internal class PropertyDetail
    {

        public string? UPRN { get; set; }
        public string? Address { get; set; }
        public string? HouseType { get; set; }
        public string? Tenure { get; set; }
        public string? NumberOfBedroom { get; set; }
        public string? NumberOfBathroom { get; set; }
        public string? NumberOfReceptionArea { get; set; }
        public string? EstPriceRangeLow { get; set; }
        public string? EstPriceRangeHigh { get; set; }
        
        public string? SqMetres { get; set; }   

        public List<(string SoldDate, string SoldPrice )>? SoldData { get; set; }


        public string? WebUrl { get; set; }
    }
}
