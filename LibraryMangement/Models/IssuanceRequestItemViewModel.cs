using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class IssuanceRequestItemViewModel
    {
        public string MaterialTitle { get; set; }
        public int AvailableQuantity { get; set; }
        public string AccountNumber { get; set; }
        public string BarcodeNumber { get; set; }
        public string Status { get; set; }
    }
}