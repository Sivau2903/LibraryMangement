using LibraryMangement.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using OfficeOpenXml;

using System.Web.Mvc;
using System.Data.Entity.Validation;
using Newtonsoft.Json;

namespace LibraryMangement.Controllers
{
    public class LibrarianController : Controller
    {
        private readonly ICFAISMSEntities db = new ICFAISMSEntities();
        // GET: Librarian
        public ActionResult LibrarianDashboard()
        {
            // Get logged-in librarian's email
            string librarianEmail = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(librarianEmail))
                return RedirectToAction("Login", "Login");

            // Get the librarian's UniversityID and Library_catgeories ID
            var librarian = db.Librarians.Include(x => x.tblUser)
                .Include(x => x.tblUser.tblUserUniversities)
                .Include(x => x.tblUser.tblUserUniversities).Where(x => x.UserID == librarianEmail).FirstOrDefault();
            if (librarian == null)
                return HttpNotFound("Librarian not found");

            int? LibraryCategoryID = librarian.LibraryCategoryID;
            string universityID = librarian.tblUser.tblUserUniversities.FirstOrDefault()?.UniversityID;
            var librarianID = librarian.LibrarianID;

            //Session["LibraryCategoryID"] = LibraryCategoryID;
            Session["UniversityID"] = universityID;
            Session["LibraryCategoryID"] = LibraryCategoryID;
            Session["Librarian"] = librarianID;

            // Group materials by MaterialType
            var materialsByType = db.Materials
                .Where(m => m.Librarycatgeory.LibraryCategoryID == LibraryCategoryID)
                .GroupBy(m => m.MaterialType)
                .Select(g => new MaterialTypeCount
                {
                    MaterialType = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // Prepare dashboard model
            var model = new LibrarianDashboardViewModel
            {
                TotalMaterials = materialsByType.Sum(x => x.Count),
                TotalPatrons = db.Patrons.Count(p => p.UniversityID == universityID.ToString()),

                ActiveIssues = (from c in db.Circulations
                                join mc in db.MaterialCopies on c.CopyID equals mc.CopyID
                                where c.Status == "Issued" && mc.LibraryCategoryID == LibraryCategoryID
                                select c).Count(),

                OverdueIssues = (from c in db.Circulations
                                 join mc in db.MaterialCopies on c.CopyID equals mc.CopyID
                                 where c.Status == "Overdue" && mc.LibraryCategoryID == LibraryCategoryID
                                 select c).Count(),

                PendingReservations = (from c in db.Circulations
                                 join mc in db.MaterialCopies on c.CopyID equals mc.CopyID
                                 where c.Status == "Requested" && mc.LibraryCategoryID == LibraryCategoryID
                                 select c).Count(),

                PendingBookinglist = (from r in db.Bookinglisteds
                                       join mc in db.Materials on r.MaterialID equals mc.MaterialID
                                       where r.Status == "Pending" && mc.LibraryCategoryID == LibraryCategoryID
                                       select r).Count(),

                MaterialsBelowStockLimit = db.Materials.Count(m => m.LibraryCategoryID == LibraryCategoryID && m.AvailableQuantity < 3),

                MaterialsByType = materialsByType
            };

            return View(model);
        }



        // 2. Manage Materials - List All Materials

        //public ActionResult ManageMaterials(string catalogueSearch = "")
        //{

        //    int LibraryCategoryID = Session["LibraryCategoryID"] != null ? (int)Session["LibraryCategoryID"] : 0;

        //    // Include Author to get AuthorName
        //    var materials = db.Materials
        //                      .Include(m => m.Author)
        //                      .Where(m => m.LibraryCategoryID == LibraryCategoryID) // Filter by University
        //                      .AsQueryable();



        //    // Project to view model
        //    var model = materials.Select(m => new MaterialViewModel
        //    {
        //        MaterialID = m.MaterialID,
        //        Title = m.Title,
        //        Author = m.Author != null ? m.Author.Name : "",
        //        Edition = m.Edition,
        //        Description = m.Discription,
        //        Publisher = m.Publisher,
        //        PlaceofPublishers = m.PlaceofPublishers,
        //        YearPublished = (int)m.YearPublished,
        //        Pages = m.Pages ??0,
        //        Vol = m.Vol,
        //        Source = m.Source,
        //        Price = (decimal)m.Price,
        //        ISBN = m.ISBN,
        //        AvailableQuantity = (int)m.AvailableQuantity,
        //        TotalQuantity = (int)m.TotalQuantity,
        //        MaterialType = m.MaterialType
        //    }).ToList();

        //    return View(model);
        //}


        public ActionResult ManageMaterials(string searchField, string searchText, string activeTab = "Simple")
        {
            var loggedInLibrarianId = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(loggedInLibrarianId))
                return RedirectToAction("Login", "Login");

            //var universityId = db.Librarians.Include(x => x.tblUser).Include(x => x.tblUser.tblUserUniversities)
            //                     .Where(l => l.UserID == loggedInLibrarianId)
            //                     .FirstOrDefault();


            var universityID = Session["UniversityID"];
           int LibraryCategoryID = (int)Session["LibraryCategoryID"];

            var model = new List<MaterialViewModel>();

            if (!string.IsNullOrEmpty(searchText))
            {
                searchText = searchText.Trim().ToLower(); // normalize search

                var materials = db.Materials
                                  .Include(m => m.Author)

                                  .Where(m => m.LibraryCategoryID == LibraryCategoryID);

                switch (searchField)
                {
                    case "Title":
                        materials = materials.Where(m => m.Title.ToLower().Contains(searchText));
                        break;
                    case "ISBN":
                        materials = materials.Where(m => m.ISBN.ToLower().Contains(searchText));
                        break;
                    case "Author":
                        materials = materials.Where(m => m.Author != null && m.Author.Name.ToLower().Contains(searchText));
                        break;

                    case "PublisherPlace":
                        materials = materials.Where(m => m.PlaceofPublishers.ToLower().Contains(searchText));
                        break;
                    case "MaterialType":
                        materials = materials.Where(m => m.MaterialType.ToLower().Contains(searchText));
                        break;
                    case "Year":
                        if (int.TryParse(searchText, out int year))
                            materials = materials.Where(m => m.YearPublished == year);
                        break;
                }

                model = materials.Select(m => new MaterialViewModel
                {
                    MaterialID = m.MaterialID,
                    Title = m.Title,
                    Author = m.Author != null ? m.Author.Name : "",
                    Edition = m.Edition,
                    Description = m.Discription,
                    PlaceofPublishers = m.PlaceofPublishers,
                    YearPublished = (int)m.YearPublished,
                    Pages = m.Pages ?? 0,
                    Vol = m.Vol,
                    Source = m.Source,
                    AvailableQuantity = (int)m.AvailableQuantity,
                    TotalQuantity = (int)m.TotalQuantity,
                    MaterialType = m.MaterialType,
                    DepID = m.LibraryCategoryID != null ? m.Librarycatgeory.LibraryCategoryName
                : "N/A"
                }).ToList();
            }

            // Advanced search dropdowns
            ViewBag.KeywordFields = new List<string> { "Title", "Author", "ISBN", "PublisherPlace", "Year", "MaterialType" };
            ViewBag.Library_catgeoriess = db.Librarycatgeories.Where(d => d.UniversityID == universityID.ToString()).ToList();
            ViewBag.ActiveTab = activeTab;

            return View(model);
        }

