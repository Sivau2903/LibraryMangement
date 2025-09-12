using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class MaterialUploadModel
    {
        public string Title { get; set; }
        public string AuthorName { get; set; }
        public string Publisher { get; set; }
        public int YearPublished { get; set; }
        public string ISBN { get; set; }
        public int AvailableQuantity { get; set; }
        public int TotalQuantity { get; set; }
        public string CallNumber { get; set; }
        public int CopyCount { get; set; }
        public bool IsDuplicate { get; internal set; }
        public string Edition { get; internal set; }
        public string PlaceofPublishers { get; internal set; }
        public int Pages { get; internal set; }
        public string Vol { get; internal set; }
        public string Source { get; internal set; }
        public decimal? Price { get; internal set; }
    }

}