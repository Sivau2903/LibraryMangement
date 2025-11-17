using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Models
{
    public class OverdueReportViewModel
    {
        public int CirculationID { get; set; }
        public string MaterialTitle { get; set; }
        public string StudentName { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public decimal FineAmount { get; set; }
        public string Status { get; set; }
        public bool HasMultipleSchools { get; set; }
        public List<SelectListItem> SchoolList { get; set; }
        public string UserID { get; internal set; }
        public string UserName { get; internal set; }
        public DateTime? RequestedDate { get; internal set; }
    }
}