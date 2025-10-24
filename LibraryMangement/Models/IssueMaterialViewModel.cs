using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

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
    }

}