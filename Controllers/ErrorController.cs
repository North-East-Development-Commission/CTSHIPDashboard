using System.Diagnostics;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CTSHIPDashboard.Controllers
{
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        [Route("/Error")]
        public IActionResult Index()
        {
            IExceptionHandlerPathFeature? error =
                HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            if (error?.Error != null)
            {
                try
                {
                    _logger.LogError(
                        error.Error,
                        "Unhandled request error at {Path}. Request ID: {RequestId}",
                        error.Path,
                        HttpContext.TraceIdentifier);
                }
                catch
                {
                    // The friendly page must still render if an external log provider is unavailable.
                }
            }

            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return View("~/Views/Shared/Error.cshtml", BuildModel(500));
        }

        [Route("/Error/{statusCode:int}")]
        public IActionResult StatusCodePage(int statusCode)
        {
            Response.StatusCode = statusCode;
            return View("~/Views/Shared/Error.cshtml", BuildModel(statusCode));
        }

        private ErrorViewModel BuildModel(int statusCode)
        {
            (string title, string message, string icon) = statusCode switch
            {
                400 => (
                    "Invalid request",
                    "The request could not be understood. Check the information supplied and try again.",
                    "bi-exclamation-circle-fill"),
                401 => (
                    "Sign-in required",
                    "Please sign in to continue to this page.",
                    "bi-person-lock"),
                403 => (
                    "Access denied",
                    "Your account does not have permission to perform this action.",
                    "bi-shield-lock-fill"),
                404 => (
                    "Page not found",
                    "The page or record may have moved, been removed, or never existed.",
                    "bi-search"),
                405 => (
                    "Action not allowed",
                    "This page does not support the requested action.",
                    "bi-slash-circle-fill"),
                408 => (
                    "Request timed out",
                    "The request took too long. Please try again.",
                    "bi-clock-history"),
                429 => (
                    "Too many requests",
                    "Please wait briefly before trying again.",
                    "bi-hourglass-split"),
                503 => (
                    "Service temporarily unavailable",
                    "The service is temporarily unavailable. Please try again shortly.",
                    "bi-tools"),
                _ => (
                    "Something went wrong",
                    "We could not complete your request. Your data was not intentionally changed.",
                    "bi-exclamation-triangle-fill")
            };

            return new ErrorViewModel
            {
                StatusCode = statusCode,
                Title = title,
                Message = message,
                Icon = icon,
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            };
        }
    }
}
