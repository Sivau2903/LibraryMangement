using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class PatronDashboardViewModel
    {
        public int PatronID { get; set; }
        public string PatronName { get; set; }
        public int ActiveIssuedCount { get; set; }
        public int OverdueCount { get; set; }
        public int PendingReservations { get; set; }
        public IEnumerable<Circulation> ActiveIssues { get; set; }
        public IEnumerable<Reservation> PendingReservationList { get; set; }
    }

}