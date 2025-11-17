using LibraryMangement.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Controllers
{
    public class AdminController : Controller
    {
        private readonly ICFAISMSEntities db = new ICFAISMSEntities();
        
        [HttpGet]
        public ActionResult Home()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Login");

            string userId = Session["UserID"].ToString();

            
            var userUniversity = db.tblUserUniversities
                                    .FirstOrDefault(u => u.UserID == userId);

            if (userUniversity == null)
                return HttpNotFound("User's University information not found.");

            string universityID = userUniversity.UniversityID;

            Session["UniversityID"] = universityID;
            return View();
        }

        [HttpGet]
        public ActionResult AdminDashboard()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Login");

            string userId = Session["UserID"].ToString();
            string universityID = Session["UniversityID"]?.ToString();

            var universityName = db.tblUniversities.FirstOrDefault(u => u.UniversityID == universityID);
            string University = universityName.UniversityName;

            
            var librarianDesignationID = db.tblDesignations
                                           .Where(d => d.DesignationName == "Librarian")
                                           .Select(d => d.DesignationID)
                                           .FirstOrDefault();

            if (librarianDesignationID == 0)
                return HttpNotFound("Designation 'Librarian' not found in the database.");

            
            var librarianEmployees = db.tblEmployees
                                       .Where(e => e.DesignationID == librarianDesignationID &&
                                                   e.UniversityID == universityID)
                                       .Select(e => new
                                       {
                                           e.UserID,
                                           e.EmployeeName
                                       })
                                       .ToList();
            var libraries = (from l in db.tblLibraries
                             join e in db.tblEmployees on l.LibrarianUserID equals e.UserID
                             where l.UniversityID == universityID
                             select new LibraryListViewModel
                             {
                                 LibraryName = l.LibraryName,
                                 LibrarianName = e.EmployeeName,
                                 CreatedDate = l.CreatedDate,
                                 IsActive = l.IsActive
                             }).ToList();

            ViewBag.LibraryList = libraries;


            ViewBag.Librarians = new SelectList(librarianEmployees, "UserID", "EmployeeName");
            ViewBag.UniversityID = University;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateLibrary(tblLibrary model)
        {
            string universityID = Session["UniversityID"].ToString();
            if (ModelState.IsValid)
            {
                model.CreatedDate = DateTime.Now;
                model.IsActive = true;
                model.UniversityID = universityID;

                db.tblLibraries.Add(model);
                db.SaveChanges();

                TempData["Success"] = "Library created successfully!";
                return RedirectToAction("AdminDashboard");
            }

            string universityId = Session["UniversityID"]?.ToString();

          
            var librarianDesignationID = db.tblDesignations
                                           .Where(d => d.DesignationName == "Librarian")
                                           .Select(d => d.DesignationID)
                                           .FirstOrDefault();

           
            var librarianEmployees = db.tblEmployees
                                       .Where(e => e.DesignationID == librarianDesignationID &&
                                                   e.UniversityID == universityId)
                                       .Select(e => new
                                       {
                                           e.UserID,
                                           e.EmployeeName
                                       })
                                       .ToList();

            ViewBag.Librarians = new SelectList(librarianEmployees, "UserID", "EmployeeName", model.LibrarianUserID);
            ViewBag.UniversityID = universityId;

            return View(model);
        }

        [HttpGet]
        public ActionResult ManageLibraries()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Login");

            string userId = Session["UserID"].ToString();
            string universityID = Session["UniversityID"]?.ToString();


            ViewBag.UniversityName = db.tblUniversities
                                       .Where(u => u.UniversityID == universityID)
                                       .Select(u => u.UniversityName)
                                       .FirstOrDefault();


            int librarianDesig = db.tblDesignations
                                   .Where(d => d.DesignationName == "Librarian")
                                   .Select(d => d.DesignationID)
                                   .FirstOrDefault();


            int assistantDesig = db.tblDesignations
                                   .Where(d => d.DesignationName == "Assistant Librarian")
                                   .Select(d => d.DesignationID)
                                   .FirstOrDefault();


            var librarians = db.tblEmployees
                               .Where(e => e.DesignationID == librarianDesig &&
                                           e.UniversityID == universityID)
                               .ToList();

            var assistants = db.tblEmployees
                               .Where(e => e.DesignationID == assistantDesig &&
                                           e.UniversityID == universityID)
                               .ToList();

            ViewBag.Librarians = new SelectList(librarians, "UserID", "EmployeeName");
            ViewBag.Assistants = new SelectList(assistants, "UserID", "EmployeeName");


            var libraries = db.tblLibraries
                              .Where(l => l.UniversityID == universityID)
                              .ToList();


            var assistantRecords = db.tblLibraryAssistants.ToList();


            var model = libraries.Select(lib =>
            {
                var assistantRow = assistantRecords.FirstOrDefault(a => a.LibraryID == lib.LibraryID);

                string assistantUserId = assistantRow?.AssistantUserID;
                string assistantName = "Not Assigned";

                if (!string.IsNullOrEmpty(assistantUserId))
                {
                    var assistantEmp = assistants.FirstOrDefault(a => a.UserID == assistantUserId);
                    if (assistantEmp != null)
                        assistantName = assistantEmp.EmployeeName;
                }

                return new LibraryAdminViewModel
                {
                    LibraryID = lib.LibraryID,
                    LibraryName = lib.LibraryName,

                    LibrarianUserID = lib.LibrarianUserID,
                    LibrarianName = librarians.FirstOrDefault(l => l.UserID == lib.LibrarianUserID)?.EmployeeName
                                     ?? "Not Assigned",

                    AssistantUserID = assistantUserId,
                    AssistantName = assistantName,

                    CreatedDate = lib.CreatedDate,
                    IsActive = lib.IsActive
                };
            }).ToList();


            return View(model);
        }



        [HttpPost]
        public ActionResult UpdateLibraryStaff(int LibraryID, string LibrarianUserID, string AssistantUserID)
        {
            // Update Librarian in tblLibraries
            var library = db.tblLibraries.FirstOrDefault(l => l.LibraryID == LibraryID);
            if (library != null)
            {
                library.LibrarianUserID = LibrarianUserID;
            }

            // Update Assistant Librarian in tblAssistantLibrarians
            var assistant = db.tblLibraryAssistants
                              .FirstOrDefault(a => a.LibraryID == LibraryID);

            if (assistant == null)
            {
                // Create new assistant row if not exists
                assistant = new tblLibraryAssistant
                {
                    LibraryID = LibraryID,
                    AssistantUserID = AssistantUserID,
                    AssignedDate = DateTime.Now
                };
                db.tblLibraryAssistants.Add(assistant);
            }
            else
            {
                assistant.AssistantUserID = AssistantUserID;
            }

            db.SaveChanges();

            TempData["Success"] = "Library staff updated successfully!";
            return RedirectToAction("ManageLibraries");
        }

        public ActionResult ActivateLibrary(int id)
        {
            var lib = db.tblLibraries.Find(id);
            lib.IsActive = true;
            db.SaveChanges();

            TempData["Success"] = "Library activated.";
            return RedirectToAction("ManageLibraries");
        }

        public ActionResult DeactivateLibrary(int id)
        {
            var lib = db.tblLibraries.Find(id);
            lib.IsActive = false;
            db.SaveChanges();

            TempData["Success"] = "Library deactivated.";
            return RedirectToAction("ManageLibraries");
        }

        [HttpGet]
        public ActionResult ViewAssistantRequests()
        {
            string universityID = Session["UniversityID"]?.ToString();
            if (string.IsNullOrEmpty(universityID))
                return RedirectToAction("Login", "Login");

            var requests = (from r in db.tblAssistantRequests
                            join l in db.tblLibraries on r.LibraryID equals l.LibraryID
                            join empLibrarian in db.tblEmployees on r.LibrarianUserID equals empLibrarian.UserID
                            join empAssistant in db.tblEmployees on r.AssistantUserID equals empAssistant.UserID
                            where r.UniversityID == universityID
                            select new AssistantRequestViewModel
                            {
                                RequestID = r.RequestID,
                                LibraryID = r.LibraryID,
                                LibrarianUserID = r.LibrarianUserID,
                                AssistantUserID = r.AssistantUserID,
                                RequestDate = (DateTime)r.RequestDate,
                                Status = r.Status,
                                Remarks = r.Remarks,
                                LibrarianName = empLibrarian.EmployeeName,
                                AssistantName = empAssistant.EmployeeName,
                                LibraryName = l.LibraryName
                            })
                            .OrderByDescending(r => r.RequestDate)  // 🔥 Latest First
                            .ToList();

            return View(requests);
        }





        [HttpPost]
        public ActionResult ApproveAssistant(int requestId)
        {
            var request = db.tblAssistantRequests.Find(requestId);
            if (request == null) return HttpNotFound();

            request.Status = "Approved";
            db.SaveChanges();

           
            var newAssistant = new tblLibraryAssistant
            {
                LibraryID = request.LibraryID,
                LibrarianUserID = request.LibrarianUserID,
                AssistantUserID = request.AssistantUserID,
                AssignedDate = DateTime.Now,
                IsActive = true
            };

            db.tblLibraryAssistants.Add(newAssistant);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Assistant Librarian approved and assigned successfully.";
            return RedirectToAction("ViewAssistantRequests");
        }

        [HttpPost]
        public ActionResult RejectAssistant(int requestId, string remarks)
        {
            var request = db.tblAssistantRequests.Find(requestId);
            if (request == null) return HttpNotFound();

            request.Status = "Rejected";
            request.Remarks = remarks;
            db.SaveChanges();

            TempData["ErrorMessage"] = "Assistant Librarian request rejected.";
            return RedirectToAction("ViewAssistantRequests");
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

    }
}