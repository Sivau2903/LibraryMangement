using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class LibrarianDashboardViewModel
    {
        public int TotalMaterials { get; set; }
        public int TotalPatrons { get; set; }
        public int TotalLibrarians { get; set; }
        public int ActiveIssues { get; set; }
        public int OverdueIssues { get; set; }
        public int PendingReservations { get; set; }
        public int MaterialsBelowStockLimit { get; set; }
    }

}