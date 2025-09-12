using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

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
    }

}