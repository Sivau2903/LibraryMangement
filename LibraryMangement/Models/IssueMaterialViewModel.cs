using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Models
{
    public class IssueMaterialViewModel
    {
        public int CirculationID { get; set; }
        public int MaterialID { get; set; }
        public string MaterialTitle { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string PatronType { get; set; } // Student or Faculty
        public DateTime? RequestedDate { get; set; }
        public string Status { get; set; }
        public string ID { get; internal set; }
        public bool HasMultipleSchools { get; set; }
        public List<SelectListItem> SchoolList { get; set; }
    }

}