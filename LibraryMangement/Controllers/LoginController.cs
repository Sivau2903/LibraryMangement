using LibraryMangement.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Controllers
{
    public class LoginController : Controller
    {
        private readonly LMSEntities db = new LMSEntities();
        // GET: Login

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(User model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (string.IsNullOrEmpty(model.PasswordHash))
                    {
                        ModelState.AddModelError("PasswordHash", "Password is required");
                        return View(model);
                    }
                    model.Username = model.Username;
                    // Encrypt password
                    model.PasswordHash = SecureHelper.Encrypt(model.PasswordHash);

                    // Ensure required fields are not null
                    model.Role = model.Role ?? "Patron"; // default if empty

                    // Add user
                    db.Users.Add(model);
                    db.SaveChanges();

                    TempData["SuccessMessage"] = "User created successfully!";
                    return RedirectToAction("Create");
                }
                catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx)
                {
                    string message = dbEx.InnerException != null ? dbEx.InnerException.Message : dbEx.Message;
                    SecureHelper.LogToFile("User Creation DB Error", message);
                    ViewBag.ErrorMessage = "Database error: " + message;
                }
                catch (Exception ex)
                {
                    SecureHelper.LogToFile("User Creation Error", ex.Message);
                    ViewBag.ErrorMessage = "Error: " + ex.Message;
                }
            }

            return View(model);
        }





        public ActionResult Login()
        {
            return View();
        }

        public ActionResult GenerateCaptcha()
        {
            string captchaText = new Random().Next(1000, 9999).ToString();
            Session["Captcha"] = captchaText;

            using (Bitmap bmp = new Bitmap(100, 40))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.DrawString(captchaText, new Font("Arial", 20), Brushes.Black, new PointF(10, 5));
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return File(ms.ToArray(), "image/png");
                }
            }
        }

        [HttpPost]
        public ActionResult Login(LoginViewModel model)
        {
            // 1️⃣ Check captcha
            string captcha = Session["Captcha"]?.ToString();
            if (model.CaptchaCode != captcha)
            {
                ViewBag.ErrorMessage = "Invalid Captcha";
                return View(model);
            }

            // 2️⃣ Find user by username/email
            var user = db.Users.FirstOrDefault(u => u.Username == model.Username);
            if (user == null)
            {
                ViewBag.ErrorMessage = "Email ID not found";
                return View(model);
            }

            // 3️⃣ Decrypt stored password
            string decryptedPassword = SecureHelper.Decrypt(user.PasswordHash);

            // 4️⃣ Compare decrypted password with input
            if (decryptedPassword != model.Password)
            {
                ViewBag.ErrorMessage = "Invalid password";
                return View(model);
            }

            // 5️⃣ Store UserID and Role in session
            Session["UserID"] = user.Username;
            Session["Role"] = user.Role;

            // 6️⃣ Redirect based on Role
            if (user.Role == "Librarian")
            {
                return RedirectToAction("LibrarianDashboard", "Librarian");
            }
            else // assume Patron
            {
                return RedirectToAction("PatronDashboard", "Patron");
            }
        }



        public bool ValidateUser(string email, string dobInput)
        {
            var user = db.Users.FirstOrDefault(u => u.PasswordHash == email);
            if (user == null) return false;

            // Normalize DOB format
            DateTime parsedDob;
            if (!DateTime.TryParse(dobInput, out parsedDob))
                return false;

            string formattedDob = parsedDob.ToString("yyyyMMdd"); // match storage format
            string encryptedDob = SecureHelper.Encrypt(formattedDob);

            return encryptedDob == user.PasswordHash;
        }

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login", "Login");
        }

        public ActionResult ChangePassword()
        {
            return View();
        }

    }
}