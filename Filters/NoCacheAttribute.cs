// Filters/NoCacheAttribute.cs
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System;

namespace CredWise_Trail.Filters
{
    public class NoCacheAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is ViewResult || context.Result is PartialViewResult)
            {
                // Set cache control headers to prevent caching by browsers and proxies
                context.HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                context.HttpContext.Response.Headers["Pragma"] = "no-cache"; // HTTP 1.0.
                context.HttpContext.Response.Headers["Expires"] = "-1";      // Proxies.
            }
            base.OnResultExecuting(context);
        }
    }
}