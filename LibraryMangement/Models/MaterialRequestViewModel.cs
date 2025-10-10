using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class MaterialRequestViewModel
    {
        [Required]
        public string MaterialTitle { get; set; }

        public string Edition { get; set; }

        public string Author { get; set; }

        public string Notes { get; set; }
    }
}