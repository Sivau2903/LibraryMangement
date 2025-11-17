using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Models
{
    public class NewBookRequestViewModel
    {
        public int RequestID { get; set; }
        public string UserID { get; set; }
        public string PatronName { get; set; }
        public string MaterialTitle { get; set; }
        public DateTime RequestedDate { get; set; }
        public string Status { get; set; }
        public bool HasMultipleSchools { get; set; }
        public List<SelectListItem> SchoolList { get; set; }
    }

}