using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class MaterialViewModel
    {
        public int MaterialID { get; set; }  // Used for Edit
        public string Title { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public int YearPublished { get; set; }
        public string MaterialType { get; set; }
        public string ISBN { get; set; }
        public int AvailableQuantity { get; set; }
        public int TotalQuantity { get; set; }
        public string Edition { get; set; }
        public string Description { get; set; }
        public int Pages { get; set; }
        public string Vol { get; set; }
        public string Source { get; set; }
        public string PlaceofPublishers { get; set; }
        public decimal? Price { get; set; }
        public string CallNumber { get; set; } // Optional
        public object tblSchoolName { get; internal set; }
        public string DepID { get; internal set; }
        public List<MaterialType> MaterialTypes { get; set; }  // For dropdown
        public int? SchoolID { get; internal set; }
    }

}