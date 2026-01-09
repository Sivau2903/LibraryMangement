using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class WalkInIssueVM
    {
        public string UserID { get; set; }
        public int MaterialID { get; set; }
        public int CopyID { get; set; }
        public string BarcodeNumber { get; set; }

        public DateTime IssuedDate { get; set; }
        public DateTime DueDate { get; set; }

        public string UniversityID { get; set; }
        public int SchoolID { get; set; }
        public string IssuedBy { get; set; }   // current logged-in UserID or EmployeeName
        public string UserType { get;  set; }
        public string UserIdentifier { get;  set; }
    }
}