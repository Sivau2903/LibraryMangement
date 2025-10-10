using LibraryMangement.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Models
{
    public class ReturnMaterialViewModel
    {

        public List<ReturnMaterialItemDTO> CirculationItems { get; set; } = new List<ReturnMaterialItemDTO>();
        public List<FineReasonDTO> FineReason { get; set; } = new List<FineReasonDTO>();
        public string BarcodeNumber { get; set; }
        public decimal CalculatedFineAmount { get; set; }
      
        public CirculationDisplay CirculationDisplay { get; set; }

        // Change to DTO type
        //public List<FineReasonDTO> FineReason { get; set; }

        //public decimal CalculatedFineAmount { get; set; } = 0;
        //public object CirculationItems { get; internal set; }
        //public List<FineReason> FineReason { get; set; }

    }

}