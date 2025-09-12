using LibraryMangement.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Controllers
{
    public class PatronController : Controller
    {
        private readonly LMSEntities db = new LMSEntities();
        // GET: Patron
        public ActionResult PatronDashboard()
        {
            var loggedInEmail = Session["UserID"].ToString();

            var patron = db.Patrons.FirstOrDefault(p => p.PatronEmail == loggedInEmail);

            if (patron == null)
                return HttpNotFound();

            int patronId = patron.PatronID;
            Session["PatronID"] = patron.PatronID;
            Session["UniversityID"] = patron.UniversityID;
            var model = new PatronDashboardViewModel
            {
                PatronID = patron.PatronID,
                PatronName = patron.PatronName,
                ActiveIssuedCount = db.Circulations.Count(c => c.PatronID == patronId && c.Status == "Issued"),
                OverdueCount = db.Circulations.Count(c => c.PatronID == patronId && c.Status == "Overdue"),
                PendingReservations = db.Reservations.Count(r => r.PatronID == patronId && r.Status == "Pending"),
                ActiveIssues = db.Circulations
                    .Where(c => c.PatronID == patronId && c.Status == "Issued")
                  .Include("MaterialCopy.Material")

                    .ToList(),
                PendingReservationList = db.Reservations
                    .Where(r => r.PatronID == patronId && r.Status == "Pending")
                    .Include(r => r.Material)
                    .ToList()
            };

            return View(model);
        }


        public ActionResult ManageMaterials()
        {
            var loggedInLibrarianId = Session["UserID"].ToString();

            // Get UniversityID of logged-in librarian
            var universityId = db.Patrons
                                 .Where(l => l.PatronEmail == loggedInLibrarianId)
                                 .Select(l => l.UniversityID)
                                 .FirstOrDefault();

            var materials = db.Materials
                              .Include(m => m.Author)
                              .Where(m => m.UniversityID == universityId)
                              .AsQueryable();

            int patronId = Convert.ToInt32(Session["PatronID"]);
            var cartMaterialIDs = db.CartItems
                                    .Where(c => c.PatronID == patronId)
                                    .Select(c => c.MaterialID)
                                    .ToList();

            ViewBag.CartMaterialIDs = cartMaterialIDs;

            var model = materials.Select(m => new MaterialViewModel
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
                MaterialType = m.MaterialType
            }).ToList();

            return View(model);
        }


        public ActionResult AddToCart(int id)
        {
            int patronId = Convert.ToInt32(Session["PatronID"]);

            // Check if already in cart
            var existingCartItem = db.CartItems.FirstOrDefault(c => c.PatronID == patronId && c.MaterialID == id);
            if (existingCartItem == null)
            {
                var cartItem = new CartItem
                {
                    PatronID = patronId,
                    MaterialID = id,
                    AddedAt = DateTime.Now
                };

                db.CartItems.Add(cartItem);
                db.SaveChanges();
            }

            return RedirectToAction("ManageMaterials");
        }


        public ActionResult Cart()
        {
            int patronId = Convert.ToInt32(Session["PatronID"]);

            var cartItems = db.CartItems
                .Where(c => c.PatronID == patronId)
               
                .Include(c => c.Material.Author)
                .ToList();



            var model = cartItems.Select(c => new MaterialViewModel
            {
                MaterialID = c.Material.MaterialID,
                Title = c.Material.Title,
                Author = c.Material.Author != null ? c.Material.Author.Name : "Unknown",
                Edition = c.Material.Edition,
              
                YearPublished = (int)c.Material.YearPublished,
               
                Vol = c.Material.Vol,
              
                AvailableQuantity = (int)c.Material.AvailableQuantity,
               
            }).ToList();

            return View(model);
        }

        [HttpPost]
        public ActionResult CheckoutSelected(List<int> SelectedMaterialIDs)
        {
            int patronId = Convert.ToInt32(Session["PatronID"]);

            // Get the cart items matching the selected MaterialIDs
            var cartItems = db.CartItems
                .Where(c => c.PatronID == patronId && SelectedMaterialIDs.Contains(c.MaterialID))
                .Include(c => c.Material)
                .ToList();

            var model = cartItems.Select(c => new MaterialViewModel
            {
                MaterialID = c.Material.MaterialID,
                Title = c.Material.Title,
                AvailableQuantity = (int)c.Material.AvailableQuantity
            }).ToList();

            return View("Checkout", model);
        }



        public ActionResult RemoveFromCart(int id)
        {
            int patronId = Convert.ToInt32(Session["PatronID"]);

            var cartItem = db.CartItems.FirstOrDefault(c => c.PatronID == patronId && c.MaterialID == id);
            if (cartItem != null)
            {
                db.CartItems.Remove(cartItem);
                db.SaveChanges();
            }

            return RedirectToAction("Cart");
        }

        public ActionResult Checkout()
        {
            int patronId = Convert.ToInt32(Session["PatronID"]);

            var cartItems = db.CartItems
                .Where(c => c.PatronID == patronId)
                .Include(c => c.Material)
                .ToList();

            var model = cartItems.Select(c => new MaterialViewModel
            {
                MaterialID = c.Material.MaterialID,
                Title = c.Material.Title,
                AvailableQuantity = (int)c.Material.AvailableQuantity
            }).ToList();

            return View(model);
        }


        [HttpPost]
       
        public ActionResult ProcessCheckout()
        {
            int patronId = Convert.ToInt32(Session["PatronID"]);
            int universityId = Convert.ToInt32(Session["UniversityID"]);
            var cartItems = db.CartItems
                .Where(c => c.PatronID == patronId)
                .Include(c => c.Material)
                .ToList();

             
            if (!cartItems.Any())
            {
                ModelState.AddModelError("", "Cart is empty.");
                return RedirectToAction("Cart");
            }

            // Create a new IssuanceRequest
            var issuanceRequest = new IssuanceRequest
            {
                PatronID = patronId,
                RequestDate = DateTime.Now,
                Status = "Requested",
                UniversityID = universityId
            };

            db.IssuanceRequests.Add(issuanceRequest);
            db.SaveChanges();  // Save to generate RequestID

            foreach (var cartItem in cartItems)
            {
              /*  var availableCopy = db.MaterialCopies
                    .FirstOrDefault(mc => mc.MaterialID == cartItem.MaterialID && mc.Status == "Available");*/

              
                    var circulation = new Circulation
                    {
                        PatronID = patronId,
                        MaterialID = cartItem.MaterialID,
                        RequestID = issuanceRequest.RequestID,
                        Status = "Requested",
                        UniversityID = universityId
                    };

                    db.Circulations.Add(circulation);

                   /* availableCopy.Status = "Requested";*//*

                    var material = db.Materials.Find(cartItem.MaterialID);
                    if (material != null)
                        material.AvailableQuantity -= 1;*/
                
            }

            // Remove CartItems after successful issuance
            db.CartItems.RemoveRange(cartItems);
            db.SaveChanges();

            return RedirectToAction("PatronDashboard");
        }

    }
}