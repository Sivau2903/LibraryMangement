using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class LibraryAdminViewModel
    {
        public int LibraryID { get; set; }
        public string LibraryName { get; set; }
        public string LibrarianUserID { get; set; }
        public string LibrarianName { get; set; }

        public string AssistantUserID { get; set; }
        public string AssistantName { get; set; }

        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }

        public string UniversityID { get; set; }
    }
}