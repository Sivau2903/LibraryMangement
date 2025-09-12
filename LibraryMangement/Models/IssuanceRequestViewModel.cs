using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class IssuanceRequestViewModel
    {
        public int RequestID { get; set; }
        public string PatronName { get; set; }
        public DateTime RequestDate { get; set; }
        public string RequestStatus { get; set; }

        public List<IssuanceRequestItemViewModel> Items { get; set; }
    }
}