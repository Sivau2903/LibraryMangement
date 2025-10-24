using LibraryMangement.Models;
using LibraryMangement.Services;
using Newtonsoft.Json;
using OfficeOpenXml;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Controllers
{
    public class LibrarianController : HomeController
    {
        private readonly ICFAISMSEntities db = new ICFAISMSEntities();
        private readonly int? selectedDays;

        public ActionResult LibrarianDashboard()
        {
            
            //string librarianEmail = Session["UserID"]?.ToString();
            //if (string.IsNullOrEmpty(librarianEmail))
            //    return RedirectToAction("Login", "Login");

           
            //var librarian = db.Librarians.Include(x => x.tblUser)
            //    .Include(x => x.tblUser.tblUserUniversities)
            //    .Include(x => x.tblUser.tblUserUniversities).Where(x => x.UserID == librarianEmail).FirstOrDefault();
            //if (librarian == null)
            //    return HttpNotFound("Librarian not found");

            //int? SchoolID = librarian.SchoolID;
            //string universityID = librarian.tblUser.tblUserUniversities.FirstOrDefault()?.UniversityID;
            //var librarianID = librarian.LibrarianID;

            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Login");

            string userId = Session["UserID"].ToString();
            string role = Session["Role"]?.ToString();

            // 2️⃣ Get university & school details from tblUserSchools using UserID
            var userSchool = db.tblUserSchools
                               .FirstOrDefault(u => u.UserID == userId.ToString());

            if (userSchool == null)
                return HttpNotFound("User's University/School information not found.");

            // Store IDs in Session
            Session["Librarian"] = userId;
            Session["UniversityID"] = userSchool.UniversityID;
            Session["SchoolID"] = userSchool.SchoolID;

            var SchoolID = userSchool.SchoolID;
            var universityID = userSchool.UniversityID;

            var materialsByType = db.Materials
               .Where(m => m.SchoolID == SchoolID)

                .GroupBy(m => m.MaterialType)
                .Select(g => new MaterialTypeCount
                {
                    MaterialType = g.Key,
                    Count = g.Count()
                })
                .ToList();


            var model = new LibrarianDashboardViewModel
            {
                TotalMaterials = materialsByType.Sum(x => x.Count),
                TotalPatrons = db.tblUserUniversities.Count(p => p.UniversityID == universityID),
                ActiveIssues = (from c in db.Circulations
                                join mc in db.MaterialCopies on c.CopyID equals mc.CopyID
                                where c.Status == "Issued" && mc.SchoolID == SchoolID
                                select c).Count(),
                OverdueIssues = (from c in db.Circulations
                                 join mc in db.MaterialCopies on c.CopyID equals mc.CopyID
                                 where c.Status == "Overdue" && mc.SchoolID == SchoolID
                                 select c).Count(),
                PendingReservations = (from c in db.Circulations
                                       join mc in db.MaterialCopies on c.CopyID equals mc.CopyID
                                       where c.Status == "Requested" && mc.SchoolID == SchoolID
                                       select c).Count(),
                PendingBookinglist = (from r in db.Bookinglisteds
                                      join mc in db.Materials on r.MaterialID equals mc.MaterialID
                                      where r.Status == "Pending" && mc.SchoolID == SchoolID
                                      select r).Count(),
                MaterialsBelowStockLimit = db.Materials.Count(m => m.SchoolID == SchoolID && m.AvailableQuantity < 3),
                MaterialsByType = materialsByType,
                SelectedDays = selectedDays
            };

            // Calculate Upcoming Overdue only if user entered days
            if (selectedDays.HasValue && selectedDays.Value > 0)
            {
                model.UpcomingOverdueIssues = (from c in db.Circulations
                                               join mc in db.MaterialCopies on c.CopyID equals mc.CopyID
                                               where c.Status == "Issued"
                                                     && mc.SchoolID == SchoolID
                                                     && c.DueDate >= DateTime.Now
                                                     && c.DueDate <= DbFunctions.AddDays(DateTime.Now, selectedDays.Value)
                                               select c).Count();
            }


            return View(model);
        }

        public ActionResult ManageMaterials()
        {
            var loggedInLibrarianId = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(loggedInLibrarianId))
                return RedirectToAction("Login", "Login");

            int SchoolID = (int)Session["SchoolID"];

            var materials = db.Materials
                              .Include(m => m.Author)
                              .Where(m => m.SchoolID == SchoolID)
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
                DepID = m.tblSchool != null ? m.tblSchool.SchoolName : "N/A"
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

                  
                    var universityId = Session["UniversityID"];
                    var SchoolID = Session["SchoolID"];

                    
                    var material = new Material
                    {                                             
                        Title = model.Title,
                        AuthorID = existingAuthor.AuthorID,
                        Edition = model.Edition,
                        Discription = model.Description,
                        Publisher = model.Publisher,
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
                        SchoolID = (int?)SchoolID,
                        CreatedAt = DateTime.Now
                    };

                    db.Materials.Add(material);
                    db.SaveChanges();

                    
                    //        var catalogues = new List<Cataloguing>
                    //{
                    //    new Cataloguing { MaterialID = material.MaterialID, MARCField = "100", MARCData = existingAuthor.Name },
                    //    new Cataloguing { MaterialID = material.MaterialID, MARCField = "245", MARCData = material.Title },
                    //    new Cataloguing { MaterialID = material.MaterialID, MARCField = "260", MARCData = $"{material.Publisher}, {material.YearPublished}" },
                    //    new Cataloguing { MaterialID = material.MaterialID, MARCField = "020", MARCData = material.ISBN }
                    //};
                    //        db.Cataloguings.AddRange(catalogues);

                   
                    var copies = new List<MaterialCopy>();
                    int totalCopies = model.TotalQuantity > 0 ? model.TotalQuantity : 1;
                    for (int i = 1; i <= totalCopies; i++)
                    {
                        string accNo = $"ACCNO{material.MaterialID}{i.ToString("D3")}";
                        string barcode = $"LIB{accNo}";

                        copies.Add(new MaterialCopy
                        {
                            MaterialID = material.MaterialID,
                            AccountNumber = accNo,
                            BarcodeNumber = barcode,
                            CallNumber = string.IsNullOrEmpty(model.CallNumber) ? null : model.CallNumber,
                            Status = "Available",
                            UniversityID = universityId.ToString(),
                            SchoolID = (int?)SchoolID
                        });
                    }

                    db.MaterialCopies.AddRange(copies);

                    
                    db.SaveChanges();
                    TempData["Success"] = "Material added Succesfully";
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
                    for (int i = 0; i < quantityDifference; i++)
                    {
                        var newCopy = new MaterialCopy
                        {
                            MaterialID = material.MaterialID,
                            AccountNumber = GenerateAccountNumber(material.MaterialID, (int)material.TotalQuantity + i + 1),
                            BarcodeNumber = GenerateBarcode(material.MaterialID, (int)material.TotalQuantity + i + 1),
                            CallNumber = model.CallNumber,
                            Status = "Available"
                        };

                        db.MaterialCopies.Add(newCopy);
                    }
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
            return File(filePath, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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
                        foreach (var firstRow in previewData)
                        {
                            
                            if (Response.ClientDisconnectedToken.IsCancellationRequested)
                            {
                                transaction.Rollback();
                                return Json(new { success = false, message = "Saving cancelled by user." });
                            }
                           
                            var authorNameNormalized = firstRow.AuthorName?.Trim();
                            var author = db.Authors.FirstOrDefault(a => a.Name.ToLower() == authorNameNormalized.ToLower());
                            if (author == null && !string.IsNullOrWhiteSpace(authorNameNormalized))
                            {
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
                                ((m.UniversityID ?? "").ToLower() == universityId)&&
                                 ((m.SchoolID == SchoolID))
                            );

                            if (existingMaterial != null)
                            {
                                existingMaterial.TotalQuantity += 1;
                                existingMaterial.AvailableQuantity += 1;
                                db.SaveChanges();

                                int startIndex = db.MaterialCopies.Count(c => c.MaterialID == existingMaterial.MaterialID);
                                var copy = new MaterialCopy
                                {
                                    MaterialID = existingMaterial.MaterialID,
                                    AccountNumber = GenerateAccountNumber(existingMaterial.MaterialID, startIndex + 1),
                                    BarcodeNumber = GenerateBarcode(existingMaterial.MaterialID, startIndex + 1),
                                    CallNumber = firstRow.CallNumber,
                                    Status = "Available",
                                    UniversityID = universityId,
                                    SchoolID = SchoolID
                                };
                                db.MaterialCopies.Add(copy);
                                db.SaveChanges();
                            }
                            else
                            {
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
                                    SchoolID = SchoolID
                                }; 

                                db.Materials.Add(material);
                                db.SaveChanges();

                                var copy = new MaterialCopy
                                {
                                    MaterialID = material.MaterialID,
                                    AccountNumber = GenerateAccountNumber(material.MaterialID, 1),
                                    BarcodeNumber = GenerateBarcode(material.MaterialID, 1),
                                    CallNumber = firstRow.CallNumber,
                                    Status = "Available",
                                    UniversityID = universityId,
                                    SchoolID = SchoolID
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

        private string GenerateBarcode(int materialId, int copyIndex)
        {
            return $"BC-{materialId}-{copyIndex:D6}";
        }

        public ActionResult ManageMaterialCopies()
        {
           
            var SchoolID = Session["SchoolID"];

           
            var copies = db.MaterialCopies
                           .Include(mc => mc.Material)
                           .Where(mc => mc.SchoolID == (int?)SchoolID)
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

            // Get all requested materials
            var requests = db.Circulations
                             .Include(c => c.MaterialCopy.Material)
                             .Where(c => c.Status == "Requested")
                             .ToList();

            // Filter by school or university
            if (schoolId.HasValue)
                requests = requests.Where(c => c.SchoolID == schoolId.Value).ToList();
            else if (universityId.HasValue)
                requests = requests.Where(c => c.UniversityID == universityId.Value.ToString()).ToList();

            // Map to ViewModel with user details based on role
            var result = requests.Select(c =>
            {
                string name = "N/A";
                string email = "N/A";

                if (selectedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
                {
                    var student = db.tblStudents.FirstOrDefault(s => s.UserID == c.UserID);
                    if (student != null)
                    {
                        name = student.StudentName;
                        email = student.AcademicEmail;
                    }
                }
                else if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
                {
                    var employee = db.tblEmployees.FirstOrDefault(e => e.UserID == c.UserID);
                    if (employee != null)
                    {
                        name = employee.EmployeeName;
                        email = employee.Email;
                    }
                }

                return new IssueMaterialViewModel
                {
                    CirculationID = c.CirculationID,
                    MaterialID = c.MaterialCopy?.MaterialID ?? 0,
                    MaterialTitle = c.MaterialCopy?.Material?.Title ?? "N/A",
                    UserID = c.UserID,
                    UserName = name,
                    UserEmail = email,
                    PatronType = selectedRole,
                    RequestedDate = c.RequestedDate,
                    Status = c.Status
                };
            })
            .Where(x => x.UserName != "N/A") // Only include valid users
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
                                     IsPrinted = mc.IsPrinted // Add this column to indicate print status
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


        // POST: /Librarian/MarkBarcodePrinted
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

        //[HttpGet]
        //public JsonResult GetPatronSuggestions(string term)
        //{
        //    var patrons = db.Patrons
        //                    .Where(p => p.PatronID.ToString().Contains(term))
        //                    .Select(p => new
        //                    {
        //                        id = p.PatronID,
        //                        text = p.PatronID + " - " + p.PatronName
        //                    })
        //                    .Take(10)
        //                    .ToList();

        //    return Json(patrons, JsonRequestBehavior.AllowGet);
        //}


        //[HttpPost]
        //public JsonResult ValidatePatronId(string patronId)
        //{
        //    try
        //    {
        //        var patron = db.Patrons.FirstOrDefault(p => p.PatronID.ToString() == patronId);

        //        if (patron != null)
        //        {

        //            var patronData = new
        //            {
        //                PatronID = patron.PatronID,
        //                PatronName = patron.PatronName,
        //                PatronEmail = patron.PatronEmail,
        //                PatronPhone = patron.PatronPhone,
        //                PatronType = patron.PatronType
        //            };

        //            var SchoolID = Session["SchoolID"];

        //            var issues = db.Circulations
        //                           .Where(c => c.PatronID == patron.PatronID && c.Status == "Issued" && c.SchoolID == (int?)SchoolID)
        //                           .Select(c => new
        //                           {
        //                               c.CirculationID,
        //                               MaterialTitle = c.Material.Title,
        //                               IssueDate = c.IssueDate,
        //                               DueDate = c.DueDate,
        //                               c.Status
        //                           }).ToList();

        //            var reserves = db.Circulations
        //                             .Where(c => c.PatronID == patron.PatronID && c.Status == "Requested" && c.SchoolID == (int?)SchoolID)
        //                             .Select(c => new
        //                             {
        //                                 c.CirculationID,
        //                                c.MaterialID,
        //                                 MaterialTitle = c.Material.Title,
        //                                 RequestDate = c.RequestedDate,
        //                                 c.Status
        //                             }).ToList();


        //            var bookings = db.Bookinglisteds
        //                             .Where(b => b.PatronID == patron.PatronID && b.Status == "Pending" && b.SchoolID == (int?)SchoolID)
        //                             .Select(b => new
        //                             {
        //                                 b.BookingID,
        //                                 MaterialTitle = b.Material.Title,
        //                                 b.BookingDate,
        //                                 b.ExpiryDate,
        //                                 b.Status
        //                             }).ToList();

        //            return Json(new
        //            {
        //                exists = true,
        //                patron = patronData,
        //                issues,
        //                reserves,
        //                bookings
        //            });
        //        }

        //        return Json(new { exists = false });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { exists = false, error = ex.Message });
        //    }
        //}


        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult IssueSelectedReserves(string selectedRole, string userId, List<ReserveIssueModel> selectedReserves)
        {
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

                    circ.CopyID = copy.CopyID;
                    circ.IssueDate = DateTime.Now;

                    // ✅ Determine Due Date based on Role
                    if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
                    {
                        circ.DueDate = DateTime.Now.AddDays(200);
                    }
                    else // Student
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

                // Prepare issued and reserved lists for response
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

            // Step 1️⃣: Find circulation record by barcode
            var circulationRecord = db.Circulations
                .FirstOrDefault(c => (c.Status == "Issued" || c.Status == "Overdue" || c.Status == "Renewed")
                                     && c.BarcodeNumber == barcode
                                     && ((schoolId != 0 && c.SchoolID == schoolId)
                                         || (schoolId == 0 && universityId != 0 && c.UniversityID == universityId.ToString())));

            if (circulationRecord == null)
                return Json(new { success = false, message = "No record found" }, JsonRequestBehavior.AllowGet);

            // Step 2️⃣: Get Material title
            var material = db.Materials.FirstOrDefault(m => m.MaterialID == circulationRecord.MaterialID);
            string materialTitle = material?.Title ?? "Unknown Material";

            // Step 3️⃣: Get User role using UserID
            string userId = circulationRecord.UserID;
            string roleName = (from ur in db.tblUserRoles
                               join r in db.tblRoles on ur.RoleID equals r.RoleID
                               where ur.UserID == userId
                               select r.RoleName).FirstOrDefault();

            // Step 4️⃣: Fetch user details based on role
            string patronName = "";
            string patronEmail = "";
            string patronId = "";

            if (!string.IsNullOrEmpty(roleName) && roleName.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                // Fetch from tblStudents
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
                // Fetch from tblEmployee
                var employee = db.tblEmployees.FirstOrDefault(e => e.UserID == userId);
                if (employee != null)
                {
                    patronName = employee.EmployeeName;
                    patronEmail = employee.Email;
                    patronId = employee.EmployeeID.ToString();
                }
            }

            // Step 5️⃣: Prepare response object
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


        //[HttpGet]
        //public JsonResult GetIssuedBarcodes(string term)
        //{
        //    try
        //    {
        //        int? schoolId = Session["SchoolID"] as int?;
        //        var universityId = Session["UniversityID"];

        //        var query = db.Circulations.AsQueryable();

        //        // Apply School or University filter
        //        if (schoolId != null)
        //        {
        //            query = query.Where(c => c.SchoolID == schoolId);
        //        }
        //        else if (universityId != null)
        //        {
        //            query = query.Where(c => c.UniversityID == universityId.ToString());
        //        }

        //        // Apply status and barcode filters
        //        var barcodes = query
        //            .Where(c => (c.Status == "Issued" || c.Status == "Overdue") &&
        //                        c.BarcodeNumber.StartsWith(term))
        //            .Select(c => c.BarcodeNumber)
        //            .Distinct()
        //            .Take(10)
        //            .ToList();

        //        return Json(barcodes, JsonRequestBehavior.AllowGet);
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new List<string> { "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
        //    }
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult ReturnMaterial(string barcodeNumber)
        //{
        //    System.Diagnostics.Debug.WriteLine($"[DEBUG] Entered ReturnMaterial POST. Barcode: {barcodeNumber}");

        //    var model = new ReturnMaterialViewModel
        //    {
        //        BarcodeNumber = barcodeNumber,
        //        FineReason = db.FineReasons
        //                       .Select(f => new FineReasonDTO
        //                       {
        //                           ReasonText = f.Reason,
        //                           FinePerDay = f.FineAmount,
        //                           Value = f.Reason
        //                       }).ToList()
        //    };

        //    if (string.IsNullOrWhiteSpace(barcodeNumber))
        //    {
        //        System.Diagnostics.Debug.WriteLine("[DEBUG] Barcode is empty or null.");
        //        ModelState.AddModelError("", "Please enter a barcode number.");
        //        return View(model);
        //    }

        //    var materialCopy = db.MaterialCopies
        //                         .Include(mc => mc.Material)
        //                         .FirstOrDefault(mc => mc.BarcodeNumber == barcodeNumber);

        //    if (materialCopy == null)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"[DEBUG] No MaterialCopy found for Barcode: {barcodeNumber}");
        //        ModelState.AddModelError("", "Invalid Barcode Number");
        //        return View(model);
        //    }
        //    else
        //    {
        //        System.Diagnostics.Debug.WriteLine($"[DEBUG] MaterialCopy found. CopyID: {materialCopy.CopyID}, Title: {materialCopy.Material?.Title}");
        //    }

        //    var circulation = db.Circulations
        //              .Include(c => c.MaterialCopy)
        //              .Include(c => c.MaterialCopy.Material)
        //              .Include(c => c.Patron)
        //              .FirstOrDefault(c => c.CopyID == materialCopy.CopyID
        //                                   && (c.Status == "Issued" || c.Status == "Overdue"));

        //    if (circulation == null)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"[DEBUG] No active circulation found for CopyID: {materialCopy.CopyID} with Status 'Issued'");
        //        ModelState.AddModelError("", "No active issue found for this barcode.");
        //        return View(model);
        //    }
        //    else
        //    {
        //        System.Diagnostics.Debug.WriteLine($"[DEBUG] Circulation found. CirculationID: {circulation.CirculationID}, FineAmount: {circulation.FineAmount}");
        //    }

        //    // Fine from Circulation table (already calculated by Hangfire)
        //    decimal overdueFine = circulation.FineAmount ?? 0;
        //    System.Diagnostics.Debug.WriteLine($"[DEBUG] OverdueFine calculated: {overdueFine}");

        //    model.CirculationDisplay = new CirculationDisplay
        //    {
        //        CirculationID = circulation.CirculationID,
        //        Title = circulation.MaterialCopy?.Material?.Title ?? "N/A",
        //        RequestedDate = circulation.RequestedDate,
        //        IssueDate = circulation.IssueDate,
        //        DueDate = circulation.DueDate,
        //        Status = circulation.Status,
        //        FineAmount = overdueFine
        //    };

        //    model.CalculatedFineAmount = overdueFine;

        //    System.Diagnostics.Debug.WriteLine($"[DEBUG] Model prepared. CirculationDisplay.Title: {model.CirculationDisplay.Title}, FineAmount: {model.CirculationDisplay.FineAmount}");

        //    return View(model);
        //}

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

            // --- Handle Lost Book ---
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

                    // --- Booking logic: check for pending bookings ---
                    var nextBooking = db.Bookinglisteds
                                        .Where(b => b.MaterialID == circulation.MaterialCopy.MaterialID && b.Status == "Pending")
                                        .OrderBy(b => b.BookingDate) // First come first served
                                        .FirstOrDefault();

                    if (nextBooking != null)
                    {
                        // Assign to next patron
                        nextBooking.Status = "Notified";
                        nextBooking.AssignedDate = DateTime.Now;
                        nextBooking.HoldExpiryDate = DateTime.Now.AddDays(2); // 2-day hold period

                        circulation.MaterialCopy.Status = "OnHold";

                        var patron = db.tblStudents.FirstOrDefault(p => p.UserID == patronId);
                        // Send notification
                        if (!string.IsNullOrWhiteSpace(patron.AcademicEmail))
                        {
                            EmailService.SendBookingAvailableNotification(patron.AcademicEmail, nextBooking);
                        }

                        // Optional: Dashboard Notification (if implemented)
                        // NotificationService.AddNotification(nextBooking.PatronID, $"Book '{circulation.MaterialCopy.Material.Title}' is available for you to collect.");
                    }
                    else
                    {
                        // No pending bookings → mark as available
                        circulation.MaterialCopy.Status = "Available";

                        if (circulation.MaterialCopy?.Material != null)
                        {
                            circulation.MaterialCopy.Material.AvailableQuantity += 1;
                        }
                    }
                }
                else if (action == "Renew")
                {
                    // --- Booking logic: check for pending bookings ---
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

            // --- Fine handling ---
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

            // --- Apply SchoolID or UniversityID filter ---
            int? schoolId = Session["SchoolID"] as int?;
            string universityId = Session["UniversityID"] as string;

            if (schoolId.HasValue)
                query = query.Where(m => m.SchoolID == schoolId.Value);
            else if (!string.IsNullOrEmpty(universityId))
                query = query.Where(m => m.UniversityID == universityId);

            // --- Dropdown for MaterialTypes ---
            ViewBag.MaterialTypes = db.MaterialTypes
                                      .Select(mt => new SelectListItem
                                      {
                                          Value = mt.TypeName,
                                          Text = mt.TypeName
                                      }).ToList();

            // --- AutoComplete Sources ---
            ViewBag.Titles = db.Materials.Select(m => m.Title).Distinct().ToList();
            ViewBag.Authors = db.Authors.Select(a => a.Name).Distinct().ToList();

            // --- Filter logic ---
            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(author) && !string.IsNullOrEmpty(materialType))
            {
                // ✅ Exact match on all three fields (Advanced Search)
                query = query.Where(m =>
                    m.Title == title &&
                    m.Author.Name == author &&
                    m.MaterialType == materialType
                );
            }
            else
            {
                // ✅ Partial match logic for individual filters
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
                query = query.Where(m => m.SchoolID == schoolId.Value);
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

            // Project to a view model with Student Name
            var model = query.ToList().Select(c =>
            {
                string studentName = "N/A";

                var student = db.tblStudents.FirstOrDefault(s => s.UserID == c.UserID);
                if (student != null)
                    studentName = student.StudentName;

                return new
                {
                    c.CirculationID,
                    MaterialTitle = c.MaterialCopy.Material != null ? c.MaterialCopy.Material.Title : "N/A",
                    StudentName = studentName,
                    c.IssueDate,
                    c.DueDate,
                    DaysOverdue = c.DueDate.HasValue ? (DateTime.Now - c.DueDate.Value).Days : 0,
                    FineAmount = c.FineAmount ?? 0,
                    c.Status
                };
            })
            .Where(c => c.StudentName != "N/A") // only include valid students
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

            // Filter by SchoolID or UniversityID
            if (schoolId.HasValue)
                query = query.Where(c => c.SchoolID == schoolId.Value);
            else if (universityId.HasValue)
                query = query.Where(c => c.UniversityID == universityId.Value.ToString());

            // Filter by date range
            if (from.HasValue)
                query = query.Where(c => c.RequestedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(c => c.RequestedDate <= to.Value);

            // Project to a view model with Name based on selected role
            var model = query.ToList().Select(c =>
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

                return new
                {
                    c.CirculationID,
                    c.UserID,
                    UserName = userName,
                    MaterialTitle = c.MaterialCopy.Material != null ? c.MaterialCopy.Material.Title : "N/A",
                    c.RequestedDate,
                    c.Status
                };
            })
            .Where(c => c.UserName != "N/A") // only include valid users
            .ToList();

            ViewBag.SelectedRole = selectedRole;

            return View(model);
        }


        public ActionResult IssuedReport(string fromDate, string toDate, string selectedRole = null)
        {
            // 1️⃣ Parse dates
            DateTime? from = string.IsNullOrEmpty(fromDate) ? (DateTime?)null : DateTime.Parse(fromDate);
            DateTime? to = string.IsNullOrEmpty(toDate) ? (DateTime?)null : DateTime.Parse(toDate);

            // 2️⃣ Get SchoolID and UniversityID from session
            int? schoolId = Session["SchoolID"] as int?;
            int? universityId = Session["UniversityID"] as int?;

            // 3️⃣ Base query for issued circulations
            var query = db.Circulations
                          .Where(c => c.Status == "Issued")
                          .Include(c => c.MaterialCopy.Material);

            if (schoolId.HasValue)
                query = query.Where(c => c.SchoolID == schoolId.Value);
            else if (universityId.HasValue)
                query = query.Where(c => c.UniversityID == universityId.Value.ToString());

            if (from.HasValue)
                query = query.Where(c => c.IssueDate >= from.Value);
            if (to.HasValue)
                query = query.Where(c => c.IssueDate <= to.Value);

            // 4️⃣ Default role
            selectedRole = selectedRole ?? "Student";
            string UserID = Session["UserID"]?.ToString();
            // 5️⃣ Build view model based on selected role
            List<IssuedReportViewModel> model = new List<IssuedReportViewModel>();

            if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                model = (from c in query
                         join emp in db.tblEmployees on UserID equals emp.UserID
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

            // 6️⃣ Pass selected role to view
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

            // ✅ SchoolID / UniversityID filtering
            finesQuery = finesQuery.Where(f =>
                (schoolId.HasValue && schoolId.Value > 0 && f.SchoolID == schoolId.Value)
                || (!schoolId.HasValue && !string.IsNullOrEmpty(universityId) && f.UniversityID == universityId)
                || (schoolId.HasValue && f.SchoolID == null && f.UniversityID == universityId) // include rows with null SchoolID but same UniversityID
            );

            // ✅ Date filter if provided
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

                // Toggle status: assuming IsActive is int (1=Active, 0=Inactive)
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
            if (Session["SchoolID"] == null)
            {
                return RedirectToAction("Login", "Login");
            }
            int schoolId = Convert.ToInt32(Session["SchoolID"]);


            var materialTypes = db.MaterialTypes
                .Where(mt => mt.SchoolID == schoolId)
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
                IsActive = true,  // ✅ saves as 1 in the table
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

            // 1️⃣ Default to Student if role not selected
            selectedRole = selectedRole ?? "Student";

            // 2️⃣ Fetch bookings filtered by school and pending status
            var bookingsQuery = db.Bookinglisteds
                .Where(b => b.SchoolID == schoolID && b.Status == "Pending")
                .Include(b => b.Material)
                .Include(b => b.tblUser);

            // 3️⃣ Fetch personal details based on selected role
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

            // 4️⃣ Pass the selected role to the View for dropdown/selection
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

        public ActionResult OverdueList(string selectedRole = null)
        {
            // 1️⃣ Get SchoolID from session
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
            // 2️⃣ Default role
            selectedRole = selectedRole ?? "Student";

            // 3️⃣ Base query for Overdue circulations
            var circulationsQuery = db.Circulations
                .Where(c => c.SchoolID == schoolID && c.Status == "Overdue")
                .Include(c => c.Material);
                

            // 4️⃣ Build view model dynamically based on selected role
            List<OverdueViewModel> model = new List<OverdueViewModel>();

            if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                model = (from c in circulationsQuery
                         join emp in db.tblEmployees on currentUserID equals emp.UserID
                         select new OverdueViewModel
                         {
                             CirculationID = c.CirculationID,
                             MaterialTitle = c.Material != null ? c.Material.Title : "N/A",
                             
                             Name = emp.EmployeeName,
                             IssueDate = c.IssueDate,
                             DueDate = c.DueDate,
                             DaysOverdue = c.DueDate.HasValue ? (DateTime.Now - c.DueDate.Value).Days : 0,
                             FineAmount = c.FineAmount ?? 0,
                             Status = c.Status
                         }).ToList();
            }
            else if (selectedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                model = (from c in circulationsQuery
                         join stu in db.tblStudents on currentUserID equals stu.UserID
                         select new OverdueViewModel
                         {
                             CirculationID = c.CirculationID,
                             MaterialTitle = c.Material != null ? c.Material.Title : "N/A",
                           
                             Name = stu.StudentName,
                             IssueDate = c.IssueDate,
                             DueDate = c.DueDate,
                             DaysOverdue = c.DueDate.HasValue ? (DateTime.Now - c.DueDate.Value).Days : 0,
                             FineAmount = c.FineAmount ?? 0,
                             Status = c.Status
                         }).ToList();
            }

            // 5️⃣ Pass selected role to the view for dropdown
            ViewBag.SelectedRole = selectedRole;

            return View(model);
        }





        ////public ActionResult PendingReservations()
        ////{
        ////    var SchoolID = Session["SchoolID"];
        ////    var reservations = db.Circulations
        ////        .Where(r => r.SchoolID == (int)SchoolID && r.Status == "Requested")
        ////        .Include(r => r.Material)
        ////        .Include(r => r.Patron)
        ////        .ToList();

        ////    return View(reservations);
        ////}

        //public ActionResult ActiveIssues()
        //{

        //    var SchoolIDObj = Session["SchoolID"];
        //    if (SchoolIDObj == null)
        //    {
        //        TempData["Error"] = "Library Category ID not found. Please login again.";
        //        return RedirectToAction("Login", "Account");
        //    }

        //    if (!int.TryParse(SchoolIDObj.ToString(), out int SchoolID))
        //    {
        //        TempData["Error"] = "Invalid Library Category ID.";
        //        return RedirectToAction("Login", "Account");
        //    }


        //    var reservations = db.Circulations
        //        .Where(r => r.SchoolID == SchoolID && r.Status == "Issued")
        //        .Include(r => r.Material)
        //        .Include(r => r.Patron)
        //        .ToList();

        //    var model = reservations.Select(r => new ActiveIssueViewModel
        //    {
        //        CirculationID = r.CirculationID,
        //        MaterialTitle = r.Material?.Title ?? "N/A",
        //        PatronName = r.Patron?.PatronName ?? "N/A",
        //        IssueDate = r.IssueDate,
        //        DueDate = r.DueDate,
        //        Status = r.Status
        //    }).ToList();

        //    return View(model);
        //}

        //public ActionResult PatronLists()
        //{
        //    var universityID = Session["UniversityID"];
        //    var reservations = db.Patrons

        //        .Where(r => r.UniversityID == universityID.ToString() )

        //        .ToList();

        //    return View(reservations);
        //}



        //10. Barcode Generation - Show Barcode Generation Page
        public ActionResult BarcodeGeneration()
        {
            return View();
        }

        //[HttpGet]
        //public ActionResult AssignRole()
        //{
        //    var userID = Session["UserID"];
        //    var UniversityID = Session["UniversityID"];
        //    if (userID == null)
        //    {
        //        TempData["Error"] = "University ID not found. Please login again.";
        //        return RedirectToAction("Login", "Login");
        //    }


        //    var employees = db.tblEmployees
        //                      .Where(e => e.UserID == (string)userID)
        //                      .Select(e => new
        //                      {
        //                          e.EmployeeID,
        //                          FullName = e.EmployeeName
        //                      })
        //                      .ToList();

        //    if (!employees.Any())
        //    {
        //        TempData["Error"] = "No employees found for this university.";
        //        return RedirectToAction("LibrarianDashboard", "Librarian");
        //    }

        //    var roles = db.tblRoles
        //                  .Where(r => r.UniversityID == (string)UniversityID)
        //                  .ToList();

        //    ViewBag.Employees = new SelectList(employees, "EmployeeID", "FullName");
        //    ViewBag.Roles = new SelectList(roles, "RoleID", "RoleName");

        //    return View();
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult AssignRole(int employeeId, int roleId)
        //{
        //    var userid = Session["UserID"];
        //    var employee = db.tblEmployees.FirstOrDefault(e => e.EmployeeID == employeeId);
        //    if (employee == null)
        //    {
        //        TempData["Error"] = "Employee not found.";
        //        return RedirectToAction("Index", "Employees");
        //    }

        //    var role = db.tblRoles.FirstOrDefault(r => r.RoleID == roleId);
        //    if (role == null)
        //    {
        //        TempData["Error"] = "Invalid role.";
        //        return RedirectToAction("AssignRole", new { employeeId = employeeId });
        //    }


        //    employee.RoleID = role.RoleID;
        //    db.SaveChanges();


        //    var userUni = db.tblUserUniversities
        //                    .FirstOrDefault(u => u.UserID == (string)userid
        //                                     );
        //    if (userUni != null)
        //    {
        //        userUni.UniversityRoleID = role.RoleID;
        //    }


        //    var userRole = db.tblUserRoles.FirstOrDefault(ur => ur.UserID == (string)userid);
        //    if (userRole != null)
        //    {
        //        userRole.RoleID = role.RoleID;
        //    }


        //    //var userType = db.UserTypes.FirstOrDefault(ut => ut.EmployeeID == employee.EmployeeID);
        //    //if (userType != null)
        //    //{
        //    //    userType.RoleName = role.RoleName;
        //    //}

        //    db.SaveChanges();

        //    TempData["Success"] = "Role assigned successfully.";
        //    return RedirectToAction("Index", "Employees");
        //}

        public ActionResult NewBookRequests(string selectedRole = "Student")
        {
            int? schoolID = Session["SchoolID"] as int?;
            if (!schoolID.HasValue)
            {
                TempData["Error"] = "SchoolID not found. Please login again.";
                return RedirectToAction("Login", "Login");
            }

            // Get all pending requests for the school
            var requests = db.PatronNewMaterialRequests
                             .Where(r => r.SchoolID == schoolID.Value && r.Status == "Pending")
                             .OrderByDescending(r => r.RequestedDate)
                             .ToList();

            // Prepare a list with Patron Name based on Role
            var filteredRequests = requests.Select(r =>
            {
                string name = "N/A";

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

                return new
                {
                    r.RequestID,
                    r.UserID,
                    PatronName = name,
                    r.MaterialTitle,
                    r.RequestedDate,
                    r.Status
                };
            })
            .Where(r => r.PatronName != "N/A") // only include valid users
            .ToList();

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

            // Determine the role and get Name & Email
            if (selectedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                var student = db.tblStudents.FirstOrDefault(s => s.UserID == request.UserID);
                if (student != null)
                {
                    patronName = student.StudentName;
                    toEmail = student.AcademicEmail; // assuming tblStudents has EmailID
                }
            }
            else if (selectedRole.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                var employee = db.tblEmployees.FirstOrDefault(e => e.UserID == request.UserID);
                if (employee != null)
                {
                    patronName = employee.EmployeeName;
                    toEmail = employee.Email; // assuming tblEmployees has EmailID
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

                // Update request status
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


        [HttpGet]
        public ActionResult AddCirculationManual()
        {
            return View(new CirculationViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddCirculationManual(CirculationViewModel model)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Author: {model.AuthorName}, Edition: {model.Edition}");

                if (ModelState.IsValid)
                {
                    int userID = 0, schoolID = 0, universityID = 0;

                    // 🟩 Safely parse UserID
                    if (Session["UserID"] != null)
                        int.TryParse(Session["UserID"].ToString(), out userID);

                    // 🟩 Safely parse SchoolID
                    if (Session["SchoolID"] != null)
                        int.TryParse(Session["SchoolID"].ToString(), out schoolID);

                    // 🟩 Safely parse UniversityID
                    if (Session["UniversityID"] != null)
                        int.TryParse(Session["UniversityID"].ToString(), out universityID);

                    // Get Author
                    var author = db.Authors.FirstOrDefault(a => a.Name == model.AuthorName);
                    if (author == null)
                    {
                        TempData["Error"] = "Author not found!";
                        return RedirectToAction("AddCirculationManual");
                    }

                    // Get Material
                    var material = db.Materials.FirstOrDefault(m =>
                        m.Title == model.Title &&
                        m.AuthorID == author.AuthorID &&
                        m.Edition == model.Edition);

                    if (material == null)
                    {
                        TempData["Error"] = "Material not found with exact Title, Author, and Edition!";
                        return RedirectToAction("AddCirculationManual");
                    }

                    // Get unprinted copy
                    var copy = db.MaterialCopies.FirstOrDefault(c => c.MaterialID == material.MaterialID && c.IsPrinted == false);
                    if (copy == null)
                    {
                        TempData["Error"] = "No unprinted copy available for this material!";
                        return RedirectToAction("AddCirculationManual");
                    }

                    // Mark copy as printed
                    copy.IsPrinted = true;
                    copy.Status = "Issued";
                    db.SaveChanges();

                    // Save circulation entry
                    var circulation = new Circulation
                    {
                        UserID = model.UserID,
                        RequestedDate = model.RequestedDate,
                        IssueDate = model.IssuedDate,
                        DueDate = model.RequestedDate.AddDays(15),
                        Status = "Issued",
                        MaterialID = material.MaterialID,
                        CopyID = copy.CopyID,
                        BarcodeNumber = copy.BarcodeNumber,
                        UniversityID = universityID.ToString(),
                        SchoolID = schoolID,
                        IssuedBy = userID,
                    };

                    db.Circulations.Add(circulation);
                    db.SaveChanges();

                    TempData["Success"] = "Book circulation entry added successfully!";
                    return RedirectToAction("AddCirculationManual");
                }

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR in AddCirculationManual:");
                System.Diagnostics.Debug.WriteLine(ex.Message);
                if (ex.InnerException != null)
                    System.Diagnostics.Debug.WriteLine("Inner Exception: " + ex.InnerException.Message);

                TempData["Error"] = "An error occurred: " + ex.Message;
                return RedirectToAction("AddCirculationManual");
            }
        }

        // Get unprinted barcode by exact match
        // Get all authors for a selected Title
        public JsonResult GetAuthorsByTitle(string title)
        {
            var authors = (from m in db.Materials
                           join a in db.Authors on m.AuthorID equals a.AuthorID
                           where m.Title == title
                           select a.Name).Distinct().ToList();

            return Json(authors, JsonRequestBehavior.AllowGet);
        }

        // Get all editions for selected Title + Author
        public JsonResult GetEditionsByTitleAuthor(string title, string authorName)
        {
            var editions = (from m in db.Materials
                            join a in db.Authors on m.AuthorID equals a.AuthorID
                            where m.Title == title && m.AuthorID == a.AuthorID
                            select m.Edition).Distinct().ToList();

            return Json(editions, JsonRequestBehavior.AllowGet);
        }

        // Get available barcode for exact Title + Author + Edition
        public JsonResult GetAvailableBarcodeExact(string title, string authorName, string edition)
        {
            var material = (from m in db.Materials
                            join a in db.Authors on m.AuthorID equals a.AuthorID
                            where m.Title == title && a.Name == authorName && m.Edition == edition
                            select m).FirstOrDefault();

            if (material == null) return Json(null, JsonRequestBehavior.AllowGet);

            var copy = db.MaterialCopies.FirstOrDefault(c => c.MaterialID == material.MaterialID && c.IsPrinted == false);

            if (copy == null) return Json(null, JsonRequestBehavior.AllowGet);

            return Json(new { MaterialID = material.MaterialID, BarcodeNumber = copy.BarcodeNumber }, JsonRequestBehavior.AllowGet);
        }



        // Autocomplete: Material Titles
        public JsonResult GetMaterialTitles(string term)
        {
            var data = db.Materials
                         .Where(m => m.Title.Contains(term))
                         .Select(m => new { m.MaterialID, m.Title ,m.AuthorID,m.Edition})
                         //.Take(1)
                         .ToList();

            return Json(data, JsonRequestBehavior.AllowGet);
        }

        // GET: Librarian/AddCounter
        public ActionResult AddCounter()
        {
            int schoolID = 0, universityID = 0;
            // 🟩 Safely parse SchoolID
            if (Session["SchoolID"] != null)
                int.TryParse(Session["SchoolID"].ToString(), out schoolID);

            // 🟩 Safely parse UniversityID
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


        // For Employee Autocomplete
        public JsonResult GetEmployees(string term)
        {
            // Safely extract numeric UniversityID and SchoolID from session
            int? universityId = null;
            int? schoolId = null;

            if (Session["UniversityID"] != null && int.TryParse(Session["UniversityID"].ToString(), out int uniId))
                universityId = uniId;

            if (Session["SchoolID"] != null && int.TryParse(Session["SchoolID"].ToString(), out int schId))
                schoolId = schId;

            // Join Employees with UserSchools on UserID
            var query = from emp in db.tblEmployees
                        join us in db.tblUserSchools on emp.UserID equals us.UserID
                        select new
                        {
                            emp.EmployeeID,
                            emp.EmployeeName,
                            us.UniversityID,
                            us.SchoolID
                        };

            // Filter by logged-in user's scope
            if (schoolId.HasValue)
            {
                // If the user belongs to a specific school → show only that school's employees
                query = query.Where(x => x.SchoolID == schoolId.Value);
            }
            else if (universityId.HasValue)
            {
                // If the user belongs to a university → show all employees under that university
                query = query.Where(x => x.UniversityID == universityId.Value.ToString());
            }

            // Apply search term (case-insensitive)
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(x => x.EmployeeName.Contains(term));
            }

            // Prepare final autocomplete result
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


      
        // GET
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
            if (ModelState.IsValid)
            {
                model.CreatedBy = Convert.ToInt32(Session["UserID"]);
                model.UniversityID = Convert.ToInt32(Session["UniversityID"]);
                model.SchoolID = Convert.ToInt32(Session["SchoolID"]);
                model.VisitDate = model.VisitDate == DateTime.MinValue ? DateTime.Now : model.VisitDate;

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

            return View(); // Will use PrintVisitorQR.cshtml
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




        public ActionResult MyProfile()
        {
            if (Session["UserID"] == null || Session["Role"] == null)
                return RedirectToAction("Login");

            string email = Session["UserName"].ToString();
            string UserID = Session["UserID"].ToString();
            var UniversityID = Session["UniversityID"].ToString();

            var patron = (from p in db.tblEmployees
                          join u in db.tblUsers on p.UserID equals UserID
                          join uni in db.tblUniversities on UniversityID equals uni.UniversityID

                          select new MyProfileViewModel
                          {
                              UserID = u.UserID,
                              Username = u.Username,
                              Role = u.tblUserRoles.FirstOrDefault().tblRole.RoleName,
                              //Name = p.,
                              Email = p.Email,
                              //Phone = p.PatronPhone,
                              UniversityName = uni.UniversityName,
                              IsLibrarian = false
                          }).FirstOrDefault();

            if (patron == null)
                return RedirectToAction("Login", "Login");


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

       




        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult MyProfileLibrarian(MyProfileViewModel model)
        //{
        //    var currentEmail = Session["UserID"]?.ToString();

        //    var user = db.tblUsers.FirstOrDefault(u => u.Username == currentEmail);
        //    var librarian = db.tblEmployees.FirstOrDefault(l => l.UserID == currentEmail);

        //    if (user != null && librarian != null)
        //    {
              
        //        user.Username = model.Username;         
        //        user.Email = model.Username;        
        //        //librarian.Name = model.Name;
        //        //librarian.ContactPhone = model.Phone;

        //        db.SaveChanges();

        //        Session["UserID"] = model.Username;   
        //        TempData["SuccessMessage"] = "Profile updated successfully!";
        //    }
        //    else
        //    {
        //        TempData["ErrorMessage"] = "Could not update profile. Record not found.";
        //    }

        //    return RedirectToAction("MyProfile");
        //}


        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult MyProfilePatron(MyProfileViewModel model)
        //{
        //    var currentEmail = Session["UserID"]?.ToString();

        //    var user = db.tblUsers.FirstOrDefault(u => u.Username == currentEmail);
        //    var patron = db.Patrons.FirstOrDefault(p => p.PatronEmail == currentEmail);

        //    if (user != null && patron != null)
        //    {
                
        //        user.Username = model.Username;         
        //        patron.PatronEmail = model.Username;    
        //        patron.PatronName = model.Name;
        //        patron.PatronPhone = model.Phone;

        //        db.SaveChanges();

        //        Session["UserID"] = model.Username;    
        //        TempData["SuccessMessage"] = "Profile updated successfully!";
        //    }
        //    else
        //    {
        //        TempData["ErrorMessage"] = "Could not update profile. Record not found.";
        //    }

        //    return RedirectToAction("MyProfile");
        //}



        public ActionResult ChangePassword()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login");

            var vm = new ChangePasswordViewModel
            {
                Username = Session["UserID"].ToString(),
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
            var user = db.tblUsers.FirstOrDefault(u => u.Username == username);

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
    }
}
