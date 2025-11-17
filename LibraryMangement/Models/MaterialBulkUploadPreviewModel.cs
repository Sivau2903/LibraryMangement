using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;


namespace LibraryMangement.Models
{
    public class MaterialBulkUploadPreviewModel
    {

        public int RowIndex { get; set; }
        public string Title { get; set; }
        public string AuthorName { get; set; }
        public string Publisher { get; set; }
        public int? YearPublished { get; set; }
        public string ISBN { get; set; }
        public int AvailableQuantity { get; set; }
        public int TotalQuantity { get; set; }
        public string CallNumber { get; set; }
        public int CopyCount { get; set; }
        public bool IsDuplicate { get; set; } // Flag for duplicates
        public string Edition { get; set; }
        public string Discription { get; set; }
        public string PlaceofPublishers { get;  set; }
        public decimal? Price { get;  set; }
        public string Source { get;  set; }
        public string Vol { get;  set; }
        public int? Pages { get; set; }

        public string AccountNumber { get; internal set; }
        public bool IsDeleted { get; internal set; }
        public bool HasMultipleSchools { get; set; }
        public List<SelectListItem> SchoolList { get; set; }
    }
}