using LibraryMangement.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;

namespace LibraryMangement.Controllers
{
    public class LoginController : HomeController
    {
        private readonly ICFAISMSEntities db = new ICFAISMSEntities();
        // GET: Login

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create( tblUser model)
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
                   
                    model.PasswordHash = SecureHelper.Encrypt(model.PasswordHash);

                    //model.tblUserRoles. = model.Role ?? "Patron"; // default if empty

                    db.tblUsers.Add(model);
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
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var rand = new Random();
            string captchaText = new string(Enumerable.Repeat(chars, 1)
                .Select(s => s[rand.Next(s.Length)]).ToArray());

            Session["Captcha"] = captchaText;

            using (Bitmap bmp = new Bitmap(140, 50))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);

               
                using (Pen pen = new Pen(Color.Gray, 1))
                {
                    for (int i = 0; i < 8; i++)
                        g.DrawLine(pen, rand.Next(0, bmp.Width), rand.Next(0, bmp.Height),
                            rand.Next(0, bmp.Width), rand.Next(0, bmp.Height));
                }

               
                using (Font font = new Font("Arial", 22, FontStyle.Bold))
                {
                    for (int i = 0; i < captchaText.Length; i++)
                    {
                        float angle = rand.Next(-20, 20);
                        g.TranslateTransform(20 + i * 20, 20);
                        g.RotateTransform(angle);
                        g.DrawString(captchaText[i].ToString(), font, Brushes.Black, 0, 0);
                        g.ResetTransform();
                    }
                }

               
                for (int i = 0; i < 50; i++)
                    bmp.SetPixel(rand.Next(bmp.Width), rand.Next(bmp.Height), Color.Gray);

                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return File(ms.ToArray(), "image/png");
                }
            }
        }

        private const int SaltSize = 16; 
        private const int HashSize = 32; 
        private const int Iterations = 100_000; 


       
        public static string HashClientPassword(string clientHashedPasswordHex, string saltBase64)
        {
            byte[] saltBytes;

            try
            {
               
                saltBytes = Convert.FromBase64String(saltBase64);
            }
            catch (FormatException)
            {
               
                saltBytes = Guid.Parse(saltBase64).ToByteArray();
            }

            
            byte[] passwordBytes = new byte[clientHashedPasswordHex.Length / 2];
            for (int i = 0; i < passwordBytes.Length; i++)
            {
                passwordBytes[i] = Convert.ToByte(clientHashedPasswordHex.Substring(i * 2, 2), 16);
            }

            using (var pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, saltBytes, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(HashSize);
                return Convert.ToBase64String(hash);
            }
        }

        public static bool VerifyPassword(string enteredClientHex, string storedHash, string salt)
        {
            string hashOfInput = HashClientPassword(enteredClientHex, salt);
            return hashOfInput == storedHash;
        }

        private void UpdateSaltInDatabase(string userId, string base64Salt)
        {
            var user = db.tblUsers.FirstOrDefault(u => u.UserID == userId);
            if (user != null)
            {
                user.PasswordSalt = base64Salt;
                db.SaveChanges();
            }
        }

        [HttpPost]
        public ActionResult Login(LoginViewModel model)
        {
            string captcha = Session["Captcha"]?.ToString();
            if (model.CaptchaCode != captcha)
            {
                ViewBag.ErrorMessage = "Invalid Captcha";
                return View(model);
            }

            var user = db.tblUsers.FirstOrDefault(u => u.Email == model.Username || u.Username == model.Username);
            if (user == null)
            {
                ViewBag.ErrorMessage = "Email ID not found";
                return View(model);
            }

            if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.PasswordSalt))
            {
                ViewBag.ErrorMessage = "User credentials are invalid.";
                return View(model);
            }

            if (!VerifyPassword(model.Password, user.PasswordHash, user.PasswordSalt))
            {
                ViewBag.ErrorMessage = "Invalid password";
                return View(model);
            }

            Session["UserID"] = user.UserID;
            Session["UserName"] = user.Email;
            Session["Name"] = user.Username;

            var role = user.tblUserRoles.FirstOrDefault()?.tblRole?.RoleName;
            if (string.IsNullOrEmpty(role))
            {
                ViewBag.ErrorMessage = "User role not assigned. Contact Administrator.";
                return View(model);
            }

            Session["Role"] = role;
            

            string userRole = Session["Role"] as string;
            if (string.IsNullOrEmpty(userRole))
            {
                ViewBag.ErrorMessage = "User role missing. Contact admin.";
                return View(model);
            }
            
            if (userRole == "Librarian")
            {
                var Designation = db.tblEmployees.FirstOrDefault(a => a.UserID == user.UserID);
                int designationid = (int)Designation.DesignationID;

                var id = db.tblDesignations.FirstOrDefault(b => b.DesignationID == designationid);
                string Name = id.DesignationName;
                Session["Designation"] = Name;
                if (Name == "Assistant Librarian")
                {
                    return RedirectToAction("LibrarianDashboard", "Librarian");
                }
                else
                {
                    return RedirectToAction("LibrarianDashboard", "Librarian");
                }
                  
            }
            else if (userRole == "UniversityAdmin")
                return RedirectToAction("Home", "Admin");
            else
                return RedirectToAction("PatronDashboard", "Patron");
        }

        public bool ValidateUser(string email, string dobInput)
        {
            var user = db.tblUsers.FirstOrDefault(u => u.PasswordHash == email);
            if (user == null) return false;

            DateTime parsedDob;
            if (!DateTime.TryParse(dobInput, out parsedDob))
                return false;

            string formattedDob = parsedDob.ToString("yyyyMMdd"); 
            string encryptedDob = SecureHelper.Encrypt(formattedDob);

            return encryptedDob == user.PasswordHash;
        }

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login", "Login");
        }

    }
}