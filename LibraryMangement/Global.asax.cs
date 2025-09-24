using Hangfire;
using LibraryMangement.Services;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace LibraryMangement
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

        }


    }
}
