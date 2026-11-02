using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FBISDNETCore.MVC.Authentication
{
    // Put on the Identity pages that only make sense for email/password accounts (Program.cs) when
    // LocalAccountOptions.Enabled is false, so they answer 404 instead of working.
    public class LocalAccountsDisabledFilter : IPageFilter
    {
        public void OnPageHandlerSelected(PageHandlerSelectedContext context) { }

        public void OnPageHandlerExecuting(PageHandlerExecutingContext context) => context.Result = new NotFoundResult();

        public void OnPageHandlerExecuted(PageHandlerExecutedContext context) { }
    }
}
