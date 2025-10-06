using LibraryMangement.Models;
using System;
using System.Collections.Generic;
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

        private const int SaltSize = 16; // 128-bit
        private const int HashSize = 32; // 256-bit
        private const int Iterations = 100_000; // PBKDF2 iterations
        public static string HashClientPassword(string clientHashedPasswordHex, string saltBase64)
        {
            byte[] saltBytes = Convert.FromBase64String(saltBase64);

            // Convert hex string to bytes
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
                       
        [HttpPost]
        public ActionResult Login(LoginViewModel model)
        {
           
            string captcha = Session["Captcha"]?.ToString();
            if (model.CaptchaCode != captcha)
            {
                ViewBag.ErrorMessage = "Invalid Captcha";
                return View(model);
            }

     
            var user = db.tblUsers.Where(u => u.Email == model.Username || u.Username == model.Username ).FirstOrDefault();
            if (user == null)
            {
                ViewBag.ErrorMessage = "Email ID not found";
                return View(model);
            }

      
            //string decryptedPassword = SecureHelper.Decrypt(user.PasswordHash);

          
            if (!VerifyPassword(model.Password,user.PasswordHash,user.PasswordSalt))
            {
                ViewBag.ErrorMessage = "Invalid password";
                return View(model);
            }

        
            Session["UserID"] = user.UserID;
            Session["UserName"] = user.Email;
            Session["Role"] =  user.tblUserRoles.FirstOrDefault().tblRole.RoleName;

     
            if (user.tblUserRoles.FirstOrDefault().tblRole.RoleName == "Librarian")
            {
                return RedirectToAction("LibrarianDashboard", "Librarian");
            }
            else if (user.tblUserRoles.FirstOrDefault().tblRole.RoleName == "Faculty")
            {
                return RedirectToAction("PatronDashboard", "Patron");
            }
            else
            {
              return RedirectToAction("PatronDashboard", "Patron");
            }
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