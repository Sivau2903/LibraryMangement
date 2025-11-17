using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Models
{
    public class AvailabilityReportViewModel
    {
        public int MaterialID { get; set; }
        public string Title { get; set; }
        public string AuthorName { get; set; }
        public string MaterialType { get; set; }
        public int AvailableQuantity { get; set; }
        public int IssuedQuantity { get; set; }
        public int BookLostQuantity { get; set; }
        public int? TotalQuantity { get; internal set; }
        public bool HasMultipleSchools { get; set; }
        public List<SelectListItem> SchoolList { get; set; }
    }

}