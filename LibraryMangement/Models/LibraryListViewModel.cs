using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class LibraryListViewModel
    {
        public string LibraryName { get; set; }
        public string LibrarianName { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; internal set; }
    }
}