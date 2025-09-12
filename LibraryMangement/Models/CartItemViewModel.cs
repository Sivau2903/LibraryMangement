using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class CartItemViewModel
    {
        public int MaterialID { get; set; }
        public string Title { get; set; }
        public int AvailableQuantity { get; set; }
    }

}