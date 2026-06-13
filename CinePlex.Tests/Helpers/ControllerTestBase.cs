using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using System.Security.Claims;

namespace CinePlex.Tests.Helpers
{
    public abstract class ControllerTestBase
    {
        protected static void SetupControllerContext(Controller controller, string? userId = null, bool isAuthenticated = false)
        {
            var session = new FakeSession();
            var httpContext = new DefaultHttpContext();
            httpContext.Session = session;

            if (isAuthenticated && userId != null)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, userId),
                    new(ClaimTypes.Name, "testuser")
                };
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            }

            var tempDataProvider = new Mock<ITempDataProvider>();
            var tempData = new TempDataDictionary(httpContext, tempDataProvider.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            controller.TempData = tempData;
        }
    }
}
