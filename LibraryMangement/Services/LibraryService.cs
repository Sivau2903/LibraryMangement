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
                    
                    string userId = circulation.UserID;
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

                    // Fetch Patron Email
                    if (patronEmail != null && !string.IsNullOrWhiteSpace(patronEmail))
                    {
                        EmailService.SendOverdueNotification(patronEmail, circulation);
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

                // Expire Pending bookings whose BookingExpiryDate has passed
                var expiredBookings = db.Bookinglisteds
                                        .Where(b => b.Status == "Pending" && b.ExpiryDate <= DateTime.Today)
                                        .ToList();
                foreach (var booking in expiredBookings)
                {
                    booking.Status = "Expired";
                }

                // Expire Notified bookings whose HoldExpiryDate has passed
                var expiredHoldBookings = db.Bookinglisteds
                                            .Where(b => b.Status == "Notified" && b.HoldExpiryDate <= DateTime.Now)
                                            .OrderBy(b => b.BookingDate)
                                            .ToList();

                foreach (var booking in expiredHoldBookings)
                {
                    booking.Status = "Expired";

                    // Find the exact MaterialCopy that was OnHold for this booking
                    var materialCopy = db.MaterialCopies
                                         .Where(mc => mc.MaterialID == booking.MaterialID && mc.Status == "OnHold")
                                         .OrderBy(mc => mc.CopyID) // optional: pick the first available OnHold copy
                                         .FirstOrDefault();

                    if (materialCopy != null)
                    {
                        // Assign to next patron in queue (Pending)
                        var nextBooking = db.Bookinglisteds
                                            .Where(b => b.MaterialID == booking.MaterialID && b.Status == "Pending")
                                            .OrderBy(b => b.BookingDate)
                                            .FirstOrDefault();

                        if (nextBooking != null)
                        {
                            nextBooking.Status = "Notified";
                            nextBooking.AssignedDate = DateTime.Now;
                            nextBooking.HoldExpiryDate = DateTime.Now.AddDays(2);

                            // Assign the exact MaterialCopy to the next patron
                            materialCopy.Status = "OnHold";

                            string userId = nextBooking.UserID;
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
                          

                            if (!string.IsNullOrWhiteSpace(patronEmail))
                                EmailService.SendBookingAvailableNotification(patronEmail, nextBooking);
                        }
                        else
                        {
                            // No pending booking, make the copy available
                            materialCopy.Status = "Available";
                        }
                    }
                }


                db.SaveChanges();
            }
        }


        //public static void ExpireReservationsAndBookings()
        //{
        //    using (var db = new ICFAISMSEntities())
        //    {
        //        var expiredCirculations = db.Circulations
        //            .Where(c => c.Status == "Requested" && c.ExpiryDate <= DateTime.Today)
        //            .ToList();

        //        foreach (var circ in expiredCirculations)
        //        {
        //            circ.Status = "Expired";

        //            var material = db.Materials.Find(circ.MaterialID);
        //            material.AvailableQuantity += 1;
        //        }

        //        var expiredBookings = db.Bookinglisteds
        //            .Where(b => b.Status == "Pending" && b.ExpiryDate <= DateTime.Today)
        //            .ToList();

        //        foreach (var booking in expiredBookings)
        //        {
        //            booking.Status = "Expired";
        //        }

        //        db.SaveChanges();
        //    }
        //}

    }

}