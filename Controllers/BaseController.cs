using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InventorySystem.Controllers
{
    public class BaseController : Controller
    {
        // Alert management methods
        protected void SetSuccessMessage(string message)
        {
            ClearAlerts();
            TempData["SuccessMessage"] = message;
        }

        protected void SetErrorMessage(string message)
        {
            ClearAlerts();
            TempData["ErrorMessage"] = message;
        }

        protected void SetWarningMessage(string message)
        {
            ClearAlerts();
            TempData["WarningMessage"] = message;
        }

        protected void SetInfoMessage(string message)
        {
            ClearAlerts();
            TempData["InfoMessage"] = message;
        }

        protected void ClearAlerts()
        {
            TempData.Remove("SuccessMessage");
            TempData.Remove("ErrorMessage");
            TempData.Remove("WarningMessage");
            TempData.Remove("InfoMessage");
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Get current controller and action
            var currentController = ControllerContext.ActionDescriptor.ControllerName;
            var currentAction = ControllerContext.ActionDescriptor.ActionName;
            
            // Get referer information
            var referer = Request.Headers["Referer"].ToString();
            var isRedirectFromSameController = false;

            if (!string.IsNullOrEmpty(referer))
            {
                try
                {
                    var refererUri = new Uri(referer);
                    var refererPath = refererUri.AbsolutePath;
                    isRedirectFromSameController = refererPath.Contains($"/{currentController}/", StringComparison.OrdinalIgnoreCase);
                }
                catch (UriFormatException)
                {
                    // If referer is malformed, treat as external
                    isRedirectFromSameController = false;
                }
            }

            // Clear alerts if coming from a different controller
            // Exception: Don't clear on AJAX requests or if explicitly preserving alerts
            var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            var preserveAlerts = TempData["PreserveAlerts"] != null;

            if (!isRedirectFromSameController && !isAjaxRequest && !preserveAlerts)
            {
                ClearAlerts();
            }

            // Remove the preserve flag after checking
            TempData.Remove("PreserveAlerts");

            base.OnActionExecuting(context);
        }

        // Helper method to preserve alerts across controller boundaries (when needed)
        protected void PreserveAlertsForNextRequest()
        {
            TempData["PreserveAlerts"] = true;
        }

        // Helper method to get current controller name for logging
        protected string GetCurrentControllerName()
        {
            return ControllerContext.ActionDescriptor.ControllerName;
        }

        // Helper method to get current action name for logging
        protected string GetCurrentActionName()
        {
            return ControllerContext.ActionDescriptor.ActionName;
        }
    }
}