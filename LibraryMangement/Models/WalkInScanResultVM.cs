using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class WalkInScanResultVM
    {
        public bool Found { get; set; }
        public string Message { get; set; }

        public int MaterialID { get; set; }
        public int CopyID { get; set; }
        public string Title { get; set; }
        public string BarcodeNumber { get; set; }

        public int AvailableQuantity { get; set; }
        public int RequestedCount { get; set; }      // Circulations with Status == "Requested" for this Material
        public int InCirculationCount { get; set; }  // Issued + Overdue for this Material

        public bool CanIssue { get; set; }           // AvailableQuantity > RequestedCount and AvailableQuantity > 0
        public int TotalQuantity { get; internal set; }
    }
}