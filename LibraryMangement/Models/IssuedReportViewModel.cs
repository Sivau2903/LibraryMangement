using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

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
        public string UserName { get; internal set; }
        public bool HasMultipleSchools { get; set; }
        public List<SelectListItem> SchoolList { get; set; }
    }

}