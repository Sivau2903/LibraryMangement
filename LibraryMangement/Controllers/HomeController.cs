using LibraryMangement.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Controllers
{
    public class HomeController : Controller
    {


private static readonly Dictionary<string, string[]> BreadcrumbMap = new Dictionary<string, string[]>()
{
   
    { "Librarian-ManageMaterials", new[] { "ManageMaterials" } },
    { "Librarian-EditMaterial", new[] { "ManageMaterials", "EditMaterial" } },
    { "Librarian-ActiveIssues", new[] { "ActiveIssues" } },
      { "Librarian-AddAuthor", new[] { "AddAuthor" } },
        { "Librarian-AddFineReason", new[] { "AddFineReason" } },
          { "Librarian-AddMaterial", new[] { "AddMaterial" } },
            { "Librarian-AssignRole", new[] { "AssignRole" } },
              { "Librarian-BarcodeGeneration", new[] { "BarcodeGeneration" } },
                { "Librarian-BookingList", new[] { "BookingList" } },
                  { "Librarian-BulkUploadMaterials", new[] { "BulkUploadMaterials" } },
                    { "Librarian-ChangePassword", new[] { "ChangePassword" } },
                      { "Librarian-EditMaterialCopy", new[] { "ManageMaterialCopies", "EditMaterialCopy" } },
                        { "Librarian-ManageMaterialCopies", new[] { "ManageMaterialCopies" } },
                          { "Librarian-IssueMaterial", new[] { "IssueMaterial" } },
                            { "Librarian-FineReport", new[] { "FineReport" } },
                              { "Librarian-MyProfile", new[] { "MyProfile" } },
                                { "Librarian-Overduelist", new[] { "Overduelist" } },
                                  { "Librarian-OverdueReports", new[] { "OverdueReports" } },
                                    { "Librarian-PatronLists", new[] { "PatronLists" } },
                                      { "Librarian-PendingReservations", new[] { "PendingReservations" } },
                                        { "Librarian-ReservationRequests", new[] { "ReservationRequests" } },
                                          { "Librarian-ReturnMaterial", new[] { "ReturnMaterial" } },
                                            { "Librarian-_ConfirmFineModal", new[] { "ReturnMaterial", "_ConfirmFineModal" } },
                                              { "Librarian-_MaterialsTable", new[] { "ManageMaterials", "_MaterialsTable" } },
  

    // Patron
    { "Patron-IssuedHistory", new[] { "IssuedHistory" } },
    { "Patron-MyReservations", new[] { "MyReservations" } },
    { "Patron-RequestBook", new[] { "ViewBooks", "RequestBook" } },

};


        private string SplitCamelCase(string input)
        {
            return Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            var controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var action = filterContext.ActionDescriptor.ActionName;

            var breadcrumbs = new List<BreadcrumbItem>();

            // Always start with Dashboard
            string dashboardAction = "";
            switch (controller)
            {
                case "Librarian":
                    dashboardAction = "LibrarianDashboard";
                    break;
                case "Patron":
                    dashboardAction = "PatronDashboard";
                    break;
                default:
                    dashboardAction = "Index"; // fallback
                    break;
            }

            // Always start with Dashboard
            breadcrumbs.Add(new BreadcrumbItem
            {
                Title = "Dashboard",
                Url = Url.Action(dashboardAction, controller),
                IsActive = (action == "Dashboard")
            });

            // Build intermediate steps if mapping exists
            string key = controller + "-" + action;
            if (BreadcrumbMap.ContainsKey(key))
            {
                foreach (var step in BreadcrumbMap[key])
                {
                    bool isLast = step == BreadcrumbMap[key].Last();
                    breadcrumbs.Add(new BreadcrumbItem
                    {
                        Title = SplitCamelCase(step),
                        Url = Url.Action(step, controller),
                        IsActive = isLast
                    });
                }
            }
            else if (action != "Dashboard")
            {
                // fallback for single-step actions
                breadcrumbs.Add(new BreadcrumbItem
                {
                    Title = SplitCamelCase(action),
                    Url = Url.Action(action, controller),
                    IsActive = true
                });
            }

            ViewBag.Breadcrumbs = breadcrumbs;
        }

    }
}