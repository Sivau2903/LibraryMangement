using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class FineReasonDTO
    {
        public string ReasonText { get; set; }
        public decimal FinePerDay { get; set; }
        public string Value { get; set; } // same as ReasonText
    }
}