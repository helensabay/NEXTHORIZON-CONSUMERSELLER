using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace NextHorizon.Models
{
    public class Logistics
    {
        [Key]
        public int logistics_id { get; set; }
        
        public string courier_name { get; set; } = string.Empty;
    }
}