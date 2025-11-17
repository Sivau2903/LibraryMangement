using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Models
{
    public class CirculationViewModel
    {
        public string UserID { get; set; }
        public DateTime RequestedDate { get; set; }
        public DateTime IssuedDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Issued";
        public string Barcode { get; set; }
        public int MaterialID { get; set; }
        public int CopyID { get; set; }
        public string AuthorName { get; set; }
        public string Edition { get; set; }
        public bool HasMultipleSchools { get; set; }
        public List<SelectListItem> SchoolList { get; set; }
        public string Title { get;  set; }
       
    }
}