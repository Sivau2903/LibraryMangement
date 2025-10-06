using LibraryMangement.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Controllers
{
    public class PatronController : HomeController
    {
        private readonly ICFAISMSEntities db = new ICFAISMSEntities();
        // GET: Patron
        public ActionResult PatronDashboard()
        {
            var loggedInEmail = Session["UserID"].ToString();
            if (string.IsNullOrEmpty(loggedInEmail))
                return RedirectToAction("Login", "Login");

            var patron = db.Patrons.Include(x => x.tblUser)
                .Include(x => x.tblUser.tblUserUniversities)
                .Include(x => x.tblUser.tblUserUniversities).Where(x => x.UserID == loggedInEmail).FirstOrDefault();
            if (patron == null)
                return HttpNotFound("Librarian not found");

            if (patron == null)
                return HttpNotFound();

            int patronId = patron.PatronID;
            string universityID = patron.tblUser.tblUserUniversities.FirstOrDefault()?.UniversityID;
            Session["PatronID"] = patron.PatronID;
            Session["UniversityID"] = patron.UniversityID;
            Session["schoolID"] = patron.SchoolID;



			var model = new PatronDashboardViewModel
            {
                PatronID = patron.PatronID,
                PatronName = patron.PatronName,
                ActiveIssuedCount = db.Circulations.Count(c => c.PatronID == patronId && c.Status == "Issued"),
                OverdueCount = db.Circulations.Count(c => c.PatronID == patronId && c.Status == "Overdue"),
                PendingReservations = db.Circulations.Count(r => r.PatronID == patronId && r.Status == "Requested"),
                PendingBookings = db.Bookinglisteds.Count(s => s.PatronID == patronId && s.Status == "Pending"),
                ActiveIssues = db.Circulations
                    .Where(c => c.PatronID == patronId && c.Status == "Issued" || c.Status == "Overdue")
                  .Include("MaterialCopy.Material")

                    .ToList(),
                
            };

            return View(model);
        }


        // GET: Manage Materials - Simple Search
        public ActionResult ManageMaterials(string searchField, string searchText, string activeTab = "Simple")
        {
            var loggedInLibrarianId = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(loggedInLibrarianId))
                return RedirectToAction("Login", "Login");

            //var universityId = db.Patrons
            //                    .Where(l => l.PatronEmail == loggedInLibrarianId)
            //                     .Select(l => l.UniversityID)
            //                     .FirstOrDefault();
            int schoolID = (int)Session["schoolID"];
            var model = new List<MaterialViewModel>();

            if (!string.IsNullOrEmpty(searchText))
            {
                searchText = searchText.Trim().ToLower(); 

                var materials = db.Materials
                                  .Include(m => m.Author)
                                  .Include(m => m.tblSchool)
                                  .Where(m => m.SchoolID == schoolID);

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
                    DepID = m.tblSchool != null ? m.tblSchool.SchoolName : "N/A"
                }).ToList();
            }

            // Advanced search dropdowns
            ViewBag.KeywordFields = new List<string> { "Title", "Author", "ISBN", "PublisherPlace", "Year", "MaterialType" };
            ViewBag.Departments = db.tblSchools.Where(d => d.SchoolID == schoolID).ToList();
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

            int schoolID = (int)Session["schoolID"];  

            var model = new List<MaterialViewModel>();

            // If Clear button was pressed
            if (!string.IsNullOrEmpty(clear) && clear == "true")
            {
                ViewBag.KeywordFields = new List<string> { "Title", "Author", "ISBN", "PublisherPlace", "Year", "MaterialType" };
                ViewBag.Departments = db.tblSchools.Where(d => d.SchoolID == schoolID).ToList();
                ViewBag.ActiveTab = "Advanced";
                return View("ManageMaterials", model); 
            }

            var materials = db.Materials
                              .Include(m => m.Author)
                              .Include(m => m.tblSchool)
                              .Where(m => m.SchoolID == schoolID)
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
                DepID = m.tblSchool != null
                ? m.tblSchool.SchoolName
                : "N/A"
            }).ToList();

            ViewBag.KeywordFields = new List<string> { "Title", "Author", "ISBN", "PublisherPlace", "Year", "MaterialType" };
            ViewBag.Departments = db.tblSchools.Where(d => d.SchoolID == schoolID).ToList();
            ViewBag.ActiveTab = "Advanced";

            return View("ManageMaterials", model);
        }


        //private IQueryable<Material> ApplyExactMatch(IQueryable<Material> query, string field, string keyword)
        //{
        //    keyword = keyword.Trim();

        //    switch (field)
        //    {
        //        case "Title":
        //            return query.Where(m => m.Title == keyword);
        //        case "Author":
        //            return query.Where(m => m.Author != null && m.Author.Name == keyword);
        //        case "ISBN":
        //            return query.Where(m => m.ISBN == keyword);
        //        case "PublisherPlace":
        //            return query.Where(m => m.PlaceofPublishers == keyword);
        //        case "Year":
        //            if (int.TryParse(keyword, out int year))
        //                return query.Where(m => m.YearPublished == year);
        //            return query.Where(m => false);  // invalid year, no match
        //        case "MaterialType":
        //            return query.Where(m => m.MaterialType == keyword);
        //        default:
        //            return query;
        //    }
        //}




        //// Helper method to check exact match for a field
        //private bool MatchesField(Material m, string field, string keyword)
        //{
        //    switch (field)
        //    {
        //        case "Title":
        //            return string.Equals(m.Title, keyword, StringComparison.OrdinalIgnoreCase);
        //        case "Author":
        //            return m.Author != null && string.Equals(m.Author.Name, keyword, StringComparison.OrdinalIgnoreCase);
        //        case "ISBN":
        //            return string.Equals(m.ISBN, keyword, StringComparison.OrdinalIgnoreCase);
        //        case "PublisherPlace":
        //            return string.Equals(m.PlaceofPublishers, keyword, StringComparison.OrdinalIgnoreCase);
        //        case "Year":
        //            return int.TryParse(keyword, out int y) && m.YearPublished == y;
        //        case "MaterialType":
        //            return string.Equals(m.MaterialType, keyword, StringComparison.OrdinalIgnoreCase);
        //        default:
        //            return false;
        //    }
        //}


        public ActionResult ReserveSingle(int materialId)
        {
            var model = db.Materials
                          .Include(m => m.tblSchool)
                          .Where(m => m.MaterialID == materialId)
                          .Select(m => new MaterialViewModel
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
                              DepID = m.tblSchool != null ? m.tblSchool.SchoolName : "N/A",
                              tblSchoolName = m.tblSchool != null ? m.tblSchool.SchoolName : "",
                          })
                          .FirstOrDefault();

            return View("ReservationConfirmation", new List<MaterialViewModel> { model });
        }

        [HttpPost]
        public ActionResult ReserveMultiple(int[] selectedMaterialIds)
        {
            var universityId = db.Patrons
                                 .Where(p => p.PatronEmail == Session["UserID"].ToString())
                                 .Select(p => p.UniversityID)
                                 .FirstOrDefault();

            var models = db.Materials
                           .Include(m => m.tblSchool)
                           .Where(m => selectedMaterialIds.Contains(m.MaterialID))
                           .Select(m => new MaterialViewModel
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
                               DepID = m.tblSchool != null ? m.tblSchool.SchoolName : "N/A",
                               SchoolID = m.SchoolID
                           })
                           .ToList();
            return View("ReservationConfirmation", models);
        }

        [HttpPost]
        public ActionResult ConfirmReservation(int[] materialIds)
        {
            int patronId = (int)(Session["PatronID"] ?? 0);
            string loggedInUserEmail = Session["UserID"]?.ToString();

            if (string.IsNullOrEmpty(loggedInUserEmail))
                return RedirectToAction("Login", "Login");

            var universityId = Session["UniversityID"];
            int schoolID = (int)Session["schoolID"];

			foreach (var materialId in materialIds)
            {
                var material = db.Materials.Find(materialId);

                if (material.AvailableQuantity <= 0)
                {
                    TempData["Error"] = $"Material '{material.Title}' is not available for reservation.";
                    continue;
                }

                material.AvailableQuantity -= 1;

                var circulation = new Circulation
                {
                    PatronID = patronId,
                    UniversityID = universityId.ToString(),
                    MaterialID = materialId,
                    SchoolID = schoolID,
                    RequestedDate = DateTime.Now,
                    ExpiryDate = DateTime.Now.AddDays(3),   
                    Status = "Requested",
                };
                db.Circulations.Add(circulation);
            }

            db.SaveChanges();
            TempData["Success"] = "Material reserved successfully!";
            return RedirectToAction("MyReservations");
        }

        public ActionResult MyReservations()
        {
            int patronId = Session["PatronID"] != null ? (int)Session["PatronID"] : 0;
            var reservations = db.Circulations
                                 .Where(c => c.PatronID == patronId && (c.Status == "Requested" || c.Status == "Canceled"))
                                 
                                 .Include(c => c.Material)
                                 .OrderByDescending(c => c.RequestedDate)
                                 .ToList();
            return View(reservations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelReservation(int circulationId)
        {
            var circulation = db.Circulations.FirstOrDefault(c => c.CirculationID == circulationId);

            if (circulation != null && circulation.Status == "Requested")
            {
                circulation.Status = "Canceled";

                var material = db.Materials.Find(circulation.MaterialID);
                material.AvailableQuantity += 1;

                db.SaveChanges();
                TempData["Success"] = "Reservation canceled successfully.";
            }
            else
            {
                TempData["Error"] = "Cannot cancel this reservation.";
            }

            return RedirectToAction("MyReservations");  
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddToBookingList(int materialId)
        {
            // Get PatronID from session
            int patronId = Session["PatronID"] != null ? (int)Session["PatronID"] : 0;

            if (patronId == 0)
            {
                TempData["Error"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            // Check if already exists
            var existingBooking = db.Bookinglisteds
                .FirstOrDefault(b => b.PatronID == patronId && b.MaterialID == materialId && b.Status == "Pending");

            if (existingBooking != null)
            {
                TempData["Error"] = "Material is already in your booking list.";
                return RedirectToAction("ManageMaterials"); 
            }

            var material = db.Materials.FirstOrDefault(m => m.MaterialID == materialId);
            if (material == null)
            {
                TempData["Error"] = "Material not found.";
                return RedirectToAction("ManageMaterials");
            }

            var booking = new Bookinglisted
            {
                PatronID = patronId,
                MaterialID = material.MaterialID,
                BookingDate = DateTime.Now,
                ExpiryDate = DateTime.Now.AddDays(7),  
                Status = "Pending",
                SchoolID = material.SchoolID
            };

            db.Bookinglisteds.Add(booking);

            db.SaveChanges();

            TempData["Success"] = "Material added to your booking list successfully!";
            return RedirectToAction("MyBookingList");
        }

        public ActionResult MyBookingList()
        {
            int patronId = Session["PatronID"] != null ? (int)Session["PatronID"] : 0;
            var bookings = db.Bookinglisteds
                             .Where(b => b.PatronID == patronId ||b.Status=="Pending" || b.Status == "Canceled")
                             .Include(b => b.Material)
                             .OrderByDescending(b => b.BookingDate)
                             .ToList();
            return View(bookings);
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

            return RedirectToAction("MyBookingList");
        }

        public ActionResult OverdueList()
        {
            int patronId = Session["PatronID"] != null ? (int)Session["PatronID"] : 0;
            var bookings = db.Circulations
                             .Where(b => b.PatronID == patronId && b.Status == "Overdue")
                             .Include(b => b.Material)
                             .OrderByDescending(b => b.RequestedDate)
                             .ToList();
            return View(bookings);
        }

        public ActionResult IssuedHistory()
        {
            int patronId = Session["PatronID"] != null ? (int)Session["PatronID"] : 0;

            var bookings = db.Circulations
                             .Where(b => b.PatronID == patronId && (b.Status == "Returned" || b.Status == "BookLost"))
                             .Include(b => b.Material)
                             .Include(b => b.FineDetails) 
                             .OrderByDescending(b => b.RequestedDate)
                             .ToList();

            return View(bookings);
        }

        public ActionResult MyProfile()
        {
            if (Session["UserID"] == null || Session["Role"] == null)
                return RedirectToAction("Login");

            string email = Session["UserName"].ToString();

            var patron = (from p in db.Patrons
                          join u in db.tblUsers on p.PatronEmail equals u.Email
                          join uni in db.tblUniversities on p.UniversityID equals uni.UniversityID
                         
                          select new MyProfileViewModel
                          {
                              UserID = u.UserID,
                              Username = u.Username,
                              Role = u.tblUserRoles.FirstOrDefault().tblRole.RoleName,
                              Name = p.PatronName,
                              Email = p.PatronEmail,
                              Phone = p.PatronPhone,
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
            var patron = db.Patrons.FirstOrDefault(p => p.PatronEmail == currentEmail);

            if (user != null && patron != null)
            {
              
                patron.PatronName = model.Name;
                patron.PatronPhone = model.Phone;

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

            // Redirect to dashboard
            string role = Session["Role"]?.ToString();
            if (role == "Librarian")
                return RedirectToAction("LibrarianDashboard", "Librarian");
            else
                return RedirectToAction("PatronDashboard", "Patron");
        }
    }
}