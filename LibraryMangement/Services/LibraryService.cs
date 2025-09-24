using LibraryMangement.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement.Services
{
    public class LibraryService
    {
        public static void UpdateOverdueFines()
        {
            using (var db = new ICFAISMSEntities())
            {
                decimal finePerDay = 5.00m; // Ideally from config or a settings table

                var circulations = db.Circulations
                                     .Where(c => c.Status == "Issued" && c.DueDate < DateTime.Today)
                                     .ToList();

                foreach (var circulation in circulations)
                {
                    int overdueDays = (DateTime.Today - circulation.DueDate.Value).Days;
                    overdueDays = overdueDays > 0 ? overdueDays : 0;

                    circulation.Status = "Overdue";
                    circulation.IsOverdue = true;
                    circulation.FineAmount = overdueDays * finePerDay;
                    circulation.LastFineUpdateDate = DateTime.Today;

                    // Fetch Patron Email
                    var patron = db.Patrons.FirstOrDefault(p => p.PatronID == circulation.PatronID);
                    if (patron != null && !string.IsNullOrWhiteSpace(patron.PatronEmail))
                    {
                        EmailService.SendOverdueNotification(patron.PatronEmail, circulation);
                    }
                }

                db.SaveChanges();
            }
        }

        public static void ExpireReservationsAndBookings()
        {
            using (var db = new ICFAISMSEntities())
            {
                var expiredCirculations = db.Circulations
                    .Where(c => c.Status == "Requested" && c.ExpiryDate <= DateTime.Today)
                    .ToList();

                foreach (var circ in expiredCirculations)
                {
                    circ.Status = "Expired";

                    var material = db.Materials.Find(circ.MaterialID);
                    material.AvailableQuantity += 1;
                }

                var expiredBookings = db.Bookinglisteds
                    .Where(b => b.Status == "Pending" && b.ExpiryDate <= DateTime.Today)
                    .ToList();

                foreach (var booking in expiredBookings)
                {
                    booking.Status = "Expired";
                }

                db.SaveChanges();
            }
        }

    }

}