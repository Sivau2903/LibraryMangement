using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LibraryMangement.Models
{
    public class MaterialViewModel
    {
        public int MaterialID { get; set; }  // Used for Edit

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Author is required")]
        [StringLength(150, ErrorMessage = "Author name cannot exceed 150 characters")]
        public string Author { get; set; }

        [Required(ErrorMessage = "Publisher is required")]
        [StringLength(150, ErrorMessage = "Publisher name cannot exceed 150 characters")]
        public string Publisher { get; set; }

        [Required(ErrorMessage = "Year Published is required")]
        [Range(1940, 2100, ErrorMessage = "Year Published must be between 1940 and 2100")]
        public int YearPublished { get; set; }

        [Required(ErrorMessage = "Material Type is required")]
        public string MaterialType { get; set; }

        [Required(ErrorMessage = "ISBN is required")]
        [StringLength(20, ErrorMessage = "ISBN cannot exceed 20 characters")]
        [RegularExpression(@"^(97(8|9))?\d{9}(\d|X)$", ErrorMessage = "Enter a valid ISBN (ISBN-10 or ISBN-13 format)")]
        public string ISBN { get; set; }


        [Required(ErrorMessage = "Available Quantity is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Available Quantity must be at least 1")]
        public int AvailableQuantity { get; set; }

        [Required(ErrorMessage = "Total Quantity is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Total Quantity must be at least 1")]
        public int TotalQuantity { get; set; }

        [StringLength(50, ErrorMessage = "Edition cannot exceed 50 characters")]
        public string Edition { get; set; }

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Pages must be at least 1")]
        public int Pages { get; set; }

        [StringLength(50, ErrorMessage = "Volume cannot exceed 50 characters")]
        public string Vol { get; set; }

        [StringLength(50, ErrorMessage = "Source cannot exceed 50 characters")]
        public string Source { get; set; }

        [StringLength(150, ErrorMessage = "Place of Publishers cannot exceed 150 characters")]
        public string PlaceofPublishers { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        [DataType(DataType.Currency)]
        public decimal? Price { get; set; }

        [StringLength(50, ErrorMessage = "Call Number cannot exceed 50 characters")]
        public string CallNumber { get; set; } // Optional

        public object tblSchoolName { get; internal set; }

        public string DepID { get; internal set; }

        public List<MaterialType> MaterialTypes { get; set; }  // For dropdown

        public int? SchoolID { get; internal set; }
    }
}
