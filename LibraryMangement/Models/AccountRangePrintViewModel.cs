using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Models
{
    public class AccountRangePrintViewModel
    {
        public string FromAccountNumber { get; set; }
        public string ToAccountNumber { get; set; }

        public List<MaterialCopyPrintDto> FoundCopies { get; set; } = new List<MaterialCopyPrintDto>();
        public List<string> MissingAccountNumbers { get; set; } = new List<string>();
    }
}