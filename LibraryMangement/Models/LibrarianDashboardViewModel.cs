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
        public List<MaterialTypeCount> MaterialsByType { get; set; }
        public int PendingBookinglist { get; internal set; }
        public int? SelectedDays { get; set; } // ✅ Removed default value, made nullable
        public int UpcomingOverdueIssues { get; set; }   // ✅ New property
        public string UserID { get; internal set; }
        public string Name { get; internal set; }
        public string UniversityName { get; internal set; }
        public string SchoolName { get; internal set; }
    }

}