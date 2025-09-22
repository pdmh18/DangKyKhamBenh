using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Web;
using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class DoctorOnlyAttribute : AuthorizeAttribute
{
    protected override bool AuthorizeCore(HttpContextBase httpContext)
    {
        var ses = httpContext.Session;

        // 1) Role phải là User
        var role = (ses["TK_Role"] ?? ses["Role"] ?? string.Empty).ToString();
        var isUserRole = role.Equals("User", StringComparison.OrdinalIgnoreCase);
        if (!isUserRole) return false;

        // 2) StaffType phải là Bác sĩ (khử dấu/loại ký tự không phải chữ)
        var staff = (ses["StaffType"] ?? ses["TK_StaffType"] ?? ses["StaffTypeRaw"] ?? string.Empty).ToString();
        var s = RemoveDiacritics(staff).ToUpperInvariant();
        s = new string(s.Where(char.IsLetter).ToArray());
        var isDoctor = (s == "BACSI" || s == "DOCTOR");
        if (!isDoctor) return false;

        // 3) Có mã bác sĩ (tránh vào dashboard mà thiếu khóa)
        var bsMa = (ses["BS_MaBacSi"] ?? string.Empty).ToString();
        if (string.IsNullOrWhiteSpace(bsMa)) return false;

        // 4) (Tuỳ chọn) Không cho account bị khóa
        var trangThai = (ses["TK_TrangThai"] ?? string.Empty).ToString();
        if (trangThai.Equals("Locked", StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
    {
        // Nếu là Admin thì đẩy về Admin; còn lại quay lại Login
        var ses = filterContext.HttpContext.Session;
        var role = (ses["TK_Role"] ?? ses["Role"] ?? string.Empty).ToString();
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            filterContext.Result = new RedirectResult("~/Admin/Dashboard");
            return;
        }

        filterContext.Result = new RedirectResult("~/Account/Login");
    }

    private static string RemoveDiacritics(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var n = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}