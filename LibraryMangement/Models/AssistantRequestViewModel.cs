using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Models
{
    public class AssistantRequestViewModel
    {
        [Required]
        public int LibraryID { get; set; }
        public IEnumerable<SelectListItem> LibraryList { get; set; }

        [Required]
        public string AssistantUserID { get; set; }
        public IEnumerable<SelectListItem> AssistantList { get; set; }

        public string LibrarianUserID { get; set; }

        public string AssistantName { get; set; }
        public string LibrarianName { get; set; }
        public string LibraryName { get; set; }
        public int RequestID { get; set; }
        public DateTime RequestDate { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Pending";
        public string Remarks { get; set; }
        public List<AssistantRequestViewModel> ExistingRequests { get; set; }

    }
}