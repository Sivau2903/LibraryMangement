using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please enter the CAPTCHA")]
        [Display(Name = "Captcha")]
        public string CaptchaCode { get; set; }

        // Optional: To store generated CAPTCHA image as Base64 or URL
        public string CaptchaImage { get; set; }
    }
}