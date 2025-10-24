using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class LibraryCounterViewModel
    {
        public int CounterID { get; set; }

        [Required]
        [Display(Name = "Counter Number")]
        public string CounterNumber { get; set; }

        [Required]
        [Display(Name = "Counter Name")]
        public string CounterName { get; set; }

        [Display(Name = "Assigned Employee")]
        public int? EmployeeID { get; set; }


        public string EmployeeName { get; set; } // for autocomplet
        public List<LibraryCounterViewModel> Counters { get; set; } // for left table
        public string AssignedBy { get;  set; }
    }
}