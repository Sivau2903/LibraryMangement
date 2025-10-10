using Hangfire;
using LibraryMangement.Services;
using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LibraryMangement
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Hangfire configuration
            var sqlConnectionString = @"Server=CITHP01;Database=LMS;Trusted_Connection=True;";

            GlobalConfiguration.Configuration
                .UseSqlServerStorage(sqlConnectionString);

            // Start Hangfire server
            app.UseHangfireServer(new BackgroundJobServerOptions
            {
                ServerName = "LibraryHangfireServer"
            });

            // Enable dashboard (optional, can secure later)
            app.UseHangfireDashboard("/hangfire");

            // Time zone: India Standard Time
            var indianTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    //        // Schedule Morning Job at 09:30 IST
    //        RecurringJob.AddOrUpdate(
    //            "UpdateOverdueFinesMorning",
    //            () => LibraryService.UpdateOverdueFines(),
    //            "30 9 * * *",
    //            new RecurringJobOptions { TimeZone = indianTimeZone });

    //        // Schedule Afternoon Job at 15:30 IST
    //        RecurringJob.AddOrUpdate(
    //            "UpdateOverdueFinesAfternoon",
    //            () => LibraryService.UpdateOverdueFines(),
    //            "30 15 * * *",
    //            new RecurringJobOptions { TimeZone = indianTimeZone });

    //        RecurringJob.AddOrUpdate(
    //"ExpireReservationsAndBookings",
    //() => LibraryService.ExpireReservationsAndBookings(),
    //"30 9 * * *",
    //            new RecurringJobOptions { TimeZone = indianTimeZone });


        }
    }
}