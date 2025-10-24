using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class ActiveBookingViewModel
    {

              public int BookingID { get; set; }
        public string MaterialTitle { get; set; }
        public string Name { get; set; }
        public String UserID { get; set; }
        public DateTime? BookingDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Status { get; set; }
    }
}