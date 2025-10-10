using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class IssueReservedMaterialViewModel
    {
        public int CirculationID { get; set; }
        public int MaterialID { get; set; }
        public string MaterialTitle { get; set; }
        public int PatronID { get; set; }
        public string PatronName { get; set; }
        public string PatronEmail { get; set; }
        public string PatronType { get; set; }
        public DateTime? RequestedDate { get; set; }
        public string Status { get; set; }
        public Patron Patron { get; set; }
        public Material Material { get; set; }
    }
}