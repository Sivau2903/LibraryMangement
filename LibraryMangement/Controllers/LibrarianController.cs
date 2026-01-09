using LibraryMangement.Models;
using LibraryMangement.Services;
using Newtonsoft.Json;
using OfficeOpenXml;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
//using System.Windows.Media.Media3D;

namespace LibraryMangement.Controllers
{
    public class LibrarianController : HomeController
    {
        private readonly ICFAISMSEntities db = new ICFAISMSEntities();
        private readonly int? selectedDays;
        private string circulationUserID;

        public ActionResult LibrarianDashboard(int? selectedSchoolID)
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Login");

            string userId = Session["UserID"].ToString();
            string role = Session["Role"]?.ToString();

            var userUniversity = db.tblUserUniversities.FirstOrDefault(u => u.UserID == userId);
            if (userUniversity == null)
                return HttpNotFound("User's University information not found.");

            string universityID = userUniversity.UniversityID;
            Session["UniversityID"] = universityID;


            string Designation = Session["Designation"].ToString();
            List<tblLibrary> userLibraries = new List<tblLibrary>();

            if (Designation == "Librarian")
            {
                userLibraries = db.tblLibraries
                    .Where(s => s.LibrarianUserID == userId && s.IsActive == true)
                    .ToList();
            }
            else if (Designation == "Assistant Librarian")
            {
                userLibraries = (from al in db.tblLibraryAssistants
                                 join lib in db.tblLibraries on al.LibraryID equals lib.LibraryID
                                 where al.AssistantUserID == userId && al.IsActive == true
                                 select lib).ToList();
            }


            int? schoolID = null;


            if (selectedSchoolID.HasValue)
            {
                schoolID = selectedSchoolID.Value;
            }
            else if (Session["SchoolID"] != null)
            {
                schoolID = Convert.ToInt32(Session["SchoolID"]);
            }
            else
            {
                schoolID = userLibraries.FirstOrDefault()?.LibraryID ?? 0;
            }

            Session["SchoolID"] = schoolID;



            var schoolDropdown = userLibraries
                .Select(s => new SelectListItem
                {
                    Value = s.LibraryID.ToString(),
                    Text = s.LibraryName,
                    Selected = (s.LibraryID == schoolID && s.IsActive == true) 
                })
                .ToList();

            
            var universityName = db.tblUniversities
                .Where(u => u.UniversityID == universityID)
                .Select(u => u.UniversityName)
                .FirstOrDefault();

            string schoolName = db.tblLibraries
                .Where(s => s.LibraryID == schoolID && s.IsActive == true)
                .Select(s => s.LibraryName)
                .FirstOrDefault();

           
            string librarianName = role == "Librarian"
                ? db.tblEmployees.Where(e => e.UserID == userId).Select(e => e.EmployeeName).FirstOrDefault()
                : "";

           
            var materialsQuery = db.Materials.AsQueryable();
            if (schoolID.HasValue)
                materialsQuery = materialsQuery.Where(m => m.LibraryID == schoolID);
            else
                materialsQuery = materialsQuery.Where(m => m.UniversityID == universityID);

            var materialsByType = materialsQuery
                .GroupBy(m => m.MaterialType)
                .Select(g => new MaterialTypeCount
                {
                    MaterialType = g.Key,
                    Count = g.Count()
                })
                .ToList();

            
            var model = new LibrarianDashboardViewModel
            {
                UserID = userId,
                Name = librarianName,
                UniversityName = universityName,
                SchoolName = schoolName,
                SchoolList = schoolDropdown,
                HasMultipleSchools = schoolDropdown.Count > 1,
                TotalMaterials = materialsByType.Sum(x => x.Count),
                TotalPatrons = db.tblUserUniversities.Count(p => p.UniversityID == universityID),
                ActiveIssues = (from c in db.Circulations
                                join mc in db.MaterialCopies on c.CopyID equals mc.CopyID
                                where c.Status == "Issued" &&
                                      (schoolID.HasValue ? mc.LibraryID == schoolID : mc.UniversityID == universityID)
                                select c).Count(),
                OverdueIssues = (from c in db.Circulations
                                 join mc in db.MaterialCopies on c.CopyID equals mc.CopyID
                                 where c.Status == "Overdue" &&
                                       (schoolID.HasValue ? mc.LibraryID == schoolID : mc.UniversityID == universityID)
                                 select c).Count(),
                PendingReservations = db.Circulations.Count(c => c.Status == "Requested" &&
                    (schoolID.HasValue ? c.SchoolID == schoolID : c.UniversityID == universityID)),
                PendingBookinglist = (from r in db.Bookinglisteds
                                      join mc in db.Materials on r.MaterialID equals mc.MaterialID
                                      where r.Status == "Pending" &&
                                            (schoolID.HasValue ? mc.LibraryID == schoolID : mc.UniversityID == universityID)
                                      select r).Count(),
                MaterialsBelowStockLimit = db.Materials
                    .Count(m => (schoolID.HasValue ? m.LibraryID == schoolID : m.UniversityID == universityID)
                                && m.AvailableQuantity < m.StockLimit),
                MaterialsByType = materialsByType
            };

            List<SelectListItem> schools;

            if (Designation == "Librarian")
            {
                schools = db.tblLibraries
                    .Where(s => s.LibrarianUserID == userId && s.IsActive == true)
                    .Select(s => new SelectListItem
                    {
                        Value = s.LibraryID.ToString(),
                        Text = s.LibraryName
                    }).ToList();
            }
            else // Assistant Librarian
            {
                schools = (from al in db.tblLibraryAssistants
                           join lib in db.tblLibraries on al.LibraryID equals lib.LibraryID
                           where al.AssistantUserID == userId && al.IsActive == true
                           select new SelectListItem
                           {
                               Value = lib.LibraryID.ToString(),
                               Text = lib.LibraryName
                           }).ToList();
            }


            ViewBag.SchoolList = schools;
            ViewBag.HasMultipleSchools = schools.Count > 1;

            ViewBag.SchoolName = ViewBag.LibraryName != null && ViewBag.LibraryName == true;
           
            foreach (var s in schools)
                s.Selected = (s.Value == schoolID.ToString());


            Session["SchoolList"] = schools;

            Debug.WriteLine("the list is " + schools);

            return View(model);
        }

