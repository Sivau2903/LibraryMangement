using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace LibraryMangement.Models
{
    public class LibrarianPageViewModel
    {
        // ✅ School / Library context
        public bool HasMultipleSchools { get; set; }
        public List<SelectListItem> SchoolList { get; set; }
        public string SelectedSchoolName { get; set; }
        public int? SelectedSchoolID { get; set; }

        // ✅ Materials (your existing MaterialViewModel list)
        public List<MaterialViewModel> Materials { get; set; }

        // ✅ Optional: Dashboard or stats
        public int TotalMaterials { get; set; }
        public int TotalPatrons { get; set; }
        public int ActiveIssues { get; set; }
        public int OverdueIssues { get; set; }
        public int PendingReservations { get; set; }
        public int MaterialsBelowStockLimit { get; set; }
    }
}