using LibraryMangement.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;

namespace LibraryMangement.Services
{
    public static class EmailService
    {
        public static void SendOverdueNotification(string toEmail, Circulation circulation)
        {
            int overdueDays = 0;

            if (circulation.DueDate.HasValue)
            {
                overdueDays = (DateTime.Today - circulation.DueDate.Value).Days;
                overdueDays = overdueDays > 0 ? overdueDays : 0;
            }

            var subject = "Library Overdue Book Notice";
            var body = $@"
        Dear Patron,<br/>
        Your book '<strong>{circulation.MaterialCopy.Material.Title}</strong>' is overdue by {overdueDays} day(s).<br/>
        Current Fine Amount: {circulation.FineAmount:C}.<br/>
        Please return the book as soon as possible.<br/><br/>
        Regards,<br/>Library Team.";

            SendEmail(toEmail, subject, body);
        }




        private static void SendEmail(string toEmail, string subject, string body)
        {
            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("shivaupputuri5@gmail.com", "uxwvtphmvzhqqqpl"),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress("shivaupputuri5@gmail.com"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };

            mailMessage.To.Add(toEmail);

            smtpClient.Send(mailMessage);
        }
    }

}