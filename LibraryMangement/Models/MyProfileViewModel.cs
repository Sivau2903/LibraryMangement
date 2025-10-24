using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class MyProfileViewModel
    {
        public string UserID { get; set; }

        //[Required(ErrorMessage = "Email / Username is required.")]
        //[EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Username { get; set; }
        public string Role { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [RegularExpression("^[a-zA-Z ]+$", ErrorMessage = "Name should contain letters only.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression("^[0-9]{10}$", ErrorMessage = "Phone number must be number with 10 digits.")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Email / Username is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; }


        public string DepartmentName { get; set; } // Librarian only
        public string UniversityName { get; set; } // Both

        public bool IsLibrarian { get; set; }
        public object SchoolName { get; internal set; }


        //public tblUserRole role { get; set; }
    }
}