        [HttpPost]
        public ActionResult AdvancedSearchThreeKeywords(
     string field1, string keyword1,
     string field2, string keyword2,
     string field3, string keyword3,
     string clear)
        {
            var loggedInLibrarianId = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(loggedInLibrarianId))
                return RedirectToAction("Login", "Login");

            var universityId = db.Librarians.Include(x => x.tblUser).Include(x => x.tblUser.tblUserUniversities)
                                 .Where(l => l.UserID == loggedInLibrarianId)
                                 .FirstOrDefault();

            var model = new List<MaterialViewModel>();

            // If Clear button was pressed
            if (!string.IsNullOrEmpty(clear) && clear == "true")
            {
                ViewBag.KeywordFields = new List<string> { "Title", "Author", "ISBN", "PublisherPlace", "Year", "MaterialType" };
                ViewBag.Library_catgeoriess = db.Librarycatgeories.Where(d => d.UniversityID == universityId.ToString()).ToList();
                ViewBag.ActiveTab = "Advanced";
                return View("ManageMaterials", model); // Empty model
            }

            var materials = db.Materials
                              .Include(m => m.Author)
                              .Include(m => m.MaterialCopies)
                              .Where(m => m.UniversityID == universityId.ToString())
                              .AsQueryable();

            keyword1 = keyword1?.Trim();
            keyword2 = keyword2?.Trim();
            keyword3 = keyword3?.Trim();

            materials = materials.Where(m =>
                (string.IsNullOrEmpty(field1) || string.IsNullOrEmpty(keyword1) ||
                    (field1 == "Title" && m.Title == keyword1) ||
                    (field1 == "Author" && m.Author != null && m.Author.Name == keyword1) ||
                    (field1 == "ISBN" && m.ISBN == keyword1) ||
                    (field1 == "PublisherPlace" && m.PlaceofPublishers == keyword1) ||
                    (field1 == "Year" && m.YearPublished.ToString() == keyword1) ||
                    (field1 == "MaterialType" && m.MaterialType == keyword1)
                )
                &&
                (string.IsNullOrEmpty(field2) || string.IsNullOrEmpty(keyword2) ||
                    (field2 == "Title" && m.Title == keyword2) ||
                    (field2 == "Author" && m.Author != null && m.Author.Name == keyword2) ||
                    (field2 == "ISBN" && m.ISBN == keyword2) ||
                    (field2 == "PublisherPlace" && m.PlaceofPublishers == keyword2) ||
                    (field2 == "Year" && m.YearPublished.ToString() == keyword2) ||
                    (field2 == "MaterialType" && m.MaterialType == keyword2)
                )
                &&
                (string.IsNullOrEmpty(field3) || string.IsNullOrEmpty(keyword3) ||
                    (field3 == "Title" && m.Title == keyword3) ||
                    (field3 == "Author" && m.Author != null && m.Author.Name == keyword3) ||
                    (field3 == "ISBN" && m.ISBN == keyword3) ||
                    (field3 == "PublisherPlace" && m.PlaceofPublishers == keyword3) ||
                    (field3 == "Year" && m.YearPublished.ToString() == keyword3) ||
                    (field3 == "MaterialType" && m.MaterialType == keyword3)
                )
            );

