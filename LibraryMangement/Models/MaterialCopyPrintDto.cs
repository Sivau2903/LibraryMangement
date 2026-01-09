using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class MaterialCopyPrintDto
    {
        public int MaterialCopyID { get; set; }
        public string AccountNumber { get; set; }      // numeric for range comparisons
        public string BarcodeNumber { get; set; }
    }
}