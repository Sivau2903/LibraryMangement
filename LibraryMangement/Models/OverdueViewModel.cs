using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class OverdueViewModel
    {
        public int CirculationID { get; set; }
        public string MaterialTitle { get; set; }
        public string PatronName { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public decimal FineAmount { get; set; }
        public string Status { get; set; }
    }

}