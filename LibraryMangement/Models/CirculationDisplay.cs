using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class CirculationDisplay
    {
        public int CirculationID { get; set; }
        public string Title { get; set; }
        public DateTime? RequestedDate { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Status { get; set; }
        public decimal FineAmount { get; internal set; }
    }
}