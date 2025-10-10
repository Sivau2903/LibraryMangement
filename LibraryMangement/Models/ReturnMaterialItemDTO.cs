using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class ReturnMaterialItemDTO
    {
        public int CirculationID { get; set; }
        public string BarcodeNumber { get; set; }
        public string MaterialTitle { get; set; }
        public int PatronID { get; set; }
        public string PatronName { get; set; }
        public string PatronEmail { get; set; }
        public string PatronType { get; set; }
        public DateTime RequestedDate { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Status { get; set; }
        public decimal FineAmount { get; set; }
    }
}