        [HttpGet]
        public ActionResult RequestAssistant()
        {
            string userId = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Login");

            string universityID = Session["UniversityID"]?.ToString();

         
            var libraries = db.tblLibraries
                              .Where(l => l.LibrarianUserID == userId)
                              .Select(l => new SelectListItem
                              {
                                  Value = l.LibraryID.ToString(),
                                  Text = l.LibraryName
                              })
                              .ToList();

           
            var assistantDesignationID = db.tblDesignations
                                           .Where(d => d.DesignationName == "Assistant Librarian")
                                           .Select(d => d.DesignationID)
                                           .FirstOrDefault();

            
            var assistants = db.tblEmployees
                               .Where(emp => emp.DesignationID == assistantDesignationID &&
                                             emp.UniversityID == universityID)
                               .Select(emp => new SelectListItem
                               {
                                   Value = emp.UserID,
                                   Text = emp.EmployeeName
                               })
                               .ToList();

            
            var existingRequests = (from r in db.tblAssistantRequests
                                    join l in db.tblLibraries on r.LibraryID equals l.LibraryID
                                    join emp in db.tblEmployees on r.AssistantUserID equals emp.UserID
                                    where r.LibrarianUserID == userId
                                    select new AssistantRequestViewModel
                                    {
                                        AssistantName = emp.EmployeeName,
                                        LibraryName = l.LibraryName,
                                        Status = r.Status,
                                        Remarks = r.Remarks,
                                        RequestDate = (DateTime)r.RequestDate

                                    }).ToList();

            var model = new AssistantRequestViewModel
            {
                LibrarianUserID = userId,
                LibraryList = libraries,
                AssistantList = assistants,
                ExistingRequests = existingRequests   
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestAssistant(AssistantRequestViewModel model)
        {
            string userId = Session["UserID"]?.ToString();
            string universityID = Session["UniversityID"]?.ToString();

            if (!ModelState.IsValid)
            {
                
                model.LibraryList = db.tblLibraries
                                      .Where(l => l.LibrarianUserID == userId)
                                      .Select(l => new SelectListItem
                                      {
                                          Value = l.LibraryID.ToString(),
                                          Text = l.LibraryName
                                      })
                                      .ToList();

                
                var assistantDesignationID = db.tblDesignations
                                               .Where(d => d.DesignationName == "Assistant Librarian")
                                               .Select(d => d.DesignationID)
                                               .FirstOrDefault();

                model.AssistantList = db.tblEmployees
                                        .Where(e => e.DesignationID == assistantDesignationID &&
                                                    e.UniversityID == universityID)
                                        .Select(e => new SelectListItem
                                        {
                                            Value = e.EmployeeID.ToString(),   
                                            Text = e.EmployeeName
                                        })
                                        .ToList();

                return View(model);
            }

            
            var request = new tblAssistantRequest
            {
                LibrarianUserID = userId,
                AssistantUserID = model.AssistantUserID, 
                LibraryID = model.LibraryID,
                RequestDate = DateTime.Now,
                Status = "Pending",
                UniversityID= universityID
            };

            db.tblAssistantRequests.Add(request);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Assistant Librarian request has been sent to Admin.";
            return RedirectToAction("LibrarianDashboard", "Librarian");
        }
        
        public ActionResult ManageMaterials()
        {
            var loggedInLibrarianId = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(loggedInLibrarianId))
                return RedirectToAction("Login", "Login");

            int SchoolID = (int)Session["SchoolID"];

            var materials = db.Materials
                              .Include(m => m.Author)
                              .Where(m => m.LibraryID == SchoolID)
                              .ToList();

            var model = materials.Select(m => new MaterialViewModel
            {
                MaterialID = m.MaterialID,
                Title = m.Title,
                Author = m.Author != null ? m.Author.Name : "",
                Edition = m.Edition,
                Description = m.Discription,
                PlaceofPublishers = m.PlaceofPublishers,
                YearPublished = m.YearPublished ?? 0,
                Pages = m.Pages ?? 0,
                Vol = m.Vol,
                Source = m.Source,
                AvailableQuantity = m.AvailableQuantity ?? 0,
                TotalQuantity = m.TotalQuantity ?? 0,
                MaterialType = m.MaterialType,
                DepID = m.tblLibrary != null ? m.tblLibrary.LibraryName : "N/A"
            }).ToList();

            return View(model);
        }

        //   public JsonResult GetMaterialAutoComplete(string field, string term)
        //   {
        //       int? SchoolID = Session["SchoolID"] as int?;
        //       int? UniversityID = Session["UniversityID"] as int?;

        //       var query = db.Materials.Include(m => m.Author).AsQueryable();

        //       // Filter by SchoolID first, else UniversityID
        //       if (SchoolID.HasValue && SchoolID.Value != 0)
        //           query = query.Where(m => m.SchoolID == SchoolID.Value);
        //       else if (UniversityID.HasValue && UniversityID.Value != 0)
        //           query = query.Where(m => m.UniversityID == UniversityID.Value.ToString()); // int comparison

        //       term = term.ToLower().Trim();

        //       List<string> result = new List<string>();

        //       switch (field)
        //       {
        //           case "Title":
        //               result = query.Where(m => m.Title.ToLower().Contains(term))
        //                             .Select(m => m.Title)
        //                             .Distinct()
        //                             .Take(10)
        //                             .ToList();
        //               break;

        //           case "Author":
        //               result = query.Where(m => m.Author != null && m.Author.Name.ToLower().Contains(term))
        //                             .Select(m => m.Author.Name)
        //                             .Distinct()
        //                             .Take(10)
        //                             .ToList();
        //               break;

        //           case "ISBN":
        //               result = query.Where(m => m.ISBN.ToLower().Contains(term))
        //                             .Select(m => m.ISBN)
        //                             .Distinct()
        //                             .Take(10)
        //                             .ToList();
        //               break;

        //           case "MaterialType":
        //               result = query.Where(m => m.MaterialType.ToLower().Contains(term))
        //                             .Select(m => m.MaterialType)
        //                             .Distinct()
        //                             .Take(10)
        //                             .ToList();
        //               break;

        //           case "Year":
        //               result = query.Where(m => m.YearPublished.ToString().Contains(term))
        //                             .Select(m => m.YearPublished.ToString())
        //                             .Distinct()
        //                             .Take(10)
        //                             .ToList();
        //               break;
        //       }

        //       return Json(result, JsonRequestBehavior.AllowGet);
        //   }


        //   [HttpPost]
        //   public ActionResult AdvancedSearchThreeKeywords(
        //string field1, string keyword1,
        //string field2, string keyword2,
        //string field3, string keyword3,
        //string clear)
        //   {
        //       var loggedInLibrarianId = Session["UserID"]?.ToString();
        //       if (string.IsNullOrEmpty(loggedInLibrarianId))
        //           return RedirectToAction("Login", "Login");

        //       //var universityId = db.Librarians.Include(x => x.tblUser).Include(x => x.tblUser.tblUserUniversities)
        //       //                     .Where(l => l.UserID == loggedInLibrarianId)
        //       //                     .FirstOrDefault();
        //       int SchoolID = (int)Session["SchoolID"];


        //       var model = new List<MaterialViewModel>();

        //       // If Clear button was pressed
        //       if (!string.IsNullOrEmpty(clear) && clear == "true")
        //       {
        //           ViewBag.KeywordFields = new List<string> { "Title", "Author", "ISBN", "PublisherPlace", "Year", "MaterialType" };
        //           ViewBag.Library_catgeoriess = db.tblSchools.Where(d => d.SchoolID == SchoolID).ToList();
        //           ViewBag.ActiveTab = "Advanced";
        //           return View("ManageMaterials", model); 
        //       }

        //       var materials = db.Materials
        //                         .Include(m => m.Author)
        //                         .Include(m => m.MaterialCopies)
        //                         .Where(m => m.SchoolID == SchoolID)
        //                         .AsQueryable();

        //       keyword1 = keyword1?.Trim();
        //       keyword2 = keyword2?.Trim();
        //       keyword3 = keyword3?.Trim();

        //       materials = materials.Where(m =>
        //           (string.IsNullOrEmpty(field1) || string.IsNullOrEmpty(keyword1) ||
        //               (field1 == "Title" && m.Title == keyword1) ||
        //               (field1 == "Author" && m.Author != null && m.Author.Name == keyword1) ||
        //               (field1 == "ISBN" && m.ISBN == keyword1) ||
        //               (field1 == "PublisherPlace" && m.PlaceofPublishers == keyword1) ||
        //               (field1 == "Year" && m.YearPublished.ToString() == keyword1) ||
        //               (field1 == "MaterialType" && m.MaterialType == keyword1)
        //           )
        //           &&
        //           (string.IsNullOrEmpty(field2) || string.IsNullOrEmpty(keyword2) ||
        //               (field2 == "Title" && m.Title == keyword2) ||
        //               (field2 == "Author" && m.Author != null && m.Author.Name == keyword2) ||
        //               (field2 == "ISBN" && m.ISBN == keyword2) ||
        //               (field2 == "PublisherPlace" && m.PlaceofPublishers == keyword2) ||
        //               (field2 == "Year" && m.YearPublished.ToString() == keyword2) ||
        //               (field2 == "MaterialType" && m.MaterialType == keyword2)
        //           )
        //           &&
        //           (string.IsNullOrEmpty(field3) || string.IsNullOrEmpty(keyword3) ||
        //               (field3 == "Title" && m.Title == keyword3) ||
        //               (field3 == "Author" && m.Author != null && m.Author.Name == keyword3) ||
        //               (field3 == "ISBN" && m.ISBN == keyword3) ||
        //               (field3 == "PublisherPlace" && m.PlaceofPublishers == keyword3) ||
        //               (field3 == "Year" && m.YearPublished.ToString() == keyword3) ||
        //               (field3 == "MaterialType" && m.MaterialType == keyword3)
        //           )
        //       );

        //       model = materials.Select(m => new MaterialViewModel
        //       {
        //           MaterialID = m.MaterialID,
        //           Title = m.Title,
        //           Author = m.Author != null ? m.Author.Name : "",
        //           Edition = m.Edition,
        //           Description = m.Discription,
        //           PlaceofPublishers = m.PlaceofPublishers,
        //           YearPublished = (int)m.YearPublished,
        //           Pages = m.Pages ?? 0,
        //           Vol = m.Vol,
        //           Source = m.Source,
        //           AvailableQuantity = (int)m.AvailableQuantity,
        //           TotalQuantity = (int)m.TotalQuantity,
        //           MaterialType = m.MaterialType,
        //           DepID = m.tblSchool != null ? m.tblSchool.SchoolName : "N/A"
        //       }).ToList();

        //       ViewBag.KeywordFields = new List<string> { "Title", "Author", "ISBN", "PublisherPlace", "Year", "MaterialType" };
        //       ViewBag.Library_catgeoriess = db.tblSchools.Where(d => d.SchoolID == SchoolID).ToList();
        //       ViewBag.ActiveTab = "Advanced";

        //       return View("ManageMaterials", model);
        //   }


        [HttpGet]
        public ActionResult AddMaterial()
        {
            var model = new MaterialViewModel
            {
                MaterialTypes = db.MaterialTypes.ToList()
            };
            return View(model);
        }

        public JsonResult GetAuthors(string term)
        {
            var authors = db.Authors.
                Where(a => a.Name.Contains(term)).Select(a => a.Name).ToList();
            return Json(authors, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddMaterial(MaterialViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingAuthor = db.Authors.FirstOrDefault(a => a.Name == model.Author);
                    if (existingAuthor == null)
                    {
                        existingAuthor = new Author { Name = model.Author };
                        db.Authors.Add(existingAuthor);
                        db.SaveChanges();
                    }

                    string universityId = Session["UniversityID"].ToString();
                    var SchoolID = Session["SchoolID"];

                    var material = new Material
                    {
                        Title = model.Title,
                        AuthorID = existingAuthor.AuthorID,
                        Edition = model.Edition,
                        //Discription = model.Description,
                        //Publisher = model.Publisher,
                        PlaceofPublishers = model.PlaceofPublishers,
                        YearPublished = model.YearPublished,
                        MaterialType = model.MaterialType,
                        Price = model.Price,
                        Vol = model.Vol,
                        Source = model.Source,
                        Pages = model.Pages,
                        ISBN = model.ISBN,
                        AvailableQuantity = model.AvailableQuantity,
                        TotalQuantity = model.TotalQuantity,
                        UniversityID = universityId.ToString(),
                        LibraryID = (int?)SchoolID,
                        CreatedAt = DateTime.Now

                    };

                    db.Materials.Add(material);
                    db.SaveChanges();

                    var copies = new List<MaterialCopy>();
                    int totalCopies = model.TotalQuantity > 0 ? model.TotalQuantity : 1;
                    for (int i = 1; i <= totalCopies; i++)
                    {
                        int libraryId = Session["SchoolID"] != null ? Convert.ToInt32(Session["SchoolID"]) : 0;
                        //string universityId = (Session["UniversityID"] ?? "").ToString();

                        // 1️⃣ Get all existing AccountNumbers under this LibraryID (and University if needed)
                        var existingAccountNumbers = db.MaterialCopies
                            .Where(c =>
                                c.LibraryID == libraryId &&
                                //c.UniversityID == universityId &&          // optional, if you use it
                                c.AccountNumber != null &&
                                c.AccountNumber != ""
                            )
                            .Select(c => c.AccountNumber)
                            .ToList();

                        // 2️⃣ Find highest numeric AccountNumber
                        int maxAccountNumber = 0;

                        if (existingAccountNumbers.Any())
                        {
                            maxAccountNumber = existingAccountNumbers
                                .Select(a =>
                                {
                                    int n;
                                    return int.TryParse(a, out n) ? n : 0;
                                })
                                .Max();
                        }

                        int accNo = maxAccountNumber + 1;

                        copies.Add(new MaterialCopy
                        {
                            MaterialID = material.MaterialID,
                            AccountNumber = accNo.ToString(),
                            BarcodeNumber = GenerateBarcode(material.MaterialID, accNo.ToString()),
                            CallNumber = string.IsNullOrEmpty(model.CallNumber) ? null : model.CallNumber,
                            Status = "Available",
                            UniversityID = universityId.ToString(),
                            LibraryID = (int?)SchoolID,
                            IsPrinted = false,
                        });
                    }

                    db.MaterialCopies.AddRange(copies);


                    db.SaveChanges();
                    TempData["Success"] = "Material added Succesfully & Kindly,Add the AccNo: By edit option";
                    return RedirectToAction("ManageMaterials");
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException ex)
                {

                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        string entityName = eve.Entry.Entity.GetType().Name;
                        foreach (var ve in eve.ValidationErrors)
                        {
                            System.Diagnostics.Debug.WriteLine($"Entity: {entityName}, Property: {ve.PropertyName}, Error: {ve.ErrorMessage}");
                        }
                    }

                    ViewBag.ErrorMessage = "Validation failed! Check debug output for details.";
                    return View(model);
                }
            }

            return View(model);
        }


