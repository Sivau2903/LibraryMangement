using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Models
{
    public class RequestReportViewModel
    {
        public int CirculationID { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string MaterialTitle { get; set; }
        public DateTime? RequestedDate { get; set; }
        public string Status { get; set; }
        public bool HasMultipleSchools { get; set; }
        public List<SelectListItem> SchoolList { get; set; }
    }
}