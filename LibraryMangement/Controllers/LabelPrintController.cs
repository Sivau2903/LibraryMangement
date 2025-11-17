using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Controllers
{
    public class LabelModel
    {
        public string Text { get; set; }
    }

    public class LabelPrintController : Controller
    {
        // GET: LabelPrint
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult LabelPrint(string value)
        {
            return View(new LabelModel { Text = value });
        }

    }
}