            model = materials.Select(m => new MaterialViewModel
            {
                MaterialID = m.MaterialID,
                Title = m.Title,
                Author = m.Author != null ? m.Author.Name : "",
                Edition = m.Edition,
                Description = m.Discription,
                PlaceofPublishers = m.PlaceofPublishers,
                YearPublished = (int)m.YearPublished,
                Pages = m.Pages ?? 0,
                Vol = m.Vol,
                Source = m.Source,
                AvailableQuantity = (int)m.AvailableQuantity,
                TotalQuantity = (int)m.TotalQuantity,
                MaterialType = m.MaterialType,
                DepID = m.Librarycatgeory != null ? m.Librarycatgeory.LibraryCategoryName : "N/A"
            }).ToList();

            ViewBag.KeywordFields = new List<string> { "Title", "Author", "ISBN", "PublisherPlace", "Year", "MaterialType" };
            ViewBag.Library_catgeoriess = db.Librarycatgeories.Where(d => d.UniversityID == universityId.ToString()).ToList();
            ViewBag.ActiveTab = "Advanced";

            return View("ManageMaterials", model);
        }


        public ActionResult AddMaterial()
        {
            var model = new MaterialViewModel
            {
                MaterialTypes = db.MaterialTypes.ToList()  // Load from DB
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
                    // 1️⃣ Save Author first (if not exists)
                    var existingAuthor = db.Authors.FirstOrDefault(a => a.Name == model.Author);
                    if (existingAuthor == null)
                    {
                        existingAuthor = new Author { Name = model.Author };
                        db.Authors.Add(existingAuthor);
                        db.SaveChanges();
                    }

                    // 2️⃣ Get UniversityID of logged-in librarian from session
                    var universityId = Session["UniversityID"];
                    var LibraryCategoryID = Session["LibraryCategoryID"];

                    // 3️⃣ Save Material with AuthorID and UniversityID
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
                        UniversityID = universityId.ToString(), // ✅ Save UniversityID
                        LibraryCategoryID = (int?)LibraryCategoryID,
                        CreatedAt = DateTime.Now
                    };

                    db.Materials.Add(material);
                    db.SaveChanges();

                    //        // 4️⃣ Save default cataloguing fields
                    //        var catalogues = new List<Cataloguing>
                    //{
                    //    new Cataloguing { MaterialID = material.MaterialID, MARCField = "100", MARCData = existingAuthor.Name },
                    //    new Cataloguing { MaterialID = material.MaterialID, MARCField = "245", MARCData = material.Title },
                    //    new Cataloguing { MaterialID = material.MaterialID, MARCField = "260", MARCData = $"{material.Publisher}, {material.YearPublished}" },
                    //    new Cataloguing { MaterialID = material.MaterialID, MARCField = "020", MARCData = material.ISBN }
                    //};
                    //        db.Cataloguings.AddRange(catalogues);

                    // 5️⃣ Auto-generate MaterialCopies
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
                            LibraryCategoryID = (int?)LibraryCategoryID
                        });
                    }

                    db.MaterialCopies.AddRange(copies);

                    // ✅ Save everything
                    db.SaveChanges();

                    return RedirectToAction("ManageMaterials");
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException ex)
                {
                    // Log all validation errors
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

            // Fetch the Author name using AuthorID
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
                Author = authorName,            // Set Author Name here
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

                // Calculate Quantity Difference
                int quantityDifference = (int)(model.TotalQuantity - material.TotalQuantity);

                // Update basic fields
                material.Title = model.Title;
                material.Publisher = model.Publisher;
                material.YearPublished = model.YearPublished;
                material.ISBN = model.ISBN;
                material.AvailableQuantity = model.AvailableQuantity + quantityDifference;  // Adjust AvailableQuantity
                material.TotalQuantity = model.TotalQuantity;
                material.Price = (decimal)model.Price;
                material.Source = model.Source;
                material.Pages = model.Pages;
                material.Vol = model.Vol;
                material.PlaceofPublishers = model.PlaceofPublishers;
                material.Edition = model.Edition;
                material.Discription = model.Description;

                // Update AuthorID instead of free-text Author
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

                // ✅ Smart Copy Management Logic
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




        // GET: BulkUploadMaterials
        public ActionResult BulkUploadMaterials()
        {
            return View(new List<MaterialBulkUploadPreviewModel>());
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
                            //// 🔹 Check if user cancelled
                            if (Response.ClientDisconnectedToken.IsCancellationRequested)
                            {
                                transaction.Rollback();
                                return Json(new { success = false, message = "Saving cancelled by user." });
                            }
                            // Author handling
                            var authorNameNormalized = firstRow.AuthorName?.Trim();
                            var author = db.Authors.FirstOrDefault(a => a.Name.ToLower() == authorNameNormalized.ToLower());
                            if (author == null && !string.IsNullOrWhiteSpace(authorNameNormalized))
                            {
                                author = new Author { Name = authorNameNormalized };
                                db.Authors.Add(author);
                                db.SaveChanges();
                            }

                            var universityId = Session["UniversityID"]?.ToString();
                            var libraryCategoryId = (int)Session["LibraryCategoryID"];

                            var editionNormalized = firstRow.Edition?.Trim().ToLower() ?? "";
                            var isbnNormalized = firstRow.ISBN?.Trim().ToLower() ?? "";
                            var authorId = author?.AuthorID;

                            var existingMaterial = db.Materials.FirstOrDefault(m =>
                                m.Title.ToLower() == firstRow.Title.Trim().ToLower() &&
                                m.AuthorID == authorId &&
                                ((m.Edition ?? "").ToLower() == editionNormalized) &&
                                ((m.ISBN ?? "").ToLower() == isbnNormalized)
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
                                    LibraryCategoryID = libraryCategoryId
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
                                    LibraryCategoryID = libraryCategoryId
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
                                    LibraryCategoryID = libraryCategoryId
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
                        transaction.Rollback();
                        return Json(new { success = false, message = ex.Message, skipped = skippedRows });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }





        // Helper method for logging EF validation errors
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




        // 3. Manage Material Copies - List All Copies
        public ActionResult ManageMaterialCopies()
        {
            // Get logged-in librarian's UniversityID from session
            var LibraryCategoryID = Session["LibraryCategoryID"];

            // Get copies belonging to the university and include Material
            var copies = db.MaterialCopies
                           .Include(mc => mc.Material)
                           .Where(mc => mc.LibraryCategoryID == (int?)LibraryCategoryID)
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




        public ActionResult IssueMaterial()
        {
            return View();
        }

        [HttpGet]
        public JsonResult GetAvailableBarcodes(int materialId)
        {

            try
            {
                var barcodes = db.MaterialCopies
                                 .Where(mc => mc.MaterialID == materialId && mc.Status.Trim().ToLower() == "available")
                                 .Select(mc => mc.BarcodeNumber)
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
                    barcodes = new List<string>()
                }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpGet]
        public JsonResult GetPatronSuggestions(string term)
        {
            var patrons = db.Patrons
                            .Where(p => p.PatronID.ToString().Contains(term))
                            .Select(p => new
                            {
                                id = p.PatronID,
                                text = p.PatronID + " - " + p.PatronName
                            })
                            .Take(10)
                            .ToList();

            return Json(patrons, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public JsonResult ValidatePatronId(string patronId)
        {
            try
            {
                var patron = db.Patrons.FirstOrDefault(p => p.PatronID.ToString() == patronId);

                if (patron != null)
                {
                    // Patron data
                    var patronData = new
                    {
                        PatronID = patron.PatronID,
                        PatronName = patron.PatronName,
                        PatronEmail = patron.PatronEmail,
                        PatronPhone = patron.PatronPhone,
                        PatronType = patron.PatronType
                    };

                    var LibraryCategoryID = Session["LibraryCategoryID"];
                    // Activities
                    var issues = db.Circulations
                                   .Where(c => c.PatronID == patron.PatronID && c.Status == "Issued" && c.LibraryCategoryID == (int?)LibraryCategoryID)
                                   .Select(c => new
                                   {
                                       c.CirculationID,
                                       MaterialTitle = c.Material.Title,
                                       IssueDate = c.IssueDate,
                                       DueDate = c.DueDate,
                                       c.Status
                                   }).ToList();

                    var reserves = db.Circulations
                                     .Where(c => c.PatronID == patron.PatronID && c.Status == "Requested" && c.LibraryCategoryID == (int?)LibraryCategoryID)
                                     .Select(c => new
                                     {
                                         c.CirculationID,
                                        c.MaterialID,
                                         MaterialTitle = c.Material.Title,
                                         RequestDate = c.RequestedDate, // Or reservation request date
                                         c.Status
                                     }).ToList();

              
                    var bookings = db.Bookinglisteds
                                     .Where(b => b.PatronID == patron.PatronID && b.Status == "Pending" && b.LibraryCategoryID == (int?)LibraryCategoryID)
                                     .Select(b => new
                                     {
                                         b.BookingID,
                                         MaterialTitle = b.Material.Title,
                                         b.BookingDate,
                                         b.ExpiryDate,
                                         b.Status
                                     }).ToList();

                    return Json(new
                    {
                        exists = true,
                        patron = patronData,
                        issues,
                        reserves,
                        bookings
                    });
                }

                return Json(new { exists = false });
            }
            catch (Exception ex)
            {
                return Json(new { exists = false, error = ex.Message });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult IssueSelectedReserves(int patronId, List<ReserveIssueModel> selectedReserves)
        {
            try
            {
                foreach (var item in selectedReserves)
                {
                    var circ = db.Circulations.FirstOrDefault(c => c.CirculationID == item.CirculationID);
                    if (circ == null) continue;

                    // Validate barcode exists and is available
                    var copy = db.MaterialCopies
                                 .FirstOrDefault(mc => mc.MaterialID == circ.MaterialID
                                                    && mc.BarcodeNumber == item.Barcode
                                                    && mc.Status == "Available");
                    if (copy == null)
                        return Json(new { success = false, message = $"Invalid barcode for {circ.Material.Title}" });

                    // Update circulation
                    circ.CopyID = copy.CopyID;
                    circ.IssueDate = DateTime.Now;
                    circ.DueDate = DateTime.Now.AddDays(14); // Example: 14-day due period
                    circ.Status = "Issued";
                    circ.BarcodeNumber = item.Barcode;

                    // Update copy status
                    copy.Status = "Issued";

                    // Decrease AvailableQuantity in Materials table
                    var material = db.Materials.FirstOrDefault(m => m.MaterialID == circ.MaterialID);
                    if (material != null && material.AvailableQuantity > 0)
                    {
                        material.AvailableQuantity -= 1;
                    }
                }

                db.SaveChanges();

                // Reload updated issues and reserves
                var issues = db.Circulations.Where(c => c.PatronID == patronId && c.Status == "Issued")
                    .Select(c => new
                    {
                        c.CirculationID,
                        MaterialTitle = c.Material.Title,
                        c.IssueDate,
                        c.DueDate,
                        c.Status
                    }).ToList();

                var reserves = db.Circulations.Where(c => c.PatronID == patronId && c.Status == "Reserved")
                    .Select(c => new
                    {
                        c.CirculationID,
                        MaterialTitle = c.Material.Title,
                        c.RequestedDate,
                        c.Status,
                        c.MaterialID
                    }).ToList();

                return Json(new { success = true, issues, reserves });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        // Model for AJAX
        public class ReserveIssueModel
        {
            public int CirculationID { get; set; }
            public string Barcode { get; set; }
        }




        // GET: ReturnMaterial
        public ActionResult ReturnMaterial()
        {
            var model = new ReturnMaterialViewModel
            {
                BarcodeNumber = string.Empty,
                FineReason = db.FineReasons
                               .Select(f => new FineReasonDTO
                               {
                                   ReasonText = f.Reason,
                                   FinePerDay = f.FineAmount,
                                   Value = f.Reason
                               }).ToList()
            };

            return View(model);
        }
        [HttpGet]
        public JsonResult GetIssuedBarcodes(string term)
        {
            try
            {
                var barcodes = db.Circulations
                    .Where(mc => mc.BarcodeNumber.StartsWith(term))   // match starting letters/numbers
                    .Select(mc => mc.BarcodeNumber)
                    .Take(10)  // limit results for performance
                    .ToList();

                return Json(barcodes, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new List<string> { "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }



        // POST: Fetch Circulation by Barcode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReturnMaterial(string barcodeNumber)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Entered ReturnMaterial POST. Barcode: {barcodeNumber}");

            var model = new ReturnMaterialViewModel
            {
                BarcodeNumber = barcodeNumber,
                FineReason = db.FineReasons
                               .Select(f => new FineReasonDTO
                               {
                                   ReasonText = f.Reason,
                                   FinePerDay = f.FineAmount,
                                   Value = f.Reason
                               }).ToList()
            };

            if (string.IsNullOrWhiteSpace(barcodeNumber))
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Barcode is empty or null.");
                ModelState.AddModelError("", "Please enter a barcode number.");
                return View(model);
            }

            var materialCopy = db.MaterialCopies
                                 .Include(mc => mc.Material)
                                 .FirstOrDefault(mc => mc.BarcodeNumber == barcodeNumber);

            if (materialCopy == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] No MaterialCopy found for Barcode: {barcodeNumber}");
                ModelState.AddModelError("", "Invalid Barcode Number");
                return View(model);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] MaterialCopy found. CopyID: {materialCopy.CopyID}, Title: {materialCopy.Material?.Title}");
            }

            var circulation = db.Circulations
                      .Include(c => c.MaterialCopy)
                      .Include(c => c.MaterialCopy.Material)
                      .Include(c => c.Patron)
                      .FirstOrDefault(c => c.CopyID == materialCopy.CopyID
                                           && (c.Status == "Issued" || c.Status == "Overdue"));

            if (circulation == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] No active circulation found for CopyID: {materialCopy.CopyID} with Status 'Issued'");
                ModelState.AddModelError("", "No active issue found for this barcode.");
                return View(model);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Circulation found. CirculationID: {circulation.CirculationID}, FineAmount: {circulation.FineAmount}");
            }

            // Fine from Circulation table (already calculated by Hangfire)
            decimal overdueFine = circulation.FineAmount ?? 0;
            System.Diagnostics.Debug.WriteLine($"[DEBUG] OverdueFine calculated: {overdueFine}");

            model.CirculationDisplay = new CirculationDisplay
            {
                CirculationID = circulation.CirculationID,
                Title = circulation.MaterialCopy?.Material?.Title ?? "N/A",
                RequestedDate = circulation.RequestedDate,
                IssueDate = circulation.IssueDate,
                DueDate = circulation.DueDate,
                Status = circulation.Status,
                FineAmount = overdueFine
            };

            model.CalculatedFineAmount = overdueFine;

            System.Diagnostics.Debug.WriteLine($"[DEBUG] Model prepared. CirculationDisplay.Title: {model.CirculationDisplay.Title}, FineAmount: {model.CirculationDisplay.FineAmount}");

            return View(model);
        }


        

        // POST: Process Return or Renew
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessReturnRenew(int CirculationID, string action, string FineReason, decimal? fineAmount, string paymentStatus)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] ProcessReturnRenew called: CirculationID={CirculationID}, action={action}, FineReason={FineReason}, fineAmount={fineAmount}, paymentStatus={paymentStatus}");

            var circulation = db.Circulations
                                .Include(c => c.MaterialCopy)
                                .Include(c => c.MaterialCopy.Material)
                                .Include(c => c.Patron)
                                .FirstOrDefault(c => c.CirculationID == CirculationID);

            if (circulation == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Circulation not found for ID={CirculationID}");
                return HttpNotFound();
            }

            System.Diagnostics.Debug.WriteLine($"[DEBUG] Circulation found: Status={circulation.Status}, FineAmount={circulation.FineAmount}");

            int patronId = circulation.PatronID ?? 0;
            var universityId = circulation.UniversityID;
            var LibraryCategoryID = circulation.LibraryCategoryID;

            // Handle Book Lost separately
            if (FineReason == "Book Lost")
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Handling Book Lost case");
                circulation.Status = "BookLost";
                circulation.ReturnDate = DateTime.Now;
                circulation.MaterialCopy.Status = "Lost"; // Do NOT increment AvailableQuantity
            }
            else
            {
                if (action == "Return")
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] Handling Return action");
                    circulation.Status = "Returned";
                    circulation.ReturnDate = DateTime.Now;
                    circulation.MaterialCopy.Status = "Available";
                    if (circulation.MaterialCopy?.Material != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Incrementing AvailableQuantity: Before={circulation.MaterialCopy.Material.AvailableQuantity}");
                        circulation.MaterialCopy.Material.AvailableQuantity += 1;
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] AvailableQuantity After={circulation.MaterialCopy.Material.AvailableQuantity}");
                    }
                }
                else if (action == "Renew")
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] Handling Renew action");
                    circulation.Status = "Renewed";
                    circulation.DueDate = DateTime.Now.AddDays(14); // example renewal period
                }
            }

            // Save fine details if any
            if (!string.IsNullOrEmpty(FineReason) && fineAmount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Saving FineDetail: FineReason={FineReason}, fineAmount={fineAmount}, paymentStatus={paymentStatus}");
                var fineDetail = new FineDetail
                {
                    PatronID = patronId,
                    CirculationID = circulation.CirculationID,
                    Reason = FineReason,
                    Amount = fineAmount,
                    AppliedDate = DateTime.Now,
                    Paid = paymentStatus == "Paid",
                    UniversityID = universityId,
                    // LibraryCategoryID = LibraryCategoryID
                };
                db.FineDetails.Add(fineDetail);

                circulation.FineAmount = fineAmount; // update circulation's total fine
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Circulation FineAmount updated: {circulation.FineAmount}");
            }

            db.SaveChanges();
            System.Diagnostics.Debug.WriteLine("[DEBUG] db.SaveChanges completed successfully");

            TempData["Success"] = "Operation completed successfully!";
            return RedirectToAction("ReturnMaterial");
        }


        // 8. Overdue Reports - Show Overdue Circulations
        public ActionResult OverdueReports(string fromDate, string toDate)
        {
            DateTime? from = string.IsNullOrEmpty(fromDate) ? (DateTime?)null : DateTime.Parse(fromDate);
            DateTime? to = string.IsNullOrEmpty(toDate) ? (DateTime?)null : DateTime.Parse(toDate);

            var LibraryCategoryID = Session["LibraryCategoryID"];

            var query = db.Circulations
                          .Include(c => c.Patron)
                          .Include(c => c.MaterialCopy.Material)
                          .Where(c =>c.LibraryCategoryID == (int)LibraryCategoryID && c.Status == "Overdue");

            if (from.HasValue)
                query = query.Where(c => c.DueDate >= from.Value);

            if (to.HasValue)
                query = query.Where(c => c.DueDate <= to.Value);

            var model = query.ToList();

            return View(model);
        }

        public ActionResult FineReport()
        {
            // Step 1: Get LibraryCategoryID safely from session
            var libraryCategoryIdObj = Session["LibraryCategoryID"];
            if (libraryCategoryIdObj == null)
            {
                TempData["Error"] = "Library Category ID not found. Please login again.";
                return RedirectToAction("Login", "Account");
            }

            string libraryCategoryId = libraryCategoryIdObj.ToString();

            // Step 2: Fetch fines for this librarian category with related data
            //int libraryCategoryId = (int)Session["LibraryCategoryID"];

            var fines = (from f in db.FineDetails
                         join c in db.Circulations on f.CirculationID equals c.CirculationID into circ
                         from c in circ.DefaultIfEmpty()
                         join m in db.Materials on c.MaterialID equals m.MaterialID into mat
                         from m in mat.DefaultIfEmpty()
                         join p in db.Patrons on f.PatronID equals p.PatronID into patron
                         from p in patron.DefaultIfEmpty()
                         where f.LibraryCategoryID == libraryCategoryId.ToString()
                         select new FineReportViewModel
                         {
                             FineID = f.FineID,
                             PatronName = p != null ? p.PatronName : "N/A",
                             MaterialTitle = m != null ? m.Title : "N/A",
                             Amount = f.Amount ?? 0,
                             Reason = f.Reason,
                             AppliedDate = f.AppliedDate,
                             Status = f.Paid == true ? "Paid" : "Unpaid"
                         }).ToList();


            return View(fines);
        }


        public ActionResult AddAuthor()
        {
            var LibraryCategoryID = Session["LibraryCategoryID"];

            // Get all authors for this librarian's category
            var authors = db.Authors
                .Where(a => a.LibraryCategoryID == LibraryCategoryID.ToString())
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

            var LibraryCategoryID = Session["LibraryCategoryID"];

            // Check if author already exists
            var existingAuthor = db.Authors
                .FirstOrDefault(a => a.Name.Trim().ToLower() == AuthorName.Trim().ToLower()
                                     && a.LibraryCategoryID == LibraryCategoryID.ToString());

            if (existingAuthor != null)
            {
                TempData["Error"] = "Author already exists!";
                return RedirectToAction("AddAuthor");
            }

            // Add new author
            var author = new Author
            {
                Name = AuthorName.Trim(),
                LibraryCategoryID = LibraryCategoryID.ToString()
            };

            db.Authors.Add(author);
            db.SaveChanges();

            TempData["Success"] = "Author added successfully!";
            return RedirectToAction("AddAuthor");
        }



        public ActionResult AddFineReason()
        {
            var libraryCategoryID = (Session["LibraryCategoryID"] ?? "").ToString();
            var fineReasons = db.FineReasons
                .Where(r => r.LibraryCategoryID == libraryCategoryID.ToString())
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

            var libraryCategoryID = (Session["LibraryCategoryID"] ?? "").ToString();

            if (!ModelState.IsValid)
            {
                // Reload existing reasons for the view
                var reasons = db.FineReasons
                    .Where(r => r.LibraryCategoryID == libraryCategoryID)
                    .ToList();
                return View(reasons);
            }

            // Check if the reason already exists
            var existingReason = db.FineReasons
                .FirstOrDefault(r => r.Reason.Trim().ToLower() == ReasonText.Trim().ToLower()
                                     && r.LibraryCategoryID == libraryCategoryID);

            if (existingReason != null)
            {
                // Update existing fine amount
                existingReason.FineAmount = FineAmount;
                db.Entry(existingReason).State = EntityState.Modified;
                TempData["Success"] = "Fine reason updated successfully!";
            }
            else
            {
                // Create new reason
                var fineReason = new FineReason
                {
                    Reason = ReasonText.Trim(),
                    FineAmount = FineAmount,
                    LibraryCategoryID = libraryCategoryID
                };

                db.FineReasons.Add(fineReason);
                TempData["Success"] = "Fine reason added successfully!";
            }

            db.SaveChanges();

            return RedirectToAction("AddFineReason");
        }



        public ActionResult AddMaterialType()
        {
            var libraryCategoryId = Session["LibraryCategoryID"]?.ToString();

            // Get all material types for this library category
            var materialTypes = db.MaterialTypes
                .Where(mt => mt.LibraryCategoryID == libraryCategoryId)
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

            var libraryCategoryId = Session["LibraryCategoryID"]?.ToString();

            // Check if the material type already exists
            var exists = db.MaterialTypes
                .Any(mt => mt.TypeName.Trim().ToLower() == TypeName.Trim().ToLower()
                           && mt.LibraryCategoryID == libraryCategoryId);

            if (exists)
            {
                TempData["Error"] = "Material Type already exists!";
                return RedirectToAction("AddMaterialType");
            }

            // Add new material type
            var materialType = new MaterialType
            {
                TypeName = TypeName.Trim(),
                LibraryCategoryID = libraryCategoryId
            };

            db.MaterialTypes.Add(materialType);
            db.SaveChanges();

            TempData["Success"] = "Material type added successfully!";
            return RedirectToAction("AddMaterialType");
        }


        // 9. Reservation Requests - Show All Pending Reservations
        public ActionResult BookingList()
        {
            // Step 1: Get LibraryCategoryID from session safely
            var libraryCategoryIdObj = Session["LibraryCategoryID"];
            if (libraryCategoryIdObj == null)
            {
                TempData["Error"] = "Library Category ID not found. Please login again.";
                return RedirectToAction("Login", "Account");
            }

            if (!int.TryParse(libraryCategoryIdObj.ToString(), out int libraryCategoryId))
            {
                TempData["Error"] = "Invalid Library Category ID.";
                return RedirectToAction("Login", "Account");
            }

            // Step 2: Fetch issued circulations with proper eager loading
            var reservations = db.Bookinglisteds
                .Where(r => r.LibraryCategoryID == libraryCategoryId && r.Status == "Pending")
                .Include(r => r.Material)
                .Include(r => r.Patron)
                .ToList();

            // Step 3: Map to ViewModel for null-safety
            var model = reservations.Select(r => new ActiveBookingViewModel
            {
                BookingID = r.BookingID,
                MaterialTitle = r.Material?.Title ?? "N/A",
                PatronName = r.Patron?.PatronName ?? "N/A",
                BookingDate = r.BookingDate,
                ExpiryDate = r.ExpiryDate,
                Status = r.Status
            }).ToList();

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

        public ActionResult PendingReservations()
        {
            var LibraryCategoryID = Session["LibraryCategoryID"];
            var reservations = db.Circulations
                .Where(r => r.LibraryCategoryID == (int)LibraryCategoryID && r.Status == "Requested")
                .Include(r => r.Material)
                .Include(r => r.Patron)
                .ToList();

            return View(reservations);
        }

        public ActionResult ActiveIssues()
        {
            // Step 1: Get LibraryCategoryID from session safely
            var libraryCategoryIdObj = Session["LibraryCategoryID"];
            if (libraryCategoryIdObj == null)
            {
                TempData["Error"] = "Library Category ID not found. Please login again.";
                return RedirectToAction("Login", "Account");
            }

            if (!int.TryParse(libraryCategoryIdObj.ToString(), out int libraryCategoryId))
            {
                TempData["Error"] = "Invalid Library Category ID.";
                return RedirectToAction("Login", "Account");
            }

            // Step 2: Fetch issued circulations with proper eager loading
            var reservations = db.Circulations
                .Where(r => r.LibraryCategoryID == libraryCategoryId && r.Status == "Issued")
                .Include(r => r.Material)
                .Include(r => r.Patron)
                .ToList();

            // Step 3: Map to ViewModel for null-safety
            var model = reservations.Select(r => new ActiveIssueViewModel
            {
                CirculationID = r.CirculationID,
                MaterialTitle = r.Material?.Title ?? "N/A",
                PatronName = r.Patron?.PatronName ?? "N/A",
                IssueDate = r.IssueDate,
                DueDate = r.DueDate,
                Status = r.Status
            }).ToList();

            return View(model);
        }


        public ActionResult Overduelist()
        {
            var LibraryCategoryID = Session["LibraryCategoryID"];
            var reservations = db.Circulations
                .Where(r => r.LibraryCategoryID == (int)LibraryCategoryID && r.Status == "Overdue")
                .Include(r => r.Material)
                .Include(r => r.Patron)
                .ToList();

            return View(reservations);
        }

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


        public ActionResult AssignRole()
        {
            var universityID = Session["UniversityID"]?.ToString();
            var libraryCategoryID = Session["LibraryCategoryID"]?.ToString();
                return View();
        }

        public ActionResult MyProfile()
        {
            if (Session["UserID"] == null || Session["Role"] == null)
                return RedirectToAction("Login");

            string email = Session["UserName"].ToString();

            var patron = (from p in db.Librarians
                          join u in db.tblUsers on p.EmailID equals u.Email
                          join uni in db.tblUniversities on p.UniversityID equals uni.UniversityID

                          select new MyProfileViewModel
                          {
                              UserID = u.UserID,
                              Username = u.Username,
                              Role = u.tblUserRoles.FirstOrDefault().tblRole.RoleName,
                              //Name = p.,
                              Email = p.EmailID,
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
            var patron = db.Librarians.FirstOrDefault(p => p.EmailID == currentEmail);

            if (user != null && patron != null)
            {

                patron.Name = model.Name;
                patron.ContactNumber = model.Phone;

                db.SaveChanges();

                Session["UserName"] = model.Username; // refresh session
                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Could not update profile. Record not found.";
            }

            return RedirectToAction("MyProfile");
        }

       




        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MyProfileLibrarian(MyProfileViewModel model)
        {
            var currentEmail = Session["UserID"]?.ToString();

            var user = db.tblUsers.FirstOrDefault(u => u.Username == currentEmail);
            var librarian = db.Librarians.FirstOrDefault(l => l.UserID == currentEmail);

            if (user != null && librarian != null)
            {
                // use Username as single source of truth
                user.Username = model.Username;          // new email
                user.Email = model.Username;        // same new email
                //librarian.Name = model.Name;
                //librarian.ContactPhone = model.Phone;

                db.SaveChanges();

                Session["UserID"] = model.Username;    // refresh session
                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Could not update profile. Record not found.";
            }

            return RedirectToAction("MyProfile");
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MyProfilePatron(MyProfileViewModel model)
        {
            var currentEmail = Session["UserID"]?.ToString();

            var user = db.tblUsers.FirstOrDefault(u => u.Username == currentEmail);
            var patron = db.Patrons.FirstOrDefault(p => p.PatronEmail == currentEmail);

            if (user != null && patron != null)
            {
                // use Username as single source of truth
                user.Username = model.Username;          // new email
                patron.PatronEmail = model.Username;     // same new email
                patron.PatronName = model.Name;
                patron.PatronPhone = model.Phone;

                db.SaveChanges();

                Session["UserID"] = model.Username;    // refresh session
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

            // Redirect to dashboard
            string role = Session["Role"]?.ToString();
            if (role == "Librarian")
                return RedirectToAction("LibrarianDashboard", "Librarian");
            else
                return RedirectToAction("PatronDashboard", "Patron");
        }
    }
}
