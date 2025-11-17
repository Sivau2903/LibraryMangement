using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class LowStockMaterialViewModel
    {
        public int MaterialID { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Edition { get; set; }
        public int AvailableQty { get; set; }
        public int? ReorderLevel { get; set; }
    }
}