        public ActionResult EditMaterial(int id)
        {
            var material = db.Materials.Find(id);

            if (material == null)
                return HttpNotFound();


            string authorName = "";
            if (material.AuthorID != null)
            {
                var author = db.Authors.Find(material.AuthorID);
                if (author != null)
                {
                    authorName = author.Name;
                }
            }

            var model = new MaterialViewModel
            {
                MaterialID = material.MaterialID,
                Title = material.Title,
                Author = authorName,
                Edition = material.Edition,
                Description = material.Discription,
                Publisher = material.Publisher,
                PlaceofPublishers = material.PlaceofPublishers,
                YearPublished = material.YearPublished ?? 0,
                MaterialType = material.MaterialType,
                Price = material.Price ?? 0m,
                Source = material.Source,
                Vol = material.Vol,
                Pages = material.Pages ?? 0,
                ISBN = material.ISBN,
                AvailableQuantity = (int)material.AvailableQuantity,
                TotalQuantity = (int)material.TotalQuantity
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditMaterial(MaterialViewModel model)
        {
            if (ModelState.IsValid)
            {
                var material = db.Materials.Find(model.MaterialID);

                if (material == null)
                    return HttpNotFound();


                int quantityDifference = (int)(model.TotalQuantity - material.TotalQuantity);


                material.Title = model.Title;
                material.Publisher = model.Publisher;
                material.YearPublished = model.YearPublished;
                material.ISBN = model.ISBN;
                material.AvailableQuantity = model.AvailableQuantity + quantityDifference;
                material.TotalQuantity = model.TotalQuantity;
                material.Price = (decimal)model.Price;
                material.Source = model.Source;
                material.Pages = model.Pages;
                material.Vol = model.Vol;
                material.PlaceofPublishers = model.PlaceofPublishers;
                material.Edition = model.Edition;
                material.Discription = model.Description;

                if (!string.IsNullOrEmpty(model.Author))
                {
                    var author = db.Authors.FirstOrDefault(a => a.Name == model.Author);
                    if (author != null)
                    {
                        material.AuthorID = author.AuthorID;
                    }
                    else
                    {
                        var newAuthor = new Author { Name = model.Author };
                        db.Authors.Add(newAuthor);
                        db.SaveChanges();
                        material.AuthorID = newAuthor.AuthorID;
                    }
                }

                if (quantityDifference > 0)
                {
                    // Get LibraryID (SchoolID) from session
                    int libraryId = Session["SchoolID"] != null ? Convert.ToInt32(Session["SchoolID"]) : 0;
                    //string universityId = (Session["UniversityID"] ?? "").ToString();

                    // 1️⃣ Get all existing AccountNumbers under this LibraryID (and University if needed)
                    var existingAccountNumbers = db.MaterialCopies
                        .Where(c =>
                            c.LibraryID == libraryId &&
                            //c.UniversityID == universityId &&          // optional, if you use it
                            c.AccountNumber != null &&
                            c.AccountNumber != ""
                        )
                        .Select(c => c.AccountNumber)
                        .ToList();

                    // 2️⃣ Find highest numeric AccountNumber
                    int maxAccountNumber = 0;

                    if (existingAccountNumbers.Any())
                    {
                        maxAccountNumber = existingAccountNumbers
                            .Select(a =>
                            {
                                int n;
                                return int.TryParse(a, out n) ? n : 0;
                            })
                            .Max();
                    }

                    int nextAccountNumber = maxAccountNumber + 1;

                    // 3️⃣ Create new copies starting from nextAccountNumber
                    for (int i = 0; i < quantityDifference; i++)
                    {
                        int accNo = nextAccountNumber + i;

                        var newCopy = new MaterialCopy
                        {
                            MaterialID = material.MaterialID,
                            LibraryID = libraryId,
                            //UniversityID = universityId,
                            AccountNumber = accNo.ToString(), // or use GenerateAccountNumber(...)
                            BarcodeNumber = GenerateBarcode(material.MaterialID, accNo.ToString()),
                            CallNumber = model.CallNumber,
                            Status = "Available"
                        };

                        db.MaterialCopies.Add(newCopy);
                    }

                    db.SaveChanges();
                }


                if (quantityDifference < 0)
                {
                    var copiesToRemove = db.MaterialCopies
                        .Where(c => c.MaterialID == material.MaterialID && c.Status == "Available")
                        .OrderByDescending(c => c.CopyID)
                        .Take(Math.Abs(quantityDifference))
                        .ToList();

                    db.MaterialCopies.RemoveRange(copiesToRemove);
                }

                db.SaveChanges();
                TempData["Success"] = "Edited Succesfully";
                return RedirectToAction("ManageMaterials");
            }

            return View(model);
        }

       
        public ActionResult BulkUploadMaterials()
        {
            return View(new List<MaterialBulkUploadPreviewModel>());
        }

        public ActionResult DownloadBulkTemplate()
        {
            
            string filePath = Server.MapPath("~/Templates/BulkUploadTemplate.xlsx");
            string fileName = "BulkUploadTemplate.xlsx";

            if (!System.IO.File.Exists(filePath))
            {
                return HttpNotFound("Template not found.");
            }

            return File(filePath, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private string NormalizeString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            // Remove extra spaces between words
            var cleaned = Regex.Replace(input.Trim(), @"\s+", " ");

            return cleaned.ToLowerInvariant();
        }


        [HttpPost]
        public JsonResult BulkUploadMaterialsAjax(string materialsJson)
        {
            if (string.IsNullOrEmpty(materialsJson))
                return Json(new { success = false, message = "No data received" });

            try
            {
                var previewData = JsonConvert.DeserializeObject<List<MaterialBulkUploadPreviewModel>>(materialsJson);

                if (previewData == null || !previewData.Any())
                    return Json(new { success = false, message = "No data found in the Excel file" });

                var skippedRows = new List<string>();

                previewData = previewData
                    .Where((p, index) =>
                    {
                        if (string.IsNullOrWhiteSpace(p.Title))
                        {
                            skippedRows.Add($"Row {index + 1}: Title is empty");
                            return false;
                        }
                        return true;
                    }).ToList();

                if (!previewData.Any())
                    return Json(new { success = false, message = "No valid rows found (Title is required)", skipped = skippedRows });

                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        // DEBUG: check what values you really have
                        var debugData = previewData.Select((p, idx) => new
                        {
                            Row = idx + 1,
                            p.Title,
                            p.AccountNumber
                        }).ToList();

                        foreach (var firstRow in previewData)
                        {

                            if (Response.ClientDisconnectedToken.IsCancellationRequested)
                            {
                                transaction.Rollback();
                                return Json(new { success = false, message = "Saving cancelled by user." });
                            }

                            var authorNameNormalized = NormalizeString(firstRow.AuthorName);

                            var author = db.Authors
                                .AsEnumerable()  // required to use NormalizeString on DB values
                                .FirstOrDefault(a => NormalizeString(a.Name) == authorNameNormalized);

                            if (author == null && !string.IsNullOrWhiteSpace(authorNameNormalized))
                            {
                                // Save clean version (first letter caps, etc., if you want)
                                author = new Author { Name = authorNameNormalized };
                                db.Authors.Add(author);
                                db.SaveChanges();
                            }


                            var universityId = Session["UniversityID"]?.ToString();
                            var SchoolID = (int)Session["SchoolID"];

                            var editionNormalized = firstRow.Edition?.Trim().ToLower() ?? "";
                            var isbnNormalized = firstRow.ISBN?.Trim().ToLower() ?? "";
                            var authorId = author?.AuthorID;

                            var existingMaterial = db.Materials.FirstOrDefault(m =>
                                m.Title.ToLower() == firstRow.Title.Trim().ToLower() &&
                                m.AuthorID == authorId &&
                                ((m.Edition ?? "").ToLower() == editionNormalized) &&
                                ((m.ISBN ?? "").ToLower() == isbnNormalized) &&
                                ((m.UniversityID ?? "").ToLower() == universityId) &&
                                 ((m.LibraryID == SchoolID))
                            );

                            if (existingMaterial != null)
                            {
                                existingMaterial.TotalQuantity += 1;
                                existingMaterial.AvailableQuantity += 1;
                                db.SaveChanges();

                                int startIndex = db.MaterialCopies.Count(c => c.MaterialID == existingMaterial.MaterialID);

                                var accNo = (firstRow.AccountNumber ?? "").Trim();

                                var copy = new MaterialCopy
                                {
                                    MaterialID = existingMaterial.MaterialID,
                                    AccountNumber = accNo,
                                    BarcodeNumber = GenerateBarcode(existingMaterial.MaterialID, accNo),
                                    CallNumber = firstRow.CallNumber,
                                    Status = "Available",
                                    UniversityID = universityId,
                                    LibraryID = SchoolID,
                                    IsPrinted = false,
                                };
                                db.MaterialCopies.Add(copy);
                                db.SaveChanges();
                            }

                            else
                            {
                                var accNo = (firstRow.AccountNumber ?? "").Trim();
                                var material = new Material
                                {
                                    Title = firstRow.Title?.Trim(),
                                    AuthorID = author?.AuthorID,
                                    //Publisher = string.IsNullOrWhiteSpace(firstRow.Publisher) ? null : firstRow.Publisher.Trim(),
                                    PlaceofPublishers = string.IsNullOrWhiteSpace(firstRow.PlaceofPublishers) ? null : firstRow.PlaceofPublishers.Trim(),
                                    Discription = string.IsNullOrWhiteSpace(firstRow.Discription) ? null : firstRow.Discription.Trim(),
                                    Vol = string.IsNullOrWhiteSpace(firstRow.Vol) ? null : firstRow.Vol.Trim(),
                                    Pages = firstRow.Pages,
                                    Price = firstRow.Price,
                                    Source = string.IsNullOrWhiteSpace(firstRow.Source) ? null : firstRow.Source.Trim(),
                                    Edition = firstRow.Edition,
                                    ISBN = firstRow.ISBN,
                                    YearPublished = firstRow.YearPublished,
                                    TotalQuantity = 1,
                                    AvailableQuantity = 1,
                                    MaterialType = "Book",
                                    CreatedAt = DateTime.Now,
                                    UniversityID = universityId,
                                    LibraryID = SchoolID
                                };

                                db.Materials.Add(material);
                                db.SaveChanges();

                                var copy = new MaterialCopy
                                {
                                    MaterialID = material.MaterialID,
                                    AccountNumber = accNo,
                                    BarcodeNumber = GenerateBarcode(material.MaterialID, accNo),
                                    CallNumber = firstRow.CallNumber,
                                    Status = "Available",
                                    UniversityID = universityId,
                                    LibraryID = SchoolID,
                                    IsPrinted = false
                                };
                                db.MaterialCopies.Add(copy);
                                db.SaveChanges();
                            }
                        }

                        transaction.Commit();
                        return Json(new { success = true, skipped = skippedRows });
                    }

                    catch (Exception ex)
                    {
                        transaction.Commit();
                        return Json(new { success = true, skipped = skippedRows, message = ex.Message, redirectUrl = Url.Action("ManageMaterials", "Librarian") });

                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private void LogValidationErrors(System.Data.Entity.Validation.DbEntityValidationException ex)
        {
            foreach (var eve in ex.EntityValidationErrors)
            {
                System.Diagnostics.Debug.WriteLine($"Entity of type {eve.Entry.Entity.GetType().Name} in state {eve.Entry.State} has the following validation errors:");
                foreach (var ve in eve.ValidationErrors)
                {
                    System.Diagnostics.Debug.WriteLine($"- Property: {ve.PropertyName}, Error: {ve.ErrorMessage}");
                }
            }
        }

        /* private List<MaterialBulkUploadPreviewModel> ParseExcelFile(string filePath)
         {
             ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
             var records = new List<MaterialBulkUploadPreviewModel>();

             using (var package = new OfficeOpenXml.ExcelPackage(new FileInfo(filePath)))
             {
                 var worksheet = package.Workbook.Worksheets[0];
                 var rowCount = worksheet.Dimension.Rows;

                 for (int row = 2; row <= rowCount; row++)  // Skip header row
                 {
                     int yearPublished = 0;
                     int availableQuantity = 0;
                     int totalQuantity = 0;
                     int pages = 0;
                     int copyCount = 0;
                     decimal price = 0m;

                     int.TryParse(worksheet.Cells[row, 7].Text.Trim(), out yearPublished);
                     int.TryParse(worksheet.Cells[row, 15].Text.Trim(), out availableQuantity);
                     int.TryParse(worksheet.Cells[row, 14].Text.Trim(), out totalQuantity);
                     int.TryParse(worksheet.Cells[row, 9].Text.Trim(), out pages);
                     int.TryParse(worksheet.Cells[row, 12].Text.Trim(), out copyCount);
                     decimal.TryParse(worksheet.Cells[row, 12].Text.Trim(), out price);

                     var record = new MaterialBulkUploadPreviewModel
                     {
                         Title = worksheet.Cells[row, 4].Text.Trim(),
                         AuthorName = worksheet.Cells[row, 5].Text.Trim(),
                         Publisher = worksheet.Cells[row, 6].Text.Trim(),
                         PlaceofPublishers = worksheet.Cells[row, 6].Text.Trim(),
                         YearPublished = yearPublished,
                         Edition = worksheet.Cells[row, 8].Text.Trim(),
                         Pages = pages,
                         Vol = worksheet.Cells[row, 10].Text.Trim(),
                         Source = worksheet.Cells[row, 11].Text.Trim(),
                         Price = price,
                         ISBN = worksheet.Cells[row, 13].Text.Trim(),
                         AvailableQuantity = availableQuantity,
                         TotalQuantity = totalQuantity,
                         CallNumber = worksheet.Cells[row, 3].Text.Trim(),
                         AccountNumber = worksheet.Cells[row, 2].Text.Trim(),
                         CopyCount = copyCount
                     };

                     records.Add(record);
                 }
             }

             return records;
         }*/


        private string GenerateAccountNumber(int materialId, int copyIndex)
        {
            return $"ACCT-{materialId}-{copyIndex:D4}";
        }

        private string GenerateBarcode(int materialId, string accNo)
        {
            return $"BC-{materialId}-{accNo}";
        }

        public ActionResult ManageMaterialCopies()
        {

            var SchoolID = Session["SchoolID"];


            var copies = db.MaterialCopies
                           .Include(mc => mc.Material)
                           .Where(mc => mc.LibraryID == (int?)SchoolID)
                           .ToList();

            return View(copies);
        }


        public ActionResult EditMaterialCopy(int id)
        {
            var copy = db.MaterialCopies.Find(id);
            if (copy == null) return HttpNotFound();

            var model = new MaterialCopyViewModel
            {
                CopyID = copy.CopyID,
                MaterialID = (int)copy.MaterialID,
                AccountNumber = copy.AccountNumber,
                BarcodeNumber = copy.BarcodeNumber,
                CallNumber = copy.CallNumber,
                Status = copy.Status
            };

            ViewBag.Materials = new SelectList(db.Materials.ToList(), "MaterialID", "Title", copy.MaterialID);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditMaterialCopy(MaterialCopyViewModel model)
        {
            if (ModelState.IsValid)
            {
                var copy = db.MaterialCopies.Find(model.CopyID);
                if (copy == null) return HttpNotFound();

                copy.MaterialID = model.MaterialID;
                copy.AccountNumber = model.AccountNumber;
                copy.BarcodeNumber = model.BarcodeNumber;
                copy.CallNumber = model.CallNumber;
                copy.Status = model.Status;

                db.SaveChanges();
                return RedirectToAction("ManageMaterialCopies");
            }

            ViewBag.Materials = new SelectList(db.Materials.ToList(), "MaterialID", "Title", model.MaterialID);
            return View(model);
        }

        public ActionResult IssueMaterial(string selectedRole = "Student")
        {
            int? schoolId = Session["SchoolID"] as int?;
            int? universityId = Session["UniversityID"] as int?;

            if (!schoolId.HasValue && !universityId.HasValue)
            {
                TempData["Error"] = "Please login to access the page.";
                return RedirectToAction("Login", "Login");
            }

            var requests = db.Circulations
                             .Include(c => c.MaterialCopy.Material)
                             .Where(c => c.Status == "Requested")
                             .ToList();

            if (schoolId.HasValue)
                requests = requests.Where(c => c.SchoolID == schoolId.Value).ToList();
            else if (universityId.HasValue)
                requests = requests.Where(c => c.UniversityID == universityId.Value.ToString()).ToList();


            var result = requests.Select(c =>
            {
                string name = "N/A";
                string email = "N/A";
                string titlename = "N/A";
                string id = "N/A";

                if (selectedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
                {
                    var student = db.tblStudents.FirstOrDefault(s => s.UserID == c.UserID);
                    if (student != null)
                    {
                        name = student.StudentName;
                        email = student.AcademicEmail;
                        id = student.StudentID;
                    }
                }

                else if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
                {
                    var employee = db.tblEmployees.FirstOrDefault(e => e.UserID == c.UserID);
                    if (employee != null)
                    {
                        name = employee.EmployeeName;
                        email = employee.Email;
                        id = employee.EmployeeID.ToString();
                    }
                }

                var title = db.Materials.FirstOrDefault(s => s.MaterialID == c.MaterialID);
                if (title != null)
                {
                    titlename = title.Title;

                }

                return new IssueMaterialViewModel
                {
                    CirculationID = c.CirculationID,
                    MaterialID = (int)c.MaterialID,
                    MaterialTitle = titlename,
                    ID = id,
                    UserID = c.UserID,
                    UserName = name,
                    UserEmail = email,
                    PatronType = selectedRole,
                    RequestedDate = c.RequestedDate,
                    Status = c.Status
                };
            })
            .Where(x => x.UserName != "N/A") 
            .ToList();

            ViewBag.SelectedRole = selectedRole;
            return View(result);
        }

        [HttpGet]
        public JsonResult GetAvailableBarcodes(int materialId)
        {
            try
            {
                var barcodes = db.MaterialCopies
                                 .Where(mc => mc.MaterialID == materialId && mc.Status.Trim().ToLower() == "available")
                                 .Select(mc => new
                                 {
                                     Barcode = mc.BarcodeNumber,
                                     IsPrinted = mc.IsPrinted 
                                 })
                                 .ToList();

                return Json(new
                {
                    success = true,
                    materialId = materialId,
                    count = barcodes.Count,
                    barcodes = barcodes
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    materialId = materialId,
                    barcodes = new List<object>()
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult MarkBarcodePrinted(string barcode)
        {
            var copy = db.MaterialCopies.FirstOrDefault(m => m.BarcodeNumber == barcode);
            if (copy != null)
            {
                copy.IsPrinted = true;
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult IssueSelectedReserves(string selectedRole, string userId, List<ReserveIssueModel> selectedReserves)
        {
            Debug.WriteLine("selectedRole" + selectedRole);
            try
            {
                foreach (var item in selectedReserves)
                {
                    var circ = db.Circulations.FirstOrDefault(c => c.CirculationID == item.CirculationID);
                    if (circ == null) continue;

                    var copy = db.MaterialCopies
                                 .FirstOrDefault(mc => mc.MaterialID == circ.MaterialID
                                                    && mc.BarcodeNumber == item.Barcode
                                                    && mc.Status == "Available");
                    if (copy == null)
                        return Json(new { success = false, message = $"Invalid barcode for {circ.Material.Title}" });

                    string userid = Session["UserID"].ToString();   

                    circ.CopyID = copy.CopyID;
                    circ.IssueDate = DateTime.Now;
                    circ.IssuedBy = userid;



                    if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
                    {
                        circ.DueDate = DateTime.Now.AddDays(200);
                    }
                    else 
                    {
                        circ.DueDate = DateTime.Now.AddDays(15);
                    }

                    circ.Status = "Issued";
                    circ.BarcodeNumber = item.Barcode;

                    copy.Status = "Issued";

                    var material = db.Materials.FirstOrDefault(m => m.MaterialID == circ.MaterialID);
                    if (material != null && material.AvailableQuantity > 0)
                    {
                        material.AvailableQuantity -= 1;
                    }
                }

                db.SaveChanges();

                
                var issued = db.Circulations
                               .Where(c => c.UserID == userId && c.Status == "Issued")
                               .Select(c => new
                               {
                                   c.CirculationID,
                                   MaterialTitle = c.Material.Title,
                                   c.IssueDate,
                                   c.DueDate,
                                   c.Status
                               }).ToList();

                var reserved = db.Circulations
                                 .Where(c => c.UserID == userId && c.Status == "Reserved")
                                 .Select(c => new
                                 {
                                     c.CirculationID,
                                     MaterialTitle = c.Material.Title,
                                     c.RequestedDate,
                                     c.Status,
                                     c.MaterialID
                                 }).ToList();

                return Json(new { success = true, issued, reserved });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        public class ReserveIssueModel
        {
            public int CirculationID { get; set; }
            public int PatronID { get; set; }
            public string Barcode { get; set; }
            public bool HasMultipleSchools { get; set; }
            public List<SelectListItem> SchoolList { get; set; }

        }

        public ActionResult ReturnMaterial()
        {
            var model = new ReturnMaterialViewModel
            {
                FineReason = db.FineReasons.Select(f => new FineReasonDTO
                {
                    ReasonText = f.Reason,
                    FinePerDay = f.FineAmount,
                    Value = f.Reason
                }).ToList(),
                CirculationItems = new List<ReturnMaterialItemDTO>() // start empty
            };
            return View(model);
        }

        [HttpGet]
        public JsonResult GetReturnDetailsByBarcode(string barcode)
        {
            if (string.IsNullOrEmpty(barcode))
                return Json(new { success = false, message = "Barcode is required" }, JsonRequestBehavior.AllowGet);

            int schoolId = 0;
            int universityId = 0;

            if (Session["SchoolID"] != null)
                int.TryParse(Session["SchoolID"].ToString(), out schoolId);
            else if (Session["UniversityID"] != null)
                int.TryParse(Session["UniversityID"].ToString(), out universityId);

            
            var circulationRecord = db.Circulations
                .FirstOrDefault(c => (c.Status == "Issued" || c.Status == "Overdue" || c.Status == "Renewed")
                                     && c.BarcodeNumber == barcode
                                     && ((schoolId != 0 && c.SchoolID == schoolId)
                                         || (schoolId == 0 && universityId != 0 && c.UniversityID == universityId.ToString())));

            if (circulationRecord == null)
                return Json(new { success = false, message = "No record found" }, JsonRequestBehavior.AllowGet);

            
            var material = db.Materials.FirstOrDefault(m => m.MaterialID == circulationRecord.MaterialID);
            string materialTitle = material?.Title ?? "Unknown Material";

            
            string userId = circulationRecord.UserID;
            string roleName = (from ur in db.tblUserRoles
                               join r in db.tblRoles on ur.RoleID equals r.RoleID
                               where ur.UserID == userId
                               select r.RoleName).FirstOrDefault();

           
            string patronName = "";
            string patronEmail = "";
            string patronId = "";

            if (!string.IsNullOrEmpty(roleName) && roleName.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                
                var student = db.tblStudents.FirstOrDefault(s => s.UserID == userId);
                if (student != null)
                {
                    patronName = student.StudentName;
                    patronEmail = student.AcademicEmail;
                    patronId = student.StudentID.ToString();
                }
            }
            else
            {
                
                var employee = db.tblEmployees.FirstOrDefault(e => e.UserID == userId);
                if (employee != null)
                {
                    patronName = employee.EmployeeName;
                    patronEmail = employee.Email;
                    patronId = employee.EmployeeID.ToString();
                }
            }

           
            var result = new
            {
                circulationRecord.CirculationID,
                circulationRecord.BarcodeNumber,
                MaterialTitle = materialTitle,
                PatronName = patronName,
                PatronID = patronId,
                PatronEmail = patronEmail,
                RequestedDate = circulationRecord.RequestedDate?.ToString("yyyy-MM-dd") ?? "",
                IssueDate = circulationRecord.IssueDate?.ToString("yyyy-MM-dd") ?? "",
                DueDate = circulationRecord.DueDate?.ToString("yyyy-MM-dd") ?? "",
                Status = circulationRecord.Status,
                FineAmount = circulationRecord.FineAmount ?? 0
            };

            return Json(new { success = true, record = result }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessReturnRenew(int CirculationID, string action, string FineReason, decimal? fineAmount, string paymentStatus)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] ProcessReturnRenew called: CirculationID={CirculationID}, action={action}, FineReason={FineReason}, fineAmount={fineAmount}, paymentStatus={paymentStatus}");

            var circulation = db.Circulations
                                .Include(c => c.MaterialCopy)
                                .Include(c => c.MaterialCopy.Material)
                                //.Include(c => c.Patron)
                                .FirstOrDefault(c => c.CirculationID == CirculationID);

            if (circulation == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Circulation not found for ID={CirculationID}");
                return HttpNotFound();
            }

            string patronId = circulation.UserID;
            var universityId = circulation.UniversityID;

            
            if (FineReason == "Book Lost")
            {
                circulation.Status = "BookLost";
                circulation.ReturnDate = DateTime.Now;
                circulation.MaterialCopy.Status = "Lost";
            }
            else
            {
                if (action == "Return")
                {
                    circulation.Status = "Returned";
                    circulation.ReturnDate = DateTime.Now;

                   
                    var nextBooking = db.Bookinglisteds
                                        .Where(b => b.MaterialID == circulation.MaterialCopy.MaterialID && b.Status == "Pending")
                                        .OrderBy(b => b.BookingDate) 
                                        .FirstOrDefault();

                    if (nextBooking != null)
                    {

                        nextBooking.Status = "Notified";
                        nextBooking.AssignedDate = DateTime.Now;
                        nextBooking.HoldExpiryDate = DateTime.Now.AddDays(2);

                        circulation.MaterialCopy.Status = "OnHold";

                        var patron = db.tblStudents.FirstOrDefault(p => p.UserID == patronId);

                        if (!string.IsNullOrWhiteSpace(patron.AcademicEmail))
                        {
                            EmailService.SendBookingAvailableNotification(patron.AcademicEmail, nextBooking);
                        }
                    }
                    else
                    {

                        circulation.MaterialCopy.Status = "Available";

                        if (circulation.MaterialCopy?.Material != null)
                        {
                            circulation.MaterialCopy.Material.AvailableQuantity += 1;
                        }
                    }
                }
                else if (action == "Renew")
                {
                    
                    var nextBooking = db.Bookinglisteds
                                        .Where(b => b.MaterialID == circulation.MaterialCopy.MaterialID && b.Status == "Pending")
                                        .OrderBy(b => b.BookingDate)
                                        .FirstOrDefault();

                    if (nextBooking != null)
                    {
                        TempData["Error"] = "The Book is the queue you can't Renew it";
                        return RedirectToAction("ReturnMaterial");
                    }

                    else
                    {

                        var patron = db.tblStudents.FirstOrDefault(p => p.UserID == patronId);
                        //if (patron != null && patron.PatronType == "Faculty")
                        //    circulation.DueDate = DateTime.Now.AddDays(20);
                        //else
                        circulation.DueDate = DateTime.Now.AddDays(7);
                        circulation.Status = "Renewed";

                    }
                }
            }

            
            if (!string.IsNullOrEmpty(FineReason) && fineAmount > 0)
            {
                var fineDetail = new FineDetail
                {
                    UserID = patronId,
                    CirculationID = circulation.CirculationID,
                    Reason = FineReason,
                    Amount = fineAmount,
                    AppliedDate = DateTime.Now,
                    Paid = paymentStatus == "Paid",
                    UniversityID = universityId,
                };
                db.FineDetails.Add(fineDetail);

                circulation.FineAmount = fineAmount;
            }

            db.SaveChanges();
            System.Diagnostics.Debug.WriteLine("[DEBUG] db.SaveChanges completed successfully");

            TempData["Success"] = "Operation completed successfully!";
            return RedirectToAction("ReturnMaterial");
        }



        public ActionResult AvailabilityReport(string title, string author, string materialType)
        {
            var query = db.Materials.Include(m => m.Author).AsQueryable();

           
            int? schoolId = Session["SchoolID"] as int?;
            string universityId = Session["UniversityID"] as string;

            if (schoolId.HasValue)
                query = query.Where(m => m.LibraryID == schoolId.Value);
            else if (!string.IsNullOrEmpty(universityId))
                query = query.Where(m => m.UniversityID == universityId);

           
            ViewBag.MaterialTypes = db.MaterialTypes
                                      .Select(mt => new SelectListItem
                                      {
                                          Value = mt.TypeName,
                                          Text = mt.TypeName
                                      }).ToList();

           
            ViewBag.Titles = db.Materials.Select(m => m.Title).Distinct().ToList();
            ViewBag.Authors = db.Authors.Select(a => a.Name).Distinct().ToList();

           
            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(author) && !string.IsNullOrEmpty(materialType))
            {
               
                query = query.Where(m =>
                    m.Title == title &&
                    m.Author.Name == author &&
                    m.MaterialType == materialType
                );
            }
            else
            {
                
                if (!string.IsNullOrEmpty(title))
                    query = query.Where(m => m.Title.Contains(title));

                if (!string.IsNullOrEmpty(author))
                    query = query.Where(m => m.Author.Name.Contains(author));

                if (!string.IsNullOrEmpty(materialType))
                    query = query.Where(m => m.MaterialType == materialType);
            }

            var materials = query.ToList();

            var model = materials.Select(m => new AvailabilityReportViewModel
            {
                MaterialID = m.MaterialID,
                Title = m.Title,
                AuthorName = m.Author.Name,
                MaterialType = m.MaterialType,
                TotalQuantity = m.TotalQuantity,
                AvailableQuantity = db.MaterialCopies.Count(c => c.MaterialID == m.MaterialID && c.Status == "Available"),
                IssuedQuantity = db.MaterialCopies.Count(c => c.MaterialID == m.MaterialID && c.Status == "Issued"),
                BookLostQuantity = db.MaterialCopies.Count(c => c.MaterialID == m.MaterialID && c.Status == "Lost")
            }).ToList();

            return View(model);
        }

        public JsonResult GetFilteredData(string title, string author, string materialType)
        {
            var query = db.Materials.Include(m => m.Author).AsQueryable();

            // --- Apply SchoolID or UniversityID filter ---
            int? schoolId = Session["SchoolID"] as int?;
            int? universityId = Session["UniversityID"] as int?;

            if (schoolId.HasValue)
            {
                query = query.Where(m => m.LibraryID == schoolId.Value);
            }
            else if (universityId.HasValue)
            {
                query = query.Where(m => m.UniversityID == universityId.Value.ToString());
            }

            // --- Apply dynamic filters ---
            if (!string.IsNullOrEmpty(title))
                query = query.Where(m => m.Title == title);

            if (!string.IsNullOrEmpty(materialType))
                query = query.Where(m => m.MaterialType == materialType);

            if (!string.IsNullOrEmpty(author))
                query = query.Where(m => m.Author.Name == author);

            // --- Distinct filtered lists ---
            var authors = query.Select(m => m.Author.Name).Distinct().ToList();
            var materialTypes = query.Select(m => m.MaterialType).Distinct().ToList();

            return Json(new
            {
                Authors = authors,
                MaterialTypes = materialTypes
            }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult OverdueReports(string fromDate, string toDate)
        {
            DateTime? from = string.IsNullOrEmpty(fromDate) ? (DateTime?)null : DateTime.Parse(fromDate);
            DateTime? to = string.IsNullOrEmpty(toDate) ? (DateTime?)null : DateTime.Parse(toDate);

            int? schoolId = Session["SchoolID"] as int?;
            int? universityId = Session["UniversityID"] as int?;

            var query = db.Circulations
                          .Include(c => c.MaterialCopy.Material)
                          .Where(c => c.Status == "Overdue");

            if (schoolId.HasValue)
                query = query.Where(c => c.SchoolID == schoolId.Value);
            else if (universityId.HasValue)
                query = query.Where(c => c.UniversityID == universityId.Value.ToString());

            if (from.HasValue)
                query = query.Where(c => c.DueDate >= from.Value);

            if (to.HasValue)
                query = query.Where(c => c.DueDate <= to.Value);

            var model = query.ToList().Select(c =>
            {
                var student = db.tblStudents.FirstOrDefault(s => s.UserID == c.UserID);
                string studentName = student != null ? student.StudentName : "N/A";

                return new OverdueReportViewModel
                {
                    CirculationID = c.CirculationID,
                    MaterialTitle = c.MaterialCopy.Material?.Title ?? "N/A",
                    StudentName = studentName,
                    IssueDate = c.IssueDate,
                    DueDate = c.DueDate,
                    DaysOverdue = c.DueDate.HasValue ? (DateTime.Now - c.DueDate.Value).Days : 0,
                    FineAmount = c.FineAmount ?? 0,
                    Status = c.Status
                };
            })
            .Where(c => c.StudentName != "N/A")
            .ToList();

            return View(model);
        }

        public ActionResult RequestReport(string selectedRole = "Student", string fromDate = null, string toDate = null)
        {
            DateTime? from = string.IsNullOrEmpty(fromDate) ? (DateTime?)null : DateTime.Parse(fromDate);
            DateTime? to = string.IsNullOrEmpty(toDate) ? (DateTime?)null : DateTime.Parse(toDate);

            int? schoolId = Session["SchoolID"] as int?;
            int? universityId = Session["UniversityID"] as int?;

            var query = db.Circulations
                          .Include(c => c.MaterialCopy.Material)
                          .Where(c => c.Status == "Requested");

           
            if (schoolId.HasValue)
                query = query.Where(c => c.SchoolID == schoolId.Value);
            else if (universityId.HasValue)
                query = query.Where(c => c.UniversityID == universityId.Value.ToString());

            
            if (from.HasValue)
                query = query.Where(c => c.RequestedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(c => c.RequestedDate <= to.Value);

            
            List<RequestReportViewModel> model = new List<RequestReportViewModel>();

            foreach (var c in query.ToList())
            {
                string userName = "N/A";

                if (selectedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
                {
                    var student = db.tblStudents.FirstOrDefault(s => s.UserID == c.UserID);
                    if (student != null)
                        userName = student.StudentName;
                }
                else if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
                {
                    var employee = db.tblEmployees.FirstOrDefault(e => e.UserID == c.UserID);
                    if (employee != null)
                        userName = employee.EmployeeName;
                }

                if (userName != "N/A")
                {
                    model.Add(new RequestReportViewModel
                    {
                        CirculationID = c.CirculationID,
                        UserID = c.UserID,
                        UserName = userName,
                        MaterialTitle = db.Materials.FirstOrDefault(m => m.MaterialID == c.MaterialID)?.Title ?? "N/A",
                        RequestedDate = c.RequestedDate,
                        Status = c.Status
                    });
                }
            }

            ViewBag.SelectedRole = selectedRole;
            return View(model);
        }

        public ActionResult IssuedReport(string fromDate, string toDate, string selectedRole = null)
        {
           
            DateTime? from = string.IsNullOrEmpty(fromDate) ? (DateTime?)null : DateTime.Parse(fromDate);
            DateTime? to = string.IsNullOrEmpty(toDate) ? (DateTime?)null : DateTime.Parse(toDate);

          
            int? schoolId = Session["SchoolID"] as int?;
            int? universityId = Session["UniversityID"] as int?;


            var query = db.Circulations
                          .Where(c => c.Status == "Issued" || c.Status == "Overdue")
                          .Include(c => c.MaterialCopy.Material);
            

            if (schoolId.HasValue)
                query = query.Where(c => c.SchoolID == schoolId.Value);
            else if (universityId.HasValue)
                query = query.Where(c => c.UniversityID == universityId.Value.ToString());

            if (from.HasValue)
                query = query.Where(c => c.IssueDate >= from.Value);
            if (to.HasValue)
                query = query.Where(c => c.IssueDate <= to.Value);

           
            selectedRole = selectedRole ?? "Student";
            string UserID = Session["UserID"]?.ToString();
            
            List<IssuedReportViewModel> model = new List<IssuedReportViewModel>();

            if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                model = (from c in query
                         join emp in db.tblEmployees on c.UserID equals emp.UserID
                         select new IssuedReportViewModel
                         {
                             CirculationID = c.CirculationID,
                             MaterialTitle = c.MaterialCopy.Material.Title,
                             UserID = c.UserID,
                             Name = emp.EmployeeName,
                             IssueDate = c.IssueDate,
                             DueDate = c.DueDate,
                             Status = c.Status
                         }).ToList();
            }
            else if (selectedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                model = (from c in query
                         join stu in db.tblStudents on c.UserID equals stu.UserID
                         select new IssuedReportViewModel
                         {
                             CirculationID = c.CirculationID,
                             MaterialTitle = c.MaterialCopy.Material.Title,
                             UserID = c.UserID,
                             Name = stu.StudentName,
                             IssueDate = c.IssueDate,
                             DueDate = c.DueDate,
                             Status = c.Status
                         }).ToList();
            }

           
            ViewBag.SelectedRole = selectedRole;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;

            return View(model);
        }

        public ActionResult ActiveIssued(string fromDate, string toDate, string selectedRole = null)
        {
            
            DateTime? from = string.IsNullOrEmpty(fromDate) ? (DateTime?)null : DateTime.Parse(fromDate);
            DateTime? to = string.IsNullOrEmpty(toDate) ? (DateTime?)null : DateTime.Parse(toDate);

            
            int? schoolId = Session["SchoolID"] as int?;
            int? universityId = Session["UniversityID"] as int?;

           
            var query = db.Circulations
                          .Where(c => c.Status == "Issued" || c.Status == "Overdue")
                          .Include(c => c.MaterialCopy.Material);

            if (schoolId.HasValue)
                query = query.Where(c => c.SchoolID == schoolId.Value);
            else if (universityId.HasValue)
                query = query.Where(c => c.UniversityID == universityId.Value.ToString());

            if (from.HasValue)
                query = query.Where(c => c.IssueDate >= from.Value);
            if (to.HasValue)
                query = query.Where(c => c.IssueDate <= to.Value);

           
            selectedRole = selectedRole ?? "Student";
            string UserID = Session["UserID"]?.ToString();
            
            List<IssuedReportViewModel> model = new List<IssuedReportViewModel>();

            if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                model = (from c in query
                         join emp in db.tblEmployees on c.UserID equals emp.UserID
                         select new IssuedReportViewModel
                         {
                             CirculationID = c.CirculationID,
                             MaterialTitle = c.MaterialCopy.Material.Title,
                             UserID = c.UserID,
                             Name = emp.EmployeeName,
                             IssueDate = c.IssueDate,
                             DueDate = c.DueDate,
                             Status = c.Status
                         }).ToList();
            }
            else if (selectedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                model = (from c in query
                         join stu in db.tblStudents on UserID equals stu.UserID
                         select new IssuedReportViewModel
                         {
                             CirculationID = c.CirculationID,
                             MaterialTitle = c.MaterialCopy.Material.Title,
                             UserID = c.UserID,
                             Name = stu.StudentName,
                             IssueDate = c.IssueDate,
                             DueDate = c.DueDate,
                             Status = c.Status
                         }).ToList();
            }

           
            ViewBag.SelectedRole = selectedRole;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;

            return View(model);
        }

        public ActionResult FineReport(DateTime? fromDate, DateTime? toDate)
        {
            int? schoolId = Session["SchoolID"] as int?;
            string universityId = Session["UniversityID"] as string;

            if ((schoolId == null || schoolId == 0) && string.IsNullOrEmpty(universityId))
            {
                TempData["Error"] = "Library Category ID not found. Please login again.";
                return RedirectToAction("Login", "Login");
            }

            var finesQuery = from f in db.FineDetails
                             join c in db.Circulations on f.CirculationID equals c.CirculationID into circ
                             from c in circ.DefaultIfEmpty()
                             join m in db.Materials on c.MaterialID equals m.MaterialID into mat
                             from m in mat.DefaultIfEmpty()
                             join p in db.tblStudents on f.UserID equals p.UserID into patron
                             from p in patron.DefaultIfEmpty()
                             select new FineReportViewModel
                             {
                                 FineID = f.FineID,
                                 Name = p != null ? p.StudentName : "N/A",
                                 MaterialTitle = m != null ? m.Title : "N/A",
                                 Amount = f.Amount ?? 0,
                                 Reason = f.Reason,
                                 AppliedDate = f.AppliedDate,
                                 Status = f.Paid == true ? "Paid" : "Unpaid",
                                 SchoolID = f.SchoolID,
                                 UniversityID = f.UniversityID
                             };

            
            finesQuery = finesQuery.Where(f =>
                (schoolId.HasValue && schoolId.Value > 0 && f.SchoolID == schoolId.Value)
                || (!schoolId.HasValue && !string.IsNullOrEmpty(universityId) && f.UniversityID == universityId)
                || (schoolId.HasValue && f.SchoolID == null && f.UniversityID == universityId) // include rows with null SchoolID but same UniversityID
            );

           
            if (fromDate.HasValue)
                finesQuery = finesQuery.Where(f => f.AppliedDate >= fromDate.Value);

            if (toDate.HasValue)
                finesQuery = finesQuery.Where(f => f.AppliedDate <= toDate.Value);

            var fines = finesQuery.OrderByDescending(f => f.AppliedDate).ToList();

            return View(fines);
        }

        public ActionResult AddAuthor()
        {
            int SchoolID = (int)Session["SchoolID"];

            var authors = db.Authors
                .Where(a => a.SchoolID == SchoolID)
                .OrderBy(a => a.Name)
                .ToList();

            return View(authors);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddAuthor(string AuthorName)
        {
            if (string.IsNullOrWhiteSpace(AuthorName))
            {
                TempData["Error"] = "Author name is required.";
                return RedirectToAction("AddAuthor");
            }


            int? schoolId = Session["SchoolID"] as int?;
            string universityId = Session["UniversityID"] as string;

            if ((schoolId == null || schoolId == 0) && string.IsNullOrEmpty(universityId))
            {
                TempData["Error"] = "Library Category ID not found. Please login again.";
                return RedirectToAction("Login", "Login");
            }

            var existingAuthor = db.Authors
                .FirstOrDefault(a => a.Name.Trim().ToLower() == AuthorName.Trim().ToLower()
                                     && a.SchoolID == schoolId);

            if (existingAuthor != null)
            {
                TempData["Error"] = "Author already exists!";
                return RedirectToAction("AddAuthor");
            }


            var author = new Author
            {
                Name = AuthorName.Trim(),
                SchoolID = schoolId,
                IsActive = true
            };

            db.Authors.Add(author);
            db.SaveChanges();

            TempData["Success"] = "Author added successfully!";
            return RedirectToAction("AddAuthor");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateAuthor(int id, string authorName)
        {
            try
            {
                var author = db.Authors.Find(id);
                if (author == null)
                    return Json(new { success = false, message = "Author not found" });

                author.Name = authorName.Trim();
                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ToggleAuthorStatus(int id)
        {
            try
            {
                var author = db.Authors.Find(id);
                if (author == null)
                    return Json(new { success = false, message = "Author not found" });

                
                author.IsActive = author.IsActive == true ? false : true;
                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public ActionResult AddFineReason()
        {
            int SchoolID = (int)Session["SchoolID"];
            var fineReasons = db.FineReasons
                .Where(r => r.SchoolID == SchoolID)
                .ToList();

            return View(fineReasons);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddFineReason(string ReasonText, decimal FineAmount)
        {
            if (string.IsNullOrWhiteSpace(ReasonText))
            {
                ModelState.AddModelError("", "Reason text is required.");
            }

            if (FineAmount <= 0)
            {
                ModelState.AddModelError("", "Fine amount must be greater than zero.");
            }

            int SchoolID = (int)Session["SchoolID"];

            if (!ModelState.IsValid)
            {

                var reasons = db.FineReasons
                    .Where(r => r.SchoolID == SchoolID)
                    .ToList();
                return View(reasons);
            }


            var existingReason = db.FineReasons
                .FirstOrDefault(r => r.Reason.Trim().ToLower() == ReasonText.Trim().ToLower()
                                     && r.SchoolID == SchoolID);

            if (existingReason != null)
            {

                existingReason.FineAmount = FineAmount;
                db.Entry(existingReason).State = EntityState.Modified;
                TempData["Success"] = "Fine reason updated successfully!";
            }
            else
            {

                var fineReason = new FineReason
                {
                    Reason = ReasonText.Trim(),
                    FineAmount = FineAmount,
                    SchoolID = SchoolID
                };

                db.FineReasons.Add(fineReason);
                TempData["Success"] = "Fine reason added successfully!";
            }

            db.SaveChanges();

            return RedirectToAction("AddFineReason");
        }

        public ActionResult AddMaterialType()
        {
            if (Session["UniversityID"] == null)
            {
                return RedirectToAction("Login", "Login");
            }
            string schoolId = Session["UniversityID"].ToString();


            var materialTypes = db.MaterialTypes
                .Where(mt => mt.UniversityID == schoolId)
                .OrderBy(mt => mt.TypeName)
                .ToList();

            return View(materialTypes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddMaterialType(string TypeName)
        {
            if (string.IsNullOrWhiteSpace(TypeName))
            {
                TempData["Error"] = "Material Type Name is required.";
                return RedirectToAction("AddMaterialType");
            }

            int SchoolID = (int)Session["SchoolID"];
            var UniversityID = Session["UniversityID"];

            var exists = db.MaterialTypes
                .Any(mt => mt.TypeName.Trim().ToLower() == TypeName.Trim().ToLower()
                           && mt.SchoolID == SchoolID);

            if (exists)
            {
                TempData["Error"] = "Material Type already exists!";
                return RedirectToAction("AddMaterialType");
            }

            var materialType = new MaterialType
            {
                TypeName = TypeName.Trim(),
                SchoolID = SchoolID,
                IsActive = true,  
                UniversityID = UniversityID.ToString()
            };


            db.MaterialTypes.Add(materialType);
            db.SaveChanges();

            TempData["Success"] = "Material type added successfully!";
            return RedirectToAction("AddMaterialType");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateMaterialType(int id, string typeName)
        {
            try
            {
                string UserID = Session["UserID"]?.ToString();
                var material = db.MaterialTypes.Find(id);
                if (material == null)
                    return Json(new { success = false, message = "Material Type not found" });

                material.TypeName = typeName;
                material.AddedBy = UserID;
                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ToggleMaterialTypeStatus(int id)
        {
            try
            {
                var material = db.MaterialTypes.Find(id);
                if (material == null)
                    return Json(new { success = false, message = "Material Type not found" });

                material.IsActive = material.IsActive == true ? false : true;
                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public ActionResult LowStockMaterials()
        {
            string schoolId = Session["SchoolID"]?.ToString();

            if (string.IsNullOrEmpty(schoolId))
                return RedirectToAction("Login", "Login");

            var materials = db.Materials
                .Include(m => m.Author)
                .Where(m => m.LibraryID.ToString() == schoolId)
                .Select(m => new LowStockMaterialViewModel
                {
                    MaterialID = m.MaterialID,
                    Title = m.Title,
                    Author = m.Author != null ? m.Author.Name : "",
                    Edition = m.Edition,
                    AvailableQty = (int)m.AvailableQuantity,
                    ReorderLevel = m.StockLimit
                })
                .ToList();

            return View(materials);
        }

        [HttpPost]
        public ActionResult UpdateReorderLevel(int materialId, int reorderLevel)
        {
            var material = db.Materials.FirstOrDefault(m => m.MaterialID == materialId);

            if (material == null)
            {
                TempData["Error"] = "Material not found.";
                return RedirectToAction("LowStockMaterials");
            }

            material.StockLimit = reorderLevel;
            db.SaveChanges();

            TempData["Success"] = "Reorder level updated successfully.";

            return RedirectToAction("LowStockMaterials");
        }

        [HttpGet]
        public ActionResult PrintBarcodesByAccountRange()
        {
            var model = new AccountRangePrintViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PrintBarcodesByAccountRange(AccountRangePrintViewModel model)
        {
            // 1️⃣ Validate Input
            if (string.IsNullOrWhiteSpace(model.FromAccountNumber) ||
                string.IsNullOrWhiteSpace(model.ToAccountNumber))
            {
                ModelState.AddModelError("", "Please enter both From and To Account Numbers.");
                return View(model);
            }

            string from = model.FromAccountNumber.Trim();
            string to = model.ToAccountNumber.Trim();

            // 2️⃣ Get Session Values
            var universityId = (Session["UniversityID"] ?? "").ToString();
            int libraryId = Session["SchoolID"] != null ? Convert.ToInt32(Session["SchoolID"]) : 0;

            // 3️⃣ Load Data
            var allCopies = db.MaterialCopies
                .Where(c => c.UniversityID == universityId && c.LibraryID == libraryId)
                .Select(c => new
                {
                    c.CopyID,
                    c.AccountNumber,
                    c.BarcodeNumber
                })
                .ToList();

            bool isNumeric = IsNumericRange(from) && IsNumericRange(to);

            List<MaterialCopyPrintDto> foundCopies;

            // 4️⃣ Filter + Sort (Safe)
            if (isNumeric)
            {
                int fromNum = int.Parse(from);
                int toNum = int.Parse(to);

                if (fromNum > toNum)
                {
                    ModelState.AddModelError("", "From Account Number cannot be greater than To Account Number.");
                    return View(model);
                }

                foundCopies = allCopies
                    .Where(x => int.TryParse(x.AccountNumber, out int num) &&
                                num >= fromNum && num <= toNum)
                    .OrderBy(x => int.Parse(x.AccountNumber))
                    .Select(x => new MaterialCopyPrintDto
                    {
                        MaterialCopyID = x.CopyID,
                        AccountNumber = x.AccountNumber,
                        BarcodeNumber = x.BarcodeNumber
                    })
                    .ToList();
            }
            else
            {
                if (from.Length != to.Length || string.Compare(from, to, StringComparison.Ordinal) > 0)
                {
                    ModelState.AddModelError("", "Invalid account number range.");
                    return View(model);
                }

                foundCopies = allCopies
                    .Where(x => !string.IsNullOrWhiteSpace(x.AccountNumber) &&
                                string.Compare(x.AccountNumber, from, StringComparison.Ordinal) >= 0 &&
                                string.Compare(x.AccountNumber, to, StringComparison.Ordinal) <= 0)
                    .OrderBy(x => x.AccountNumber)
                    .Select(x => new MaterialCopyPrintDto
                    {
                        MaterialCopyID = x.CopyID,
                        AccountNumber = x.AccountNumber,
                        BarcodeNumber = x.BarcodeNumber
                    })
                    .ToList();
            }

            model.FoundCopies = foundCopies;

            // 5️⃣ Find Missing Account Numbers
            var foundSet = new HashSet<string>(foundCopies.Select(f => f.AccountNumber));
            List<string> missing;

            if (isNumeric)
            {
                missing = new List<string>();
                for (int i = int.Parse(from); i <= int.Parse(to); i++)
                {
                    string val = i.ToString();
                    if (!foundSet.Contains(val))
                        missing.Add(val);
                }
            }
            else
            {
                missing = new List<string>();
                string current = from;

                while (string.Compare(current, to, StringComparison.Ordinal) <= 0)
                {
                    if (!foundSet.Contains(current))
                        missing.Add(current);

                    current = GetNextAccountString(current);
                }
            }

            model.MissingAccountNumbers = missing;

            if (!foundCopies.Any())
                ModelState.AddModelError("", "No records found for the selected range.");

            return View(model);
        }

        private bool IsNumericRange(string value)
        {
            return value.All(char.IsDigit);
        }


        private string GetNextAccountString(string input)
        {
            char[] chars = input.ToCharArray();

            for (int i = chars.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(chars[i]))
                {
                    if (chars[i] == '9')
                    {
                        chars[i] = '0';
                    }
                    else
                    {
                        chars[i]++;
                        return new string(chars);
                    }
                }
            }

            return new string(chars);
        }


        public ActionResult BookingList(string selectedRole = null)
        {
            var schoolIDObj = Session["SchoolID"];
            if (schoolIDObj == null)
            {
                TempData["Error"] = "School ID not found. Please login again.";
                return RedirectToAction("Login", "Login");
            }

            if (!int.TryParse(schoolIDObj.ToString(), out int schoolID))
            {
                TempData["Error"] = "Invalid School ID.";
                return RedirectToAction("Login", "Login");
            }

            string currentUserID = Session["UserID"]?.ToString();

            
            selectedRole = selectedRole ?? "Student";

           
            var bookingsQuery = db.Bookinglisteds
                .Where(b => b.SchoolID == schoolID && b.Status == "Pending")
                .Include(b => b.Material)
                .Include(b => b.tblUser);

           
            List<ActiveBookingViewModel> model = new List<ActiveBookingViewModel>();

            if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                model = (from b in bookingsQuery
                         join emp in db.tblEmployees on b.tblUser.UserID equals emp.UserID
                         select new ActiveBookingViewModel
                         {
                             BookingID = b.BookingID,
                             MaterialTitle = b.Material.Title,
                             UserID = b.tblUser.UserID,
                             Name = emp.EmployeeName,
                             BookingDate = b.BookingDate,
                             ExpiryDate = b.ExpiryDate,
                             Status = b.Status
                         }).ToList();
            }
            else if (selectedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                model = (from b in bookingsQuery
                         join stu in db.tblStudents on b.tblUser.UserID equals stu.UserID
                         select new ActiveBookingViewModel
                         {
                             BookingID = b.BookingID,
                             MaterialTitle = b.Material.Title,
                             UserID = b.tblUser.UserID,
                             Name = stu.StudentName,
                             BookingDate = b.BookingDate,
                             ExpiryDate = b.ExpiryDate,
                             Status = b.Status
                         }).ToList();
            }

           
            ViewBag.SelectedRole = selectedRole;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveFromBookingList(int bookingId)
        {
            var booking = db.Bookinglisteds.FirstOrDefault(b => b.BookingID == bookingId);
            if (booking != null && booking.Status == "Pending")
            {
                booking.Status = "Canceled";

                db.SaveChanges();
                TempData["Success"] = "Material Cancelled from your booking list successfully.";
            }
            else
            {
                TempData["Error"] = "Cannot remove this booking.";
            }

            return RedirectToAction("BookingList");
        }

        public ActionResult OverdueList(string selectedRole = "Student")
        {
            if (!int.TryParse(Session["SchoolID"]?.ToString(), out int schoolID))
            {
                TempData["Error"] = "Invalid session. Please login again.";
                return RedirectToAction("Login", "Login");
            }

            string currentUserID = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(currentUserID))
            {
                TempData["Error"] = "Session expired. Please login again.";
                return RedirectToAction("Login", "Login");
            }

            var query = db.Circulations
                .Where(c => c.SchoolID == schoolID && c.Status == "Overdue");

            if (selectedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => c.UserID == currentUserID);
            }

            var rawData = query
                .Include(c => c.Material)
                .Select(c => new
                {
                    c.CirculationID,
                    MaterialTitle = c.Material.Title,
                    c.UserID,
                    c.IssueDate,
                    c.DueDate,
                    c.FineAmount,
                    c.Status
                })
                .ToList();   // SQL stops here

            var result = rawData.Select(x => new OverdueViewModel
            {
                CirculationID = x.CirculationID,
                MaterialTitle = x.MaterialTitle ?? "N/A",
                Name = selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase)
                    ? db.tblEmployees.Where(e => e.UserID == x.UserID).Select(e => e.EmployeeName).FirstOrDefault()
                    : db.tblStudents.Where(s => s.UserID == x.UserID).Select(s => s.StudentName).FirstOrDefault(),
                IssueDate = x.IssueDate,
                DueDate = x.DueDate,
                DaysOverdue = x.DueDate.HasValue
                    ? (DateTime.Today - x.DueDate.Value.Date).Days
                    : 0,
                FineAmount = x.FineAmount ?? 0,
                Status = x.Status
            })
            .OrderByDescending(x => x.DaysOverdue)
            .ToList();

            ViewBag.SelectedRole = selectedRole;
            return View(result);
        }




        public ActionResult BarcodeGeneration()
        {
            return View();
        }

        public ActionResult NewBookRequests(string selectedRole = "Student")
        {
            int? schoolID = Session["SchoolID"] as int?;
            if (!schoolID.HasValue)
            {
                TempData["Error"] = "SchoolID not found. Please login again.";
                return RedirectToAction("Login", "Login");
            }

            
            var requests = db.PatronNewMaterialRequests
                             .Where(r => r.SchoolID == schoolID.Value && r.Status == "Pending")
                             .OrderByDescending(r => r.RequestedDate)
                             .ToList();

            List<NewBookRequestViewModel> filteredRequests = new List<NewBookRequestViewModel>();

            foreach (var r in requests)
            {
                string name = null;

                if (selectedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
                {
                    var student = db.tblStudents.FirstOrDefault(s => s.UserID == r.UserID);
                    if (student != null)
                        name = student.StudentName;
                }
                else if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
                {
                    var employee = db.tblEmployees.FirstOrDefault(e => e.UserID == r.UserID);
                    if (employee != null)
                        name = employee.EmployeeName;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    filteredRequests.Add(new NewBookRequestViewModel
                    {
                        RequestID = r.RequestID,
                        UserID = r.UserID,
                        PatronName = name,
                        MaterialTitle = r.MaterialTitle,
                        RequestedDate = r.RequestedDate,
                        Status = r.Status
                    });
                }
            }

            ViewBag.SelectedRole = selectedRole;

            return View(filteredRequests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult NotifyAvailability(int RequestID, string selectedRole = "Student")
        {
            var request = db.PatronNewMaterialRequests.FirstOrDefault(r => r.RequestID == RequestID);

            if (request == null)
            {
                TempData["Error"] = "Request not found!";
                return RedirectToAction("NewBookRequests");
            }

            string patronName = "Patron";
            string toEmail = "";

            
            if (selectedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                var student = db.tblStudents.FirstOrDefault(s => s.UserID == request.UserID);
                if (student != null)
                {
                    patronName = student.StudentName;
                    toEmail = student.AcademicEmail; 
                }
            }
            else if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                var employee = db.tblEmployees.FirstOrDefault(e => e.UserID == request.UserID);
                if (employee != null)
                {
                    patronName = employee.EmployeeName;
                    toEmail = employee.Email;
                }
            }

            if (string.IsNullOrEmpty(toEmail))
            {
                TempData["Error"] = "User email not found!";
                return RedirectToAction("NewBookRequests");
            }

            try
            {
                string subject = "Book Available Notification";
                string body = $@"
            Dear {patronName},<br/>
            The book '<strong>{request.MaterialTitle}</strong>' you requested is now available in the library.<br/>
            You can collect it at your convenience.<br/><br/>
            Regards,<br/>Library Team.";

                EmailService.SendEmail(toEmail, subject, body);

                
                request.Status = "Notified";
                db.SaveChanges();

                TempData["Success"] = "Email sent successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to send email: " + ex.Message;
            }

            return RedirectToAction("NewBookRequests", new { selectedRole });
        }


        public JsonResult GetMaterialsByTitle(string title)
        {
            var result = (from m in db.Materials
                          join a in db.Authors on m.AuthorID equals a.AuthorID
                          where m.Title.ToLower() == title.ToLower()

                          select new
                          {
                              MaterialID = m.MaterialID,
                              Title = m.Title,
                              Author = a.Name,
                              Year = m.YearPublished,
                              Edition = m.Edition,
                              AvailableBarcode = db.MaterialCopies
                                                    .Where(c => c.MaterialID == m.MaterialID
                                                            && c.Status == "Available")
                                                    .Select(c => c.BarcodeNumber)
                                                    .FirstOrDefault()
                          })
                         .ToList();

            return Json(result, JsonRequestBehavior.AllowGet);
        }



        [HttpGet]
        public ActionResult AddCirculationManual()
        {
            CirculationViewModel model = new CirculationViewModel
            {
                RequestedDate = DateTime.Now,
                IssuedDate = DateTime.Now
            };

            return View(model);
        }

        [HttpPost]
        public JsonResult SaveManualIssue(CirculationViewModel model)
        {
            try
            {
              int  schoolID = 0;

           
                if (Session["SchoolID"] != null)
                    int.TryParse(Session["SchoolID"].ToString(), out schoolID);

              

                string issuedBy = Session["UserID"]?.ToString();
                string universityIdStr = Session["UniversityID"]?.ToString();

            
                string circulationUserID = null;

                if (model.UserType == "Student")
                {
                    var student = db.tblStudents
                        .FirstOrDefault(s => s.EnrollmentNumber == model.UserIdentifier);

                    if (student == null)
                        return Json("No Student found with this Enrollment Number!");

                    circulationUserID = student.UserID;
                }
                else  
                {
                    var faculty = db.tblEmployees
                        .FirstOrDefault(e => e.EmployeeID.ToString() == model.UserIdentifier);

                    if (faculty == null)
                        return Json("No Faculty found with this Employee ID!");

                    circulationUserID = faculty.UserID;
                }

        
                var copy = db.MaterialCopies
                    .FirstOrDefault(c => c.MaterialID == model.MaterialID && c.Status == "Available");

                if (copy == null)
                    return Json("No available copy found!");


                DateTime dueDate;

                if (model.UserType == "Student")
                    dueDate = model.IssuedDate.AddDays(15);     
                else
                    dueDate = model.IssuedDate.AddDays(150);     
         
                decimal fineAmount = 0;

                if (model.Status == "Issued")
                {
                    copy.Status = "Issued";
                }
                else if (model.Status == "Overdue")
                {
                    int overdueDays = model.OverdueDays ?? 0;

                    copy.Status = "Overdue";   // only update copy status

                    // FETCH FINE RATE FROM FineReasons TABLE
                    var overdueFine = db.FineReasons
            .FirstOrDefault(f => f.Reason.ToLower() == "overdue");


                    if (overdueFine != null)
                    {
                        fineAmount = overdueFine.FineAmount * overdueDays;
                    }
                }

                db.SaveChanges();

                Circulation entry = new Circulation()
                {
                    UserID = circulationUserID,

                    RequestedDate = model.RequestedDate,
                    IssueDate = model.IssuedDate,
                    DueDate = dueDate,

                    Status = model.Status,
                    FineAmount = fineAmount,
                  
                    IsOverdue = (model.Status == "Overdue"),
                    LastFineUpdateDate = DateTime.Now,

                    MaterialID = model.MaterialID,
                    CopyID = copy.CopyID,
                    BarcodeNumber = copy.BarcodeNumber,

                    UniversityID = universityIdStr,
                    SchoolID = schoolID,
                    IssuedBy = issuedBy
                };

                db.Circulations.Add(entry);
                db.SaveChanges();


                return Json("Book circulation entry saved successfully!");
            }
            catch (Exception ex)
            {
                return Json("Error: " + ex.Message);
            }
        }




        public JsonResult GetAuthorsByTitle(string title)
        {
            var authors = (from m in db.Materials
                           join a in db.Authors on m.AuthorID equals a.AuthorID
                           where m.Title == title
                           select a.Name).Distinct().ToList();

            return Json(authors, JsonRequestBehavior.AllowGet);
        }

        
        public JsonResult GetEditionsByTitleAuthor(string title, string authorName, int years)
        {
            var editions = (from m in db.Materials
                            join a in db.Authors on m.AuthorID equals a.AuthorID
                            where m.Title == title && m.AuthorID == a.AuthorID && m.YearPublished == years
                            select m.Edition).Distinct().ToList();

            return Json(editions, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetyearsByTitleAuthor(string title, string authorName)
        {
            var years = (from m in db.Materials
                            join a in db.Authors on m.AuthorID equals a.AuthorID
                            where m.Title == title && m.AuthorID == a.AuthorID
                            select m.YearPublished).Distinct().ToList();

            return Json(years, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetAvailableBarcodeExact(string title, string authorName, string edition)
        {
            var material = (from m in db.Materials
                            join a in db.Authors on m.AuthorID equals a.AuthorID
                            where m.Title == title && a.Name == authorName && m.Edition == edition
                            select m).FirstOrDefault();

            if (material == null) return Json(null, JsonRequestBehavior.AllowGet);

            var copy = db.MaterialCopies.FirstOrDefault(c => c.MaterialID == material.MaterialID && c.IsPrinted == false);

            if (copy == null) return Json(null, JsonRequestBehavior.AllowGet);

            Session["MaterialID"] = material.MaterialID;

            return Json(new { MaterialID = material.MaterialID, BarcodeNumber = copy.BarcodeNumber }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetMaterialTitles(string term)
        {
            int? library = Session["SchoolID"] as int?;
            if (!library.HasValue)
            {
                TempData["Error"] = "SchoolID not found. Please login again.";

                return Json(new
                {
                    error = true,
                    message = "SchoolID not found. Please login again."
                }, JsonRequestBehavior.AllowGet);
            }


            var data = db.Materials
                
                         .Where(m =>m.LibraryID== library && m.Title.Contains(term))
                         .Select(m => new { m.MaterialID, m.Title, m.AuthorID, m.Edition })
                         .Take(15)
                         .ToList();

            return Json(data, JsonRequestBehavior.AllowGet);
        }

        public ActionResult AddCounter()
        {
            int schoolID = 0, universityID = 0;
          
            if (Session["SchoolID"] != null)
                int.TryParse(Session["SchoolID"].ToString(), out schoolID);

           
            if (Session["UniversityID"] != null)
                int.TryParse(Session["UniversityID"].ToString(), out universityID);

            var model = new LibraryCounterViewModel
            {
                Counters = db.LibraryCounters
    .Where(c => c.UniversityID == universityID && c.SchoolID == schoolID)
    .Select(c => new LibraryCounterViewModel
    {
        CounterID = c.CounterID,
        CounterNumber = c.CounterNumber,
        EmployeeID = c.EmployeeID,
        EmployeeName = c.EmployeeID != null ?
                db.tblEmployees
                  .Where(e => e.EmployeeID == c.EmployeeID)
                  .Select(e => e.EmployeeName)
                  .FirstOrDefault()
                : "",
        AssignedBy = db.tblEmployees
                  .Where(e => e.UserID == c.AssignedBy)
                  .Select(e => e.EmployeeName)
                  .FirstOrDefault() ?? ""
    }).ToList()

            };

            return View(model);
        }

        public JsonResult GetEmployees(string term)
        {
            
            int? universityId = null;
            int? schoolId = null;

            if (Session["UniversityID"] != null && int.TryParse(Session["UniversityID"].ToString(), out int uniId))
                universityId = uniId;

            if (Session["SchoolID"] != null && int.TryParse(Session["SchoolID"].ToString(), out int schId))
                schoolId = schId;

           
            var query = from emp in db.tblLibraryAssistants
                        join us in db.tblEmployees on emp.AssistantUserID equals us.UserID
                        join  u in db.tblLibraries on emp.LibrarianUserID equals u.LibrarianUserID
                        where emp.UniversityID == (universityId.HasValue ? universityId.Value.ToString() : null)
                        select new
                        {
                            us.EmployeeID,
                            us.EmployeeName,
                            us.UniversityID,
                            u.LibraryName,
                           u.LibraryID
                        };

           
            if (schoolId.HasValue)
            {
               
                query = query.Where(x => x.LibraryID == schoolId.Value);
            }
            else if (universityId.HasValue)
            {
               
                query = query.Where(x => x.UniversityID == universityId.Value.ToString());
            }

            
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(x => x.EmployeeName.Contains(term));
            }

           
            var employees = query
                .OrderBy(x => x.EmployeeName)
                .Select(x => new
                {
                    id = x.EmployeeID,
                    label = x.EmployeeName
                })
                .Distinct()
                .ToList();

            return Json(employees, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddCounter(LibraryCounterViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    string assignedBy = Session["UserID"].ToString();
                    int universityId = Convert.ToInt32(Session["UniversityID"]);
                    int schoolId = Convert.ToInt32(Session["SchoolID"]);

                    var counter = new LibraryCounter
                    {
                        CounterNumber = model.CounterNumber.Trim(),
                        CounterName = model.CounterName.Trim(),
                        EmployeeID = model.EmployeeID,
                        AssignedBy = assignedBy,
                        UniversityID = universityId,
                        SchoolID = schoolId
                    };

                    db.LibraryCounters.Add(counter);
                    db.SaveChanges();

                    TempData["Success"] = "Counter added successfully!";
                    return RedirectToAction("AddCounter");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Error: " + ex.Message;
                }
            }

            return View(model);
        }

        [HttpGet]
        public ActionResult VisitorManagement(DateTime? visitDate)
        {
            var date = visitDate ?? DateTime.Today;

            var visitors = db.VisitorManagements
                             .Where(v => DbFunctions.TruncateTime(v.VisitDate) == date.Date)
                             .OrderByDescending(v => v.VisitDate)
                             .ToList();

            ViewBag.VisitorsByDate = visitors;
            ViewBag.SelectedDate = date;


            string visitorFormUrl = "http://192.168.1.140:44309/Librarian/VisitorForm";
            ViewBag.QRCodeUrl = visitorFormUrl;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VisitorManagement(VisitorManagement model)
        {

            string universityId = Session["UniversityID"].ToString();
            var SchoolID = Session["SchoolID"];
            var userId = Session["UserID"]?.ToString();
            if (ModelState.IsValid)
            {
                model.CreatedBy = userId;
                model.UniversityID = universityId;
                model.SchoolID = (int)SchoolID;
                model.VisitDate = model.VisitDate == DateTime.MinValue ? DateTime.Now : model.VisitDate;
                model.InTime = DateTime.Now;
                model.OutTime = null;

                db.VisitorManagements.Add(model);
                db.SaveChanges();

                TempData["Success"] = "Visitor added successfully!";
                return RedirectToAction("VisitorManagement");
            }

            var today = DateTime.Today;
            ViewBag.VisitorsByDate = db.VisitorManagements
                                       .Where(v => v.UniversityID == model.UniversityID
                                                && v.SchoolID == model.SchoolID
                                                && v.VisitDate >= today
                                                && v.VisitDate < today.AddDays(1))
                                       .OrderByDescending(v => v.VisitDate)
                                       .ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddStudentVisit(string EnrollmentNumber)
        
        {
            Debug.WriteLine("EnrollementNumber is"+ EnrollmentNumber );
            if (string.IsNullOrWhiteSpace(EnrollmentNumber))
            {
                TempData["StudentError"] = "Enrollment number is required.";
                return RedirectToAction("VisitorManagement");
            }

            var student = db.tblStudents.FirstOrDefault(s => s.EnrollmentNumber == EnrollmentNumber);
            if (student == null)
            {
                TempData["StudentError"] = "No student found for this Enrollment Number.";
                return RedirectToAction("VisitorManagement");
            }

           
            string universityId = Session["UniversityID"].ToString();
            var SchoolID = Session["SchoolID"];
            var userId = Session["UserID"]?.ToString();

            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            var existingVisit = db.VisitorManagements
                .FirstOrDefault(v => v.ContactNumber == student.MobileNumber &&
                                     v.VisitDate >= today &&
                                     v.VisitDate < tomorrow);


            if (existingVisit == null)
            {
                
                var newVisit = new VisitorManagement
                {
                    VisitorName = student.StudentName,
                    Purpose = "Library Visit",
                    ContactNumber = student.MobileNumber,
                    VisitDate = DateTime.Now,
                    CreatedBy = userId,
                    UniversityID = universityId,
                    SchoolID = (int)SchoolID,
                    InTime = DateTime.Now
                };

                db.VisitorManagements.Add(newVisit);
                db.SaveChanges();

                TempData["StudentSuccess"] = $"IN recorded for {student.StudentName} ({EnrollmentNumber}).";
            }
            else
            {
               
                existingVisit.OutTime = DateTime.Now; 
                db.SaveChanges();
                TempData["StudentSuccess"] = $"OUT recorded for {student.StudentName} ({EnrollmentNumber}).";
            }

            return RedirectToAction("VisitorManagement");
        }

        public ActionResult PrintVisitorQR()
        {
            string visitorFormUrl = "http://192.168.1.140:44309/Librarian/VisitorForm";

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(visitorFormUrl, QRCodeGenerator.ECCLevel.Q))
            using (QRCode qrCode = new QRCode(qrCodeData))
            using (Bitmap qrBitmap = qrCode.GetGraphic(20))
            using (MemoryStream ms = new MemoryStream())
            {
                qrBitmap.Save(ms, ImageFormat.Png);
                string base64 = Convert.ToBase64String(ms.ToArray());
                ViewBag.QRImage = "data:image/png;base64," + base64;
            }

            return View(); 
        }

        [HttpGet]
        public ActionResult VisitorForm()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VisitorForm(VisitorManagement model)
        {
            if (ModelState.IsValid)
            {
                model.VisitDate = DateTime.Now;
                db.VisitorManagements.Add(model);
                db.SaveChanges();
                TempData["Success"] = "Your visit has been recorded successfully!";
                return RedirectToAction("VisitorForm");
            }
            return View(model);
        }


        [HttpGet]
        public ActionResult WalkInIssue()
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "Login");
            return View(); // WalkInIssue.cshtml
        }

      
        [HttpGet]
        public JsonResult ScanBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return Json(new { Found = false, Message = "Please scan a barcode." }, JsonRequestBehavior.AllowGet);

            var cir = db.Circulations.FirstOrDefault(mc => mc.BarcodeNumber == barcode && mc.Status != "Requested");
            
            if (cir != null)
            {
                return Json(new { Found = false, Message = "The slectyed copy is alreday in the Circulation" }, JsonRequestBehavior.AllowGet);
            }

          
            var copy = db.MaterialCopies
                         .Include(mc => mc.Material)
                         .FirstOrDefault(mc => mc.BarcodeNumber == barcode && mc.Status== "Available");

            if (copy == null || copy.Material == null)
                return Json(new { Found = false, Message = "No material copy found for this barcode." }, JsonRequestBehavior.AllowGet);

            var mat = copy.Material;

            
            int total = mat.TotalQuantity ?? 0;
            int available = mat.AvailableQuantity ?? 0;

            int requested = db.Circulations
                              .Count(c => c.MaterialID == mat.MaterialID && c.Status == "Requested");

            int inCirc = db.Circulations
                           .Count(c => c.MaterialID == mat.MaterialID && (c.Status == "Issued" || c.Status == "Overdue"));

          
            bool canIssue = (available > 0) && (available > requested);

            var result = new WalkInScanResultVM
            {
                Found = true,
                Message = canIssue ? "You can issue this book." : (available == 0 ? "Available quantity is zero. Cannot issue." : "There are pending requests equal/greater than available. Cannot issue now."),
                MaterialID = mat.MaterialID,
                CopyID = copy.CopyID,
                Title = mat.Title,
                BarcodeNumber = copy.BarcodeNumber,
                TotalQuantity = total,
                AvailableQuantity = available,
                RequestedCount = requested,
                InCirculationCount = inCirc,
                CanIssue = canIssue
            };

            return Json(result, JsonRequestBehavior.AllowGet);
        }

       
        [HttpGet]
        public ActionResult IssueForm(int materialId, int copyId, string barcode)
        {
            ModelState.Clear();

            if (Session["UserID"] == null) return RedirectToAction("Login", "Login");

            var universityId = (Session["UniversityID"] ?? "").ToString();
            int schoolId = Session["SchoolID"] != null ? Convert.ToInt32(Session["SchoolID"]) : 0;
            string issuedBy = Session["UserID"]?.ToString();

           
            DateTime issued = DateTime.Now;
            DateTime due = issued.AddDays(14);

            var vm = new WalkInIssueVM
            {
                MaterialID = materialId,
                CopyID = copyId,
                BarcodeNumber = barcode,
                IssuedDate = issued,
                DueDate = due,
                UniversityID = universityId,
                SchoolID = schoolId,
                IssuedBy = issuedBy
            };

            return View(vm); 
        }

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult IssueForm(WalkInIssueVM vm)
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Login");
            string currentUserId = Session["UserID"].ToString();

            string circulationUserID = null;

            if (vm.UserType == "Student")
            {
                var student = db.tblStudents
                    .FirstOrDefault(s => s.EnrollmentNumber == vm.UserIdentifier);

                if (student == null)
                    return Json("No Student found with this Enrollment Number!");

                circulationUserID = student.UserID;
            }

            else
            {
                var faculty = db.tblEmployees
                    .FirstOrDefault(e => e.EmployeeID.ToString() == vm.UserIdentifier);

                if (faculty == null)
                    return Json("No Faculty found with this Employee ID!");

                circulationUserID = faculty.UserID;
            }


            var mat = db.Materials.FirstOrDefault(m => m.MaterialID == vm.MaterialID);
            var copy = db.MaterialCopies.FirstOrDefault(c => c.CopyID == vm.CopyID && c.MaterialID == vm.MaterialID);

            if (mat == null || copy == null)
            {
                TempData["Error"] = "Material or copy not found.";
                return RedirectToAction("WalkInIssue");
            }

           
            bool copyOut = db.Circulations.Any(c => c.CopyID == vm.CopyID && (c.Status == "Issued" || c.Status == "Overdue"));
            if (copyOut)
            {
                TempData["Error"] = "This copy is already issued.";
                return RedirectToAction("WalkInIssue");
            }

          
            int available = mat.AvailableQuantity ?? 0;
            int requested = db.Circulations.Count(c => c.MaterialID == mat.MaterialID && c.Status == "Requested");
            if (!(available > 0 && available > requested))
            {
                TempData["Error"] = "Cannot issue now (no available quantity or pending requests >= available).";
                return RedirectToAction("WalkInIssue");
            }

         
            var circulation = new Circulation
            {
                UserID = circulationUserID,
                MaterialID = vm.MaterialID,
                CopyID = vm.CopyID,
                Status = "Issued",
                IssueDate = vm.IssuedDate,
                DueDate = vm.DueDate,
                RequestedDate = vm.IssuedDate, 
                UniversityID = vm.UniversityID,
                SchoolID = vm.SchoolID,
                IssuedBy = currentUserId,
                BarcodeNumber = vm.BarcodeNumber
            };

            db.Circulations.Add(circulation);

            
            mat.AvailableQuantity = (mat.AvailableQuantity ?? 0) - 1;
            if (mat.AvailableQuantity < 0) mat.AvailableQuantity = 0;

            copy.Status = "Issued";
            db.SaveChanges();

            TempData["Success"] = "Book issued successfully.";
            return RedirectToAction("WalkInIssue");
        }
       


        public ActionResult MyProfile()
        {
            if (Session["UserID"] == null || Session["Role"] == null)
                return RedirectToAction("Login");

            string userId = Session["UserID"].ToString();

            var patron = (from p in db.tblEmployees
                          join u in db.tblUsers on p.UserID equals u.UserID
                          join uu in db.tblUserUniversities on u.UserID equals uu.UserID
                          join uni in db.tblUniversities on uu.UniversityID equals uni.UniversityID
                          where u.UserID == userId
                          select new MyProfileViewModel
                          {
                              UserID = u.UserID,
                              Username = u.Username,
                              Name = p.EmployeeName,
                              Email = p.Email,
                              Phone = p.MobileNumber,
                              Role = u.tblUserRoles.FirstOrDefault().tblRole.RoleName,
                              UniversityName = uni.UniversityName,
                              IsLibrarian = u.tblUserRoles.FirstOrDefault().tblRole.RoleName == "Librarian",

                          }).FirstOrDefault();

            if (patron == null)
                return RedirectToAction("Login");

            return View(patron);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MyProfile(MyProfileViewModel model)
        {
            var currentEmail = Session["UserName"]?.ToString();

            var user = db.tblUsers.FirstOrDefault(u => u.Email == currentEmail);
            var patron = db.tblEmployees.FirstOrDefault(p => p.Email == currentEmail);

            if (user != null && patron != null)
            {

                patron.EmployeeName = model.Name;
                patron.MobileNumber = model.Phone;

                db.SaveChanges();

                Session["UserName"] = model.Username;
                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Could not update profile. Record not found.";
            }

            return RedirectToAction("MyProfile");
        }

        public ActionResult ChangePassword()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login");

            string userid = Session["UserID"].ToString();

            var username = db.tblUsers.FirstOrDefault(u => u.UserID == userid);
            var user = username.Username;
            var vm = new ChangePasswordViewModel
            {
                Username = user,
                Role = Session["Role"]?.ToString()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel vm)
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login");

            string username = Session["UserID"].ToString();
            var user = db.tblUsers.FirstOrDefault(u => u.UserID == username);

            if (user == null)
            {
                vm.ErrorMessage = "User not found.";
                return View(vm);
            }

            string decryptedPassword = SecureHelper.Decrypt(user.PasswordHash);

            if (vm.CurrentPassword != decryptedPassword)
            {
                vm.ErrorMessage = "Current password is incorrect.";
                return View(vm);
            }

            if (vm.NewPassword != vm.ConfirmPassword)
            {
                vm.ErrorMessage = "New password and confirm password do not match.";
                return View(vm);
            }

            user.PasswordHash = SecureHelper.Encrypt(vm.NewPassword);
            db.SaveChanges();

            vm.SuccessMessage = "Password changed successfully!";
            vm.CurrentPassword = vm.NewPassword = vm.ConfirmPassword = string.Empty;


            string role = Session["Role"]?.ToString();
            if (role == "Librarian")
                return RedirectToAction("LibrarianDashboard", "Librarian");
            else
                return RedirectToAction("PatronDashboard", "Patron");
        }

        public ActionResult Index()
        {
            return View();
        }

       
        [HttpPost]
        public ActionResult Generate(string text)
        {
            Debug.WriteLine("text is:" + text);
            if (string.IsNullOrWhiteSpace(text))
                return new HttpStatusCodeResult(400, "Text required");

            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
                using (var qrPng = new PngByteQRCode(qrData))
                {
                    var qrBytes = qrPng.GetGraphic(20); // pixel per module = 20
                    return File(qrBytes, "image/png", "qrcode.png");
                }
            }
        }
        // Add this helper method to the LibrarianController class

  
    }
}
