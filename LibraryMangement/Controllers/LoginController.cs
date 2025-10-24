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
                    // Encrypt password
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

                // Add noise lines
                using (Pen pen = new Pen(Color.Gray, 1))
                {
                    for (int i = 0; i < 8; i++)
                        g.DrawLine(pen, rand.Next(0, bmp.Width), rand.Next(0, bmp.Height),
                            rand.Next(0, bmp.Width), rand.Next(0, bmp.Height));
                }

                // Draw captcha text with random rotation for each character
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

                // Add random dots for distortion
                for (int i = 0; i < 50; i++)
                    bmp.SetPixel(rand.Next(bmp.Width), rand.Next(bmp.Height), Color.Gray);

                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return File(ms.ToArray(), "image/png");
                }
            }
        }

        private const int SaltSize = 16; // 128-bit
        private const int HashSize = 32; // 256-bit
        private const int Iterations = 100_000; // PBKDF2 iterations


        // ✅ HashClientPassword: handles Base64 and GUID salts
        public static string HashClientPassword(string clientHashedPasswordHex, string saltBase64)
        {
            byte[] saltBytes;

            try
            {
                // Try normal Base64 decode first
                saltBytes = Convert.FromBase64String(saltBase64);
            }
            catch (FormatException)
            {
                // If not Base64, treat as GUID
                saltBytes = Guid.Parse(saltBase64).ToByteArray();
            }

            // Convert client SHA256 hex string → bytes
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


        // ✅ VerifyPassword: simple & static — doesn’t require userId
        public static bool VerifyPassword(string enteredClientHex, string storedHash, string salt)
        {
            string hashOfInput = HashClientPassword(enteredClientHex, salt);
            return hashOfInput == storedHash;
        }


        // ✅ UpdateSaltInDatabase: instance method (optional, only if you need to replace GUID salts permanently)
        private void UpdateSaltInDatabase(string userId, string base64Salt)
        {
            var user = db.tblUsers.FirstOrDefault(u => u.UserID == userId);
            if (user != null)
            {
                user.PasswordSalt = base64Salt;
                db.SaveChanges();
            }
        }


        // ✅ Login Action
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

            // ✅ Fixed VerifyPassword call
            if (!VerifyPassword(model.Password, user.PasswordHash, user.PasswordSalt))
            {
                ViewBag.ErrorMessage = "Invalid password";
                return View(model);
            }

            // ✅ Session assignments
            Session["UserID"] = user.UserID;
            Session["UserName"] = user.Email;
            Session["Role"] = user.tblUserRoles.FirstOrDefault()?.tblRole.RoleName;

            // ✅ Redirects
            string role = Session["Role"].ToString();
            if (role == "Librarian")
                return RedirectToAction("LibrarianDashboard", "Librarian");
            else
                return RedirectToAction("PatronDashboard", "Patron");
        }


        public bool ValidateUser(string email, string dobInput)
        {
            var user = db.tblUsers.FirstOrDefault(u => u.PasswordHash == email);
            if (user == null) return false;

            // Normalize DOB format
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