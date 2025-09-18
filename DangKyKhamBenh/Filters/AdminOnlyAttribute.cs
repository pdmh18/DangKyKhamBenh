using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DangKyKhamBenh.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)] //Cho phép gắn attribute này lên class (controller) hoặc method (action)
    public class AdminOnlyAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            var user = httpContext.Session["User"] as string;
            var role = httpContext.Session["Role"] as string;
            return !string.IsNullOrWhiteSpace(user)
                   && string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase);
        }
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            // nếu chưa đăng nhập -> sang login
            if (string.IsNullOrWhiteSpace(filterContext.HttpContext.Session["User"] as string))
            {
                filterContext.Result = new RedirectResult("/Account/Login?returnUrl=/Admin/Pending");
                return;
            }
            // đã login nhưng không phải admin
            filterContext.Result = new HttpUnauthorizedResult();
        }
    }
}