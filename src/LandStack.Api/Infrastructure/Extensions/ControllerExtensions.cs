using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace LandStack.Api.Infrastructure.Extensions
{
    public static class ControllerExtensions
    {
        public static string GetResourceUrl(this ControllerBase controller, object resourceId)
        {
            return $"{UriHelper.GetDisplayUrl(controller.Request)}/{resourceId}";
        }
    }
}