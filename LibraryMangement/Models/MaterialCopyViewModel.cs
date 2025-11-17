using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace LibraryMangement.Models
{
    public class MaterialCopyViewModel
    {
        public int CopyID { get; set; }  // For Edit
        public int MaterialID { get; set; }
        public string AccountNumber { get; set; }
        public string BarcodeNumber { get; set; }
        public string CallNumber { get; set; }
        public string Status { get; set; }  // Available / Issued
        public bool HasMultipleSchools { get; set; }
        public List<SelectListItem> SchoolList { get; set; }
    }

}