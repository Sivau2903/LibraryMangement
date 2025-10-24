using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class FineReportViewModel
    {
        public int FineID { get; set; }
        public string Name { get; set; }
        public string MaterialTitle { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; }
        public DateTime? AppliedDate { get; set; }
        public string Status { get; set; }
        public int? SchoolID { get; internal set; }
        public string UniversityID { get; internal set; }
    }


}