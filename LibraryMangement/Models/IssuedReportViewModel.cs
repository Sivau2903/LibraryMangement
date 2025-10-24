using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class IssuedReportViewModel
    {
        public int CirculationID { get; set; }
        public string MaterialTitle { get; set; }
        public string UserID { get; set; }
        public string Name { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Status { get; set; }
    }

}