
using DangKyKhamBenh.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Web.UI.WebControls.WebParts;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;

namespace DangKyKhamBenh.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account     
        [HttpGet, AllowAnonymous]
        public ActionResult Login(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Login(string user, string password, string returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập Username và Password.";
                return View();
            }

            try
            {

                using (var conn = new OracleConnection(
                    ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString))
                {
                    conn.Open();

                    // Lấy đủ thông tin cần cho điều hướng và session
                    const string sql = @"
                        SELECT 
                            tk.TK_PassWord,
                            NVL(tk.TK_TrangThai,'PENDING') AS TrangThai,
                            tk.TK_Role,
                            tk.TK_StaffType,
                            tk.BS_MaBacSi,
                            tk.ND_IdNguoiDung
                        FROM TAIKHOAN tk
                        WHERE TRIM(UPPER(tk.TK_UserName)) = TRIM(UPPER(:pUser))";

                    using (var cmd = new OracleCommand(sql, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("pUser", user?.Trim());

                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                ViewBag.Error = "Sai tài khoản hoặc mật khẩu.";
                                return View();
                            }

                            var dbPassword = r.GetString(0)?.Trim();
                            var status = r.GetString(1)?.Trim();                // ACTIVE / PENDING / LOCKED ...
                            var role = r.IsDBNull(2) ? "" : r.GetString(2);   // ADMIN / USER
                            var staffType = r.IsDBNull(3) ? "" : r.GetString(3);   // Bác sĩ / Bệnh nhân ...
                            var bsMa = r.IsDBNull(4) ? "" : r.GetString(4);   // mã bác sĩ
                            var ndId = r.IsDBNull(5) ? "" : r.GetString(5);
                            Session["ND_IdNguoiDung"] = ndId;

                            string maBenhNhan = null;
                            using (var cmdBN = new OracleCommand("SELECT BN_MaBenhNhan FROM BENHNHAN WHERE ND_IdNguoiDung = :ndid", conn))
                            {
                                cmdBN.Parameters.Add(":ndid", ndId);
                                var result = cmdBN.ExecuteScalar();
                                if (result != null)
                                    maBenhNhan = result.ToString();
                            }

                            // ✅ Gán vào session nếu có
                            if (!string.IsNullOrEmpty(maBenhNhan))
                            {
                                Session["MaBenhNhan"] = maBenhNhan;
                            }

                            // So sánh password (TODO: chuyển sang hash)
                            if (!string.Equals(dbPassword, password?.Trim()))
                            {
                                ViewBag.Error = "Sai tài khoản hoặc mật khẩu.";
                                return View();
                            }

                            // Chặn tài khoản chưa ACTIVE
                            if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (status.Equals("PENDING", StringComparison.OrdinalIgnoreCase))
                                    ViewBag.Error = "Tài khoản đang chờ duyệt. Vui lòng đợi Admin phê duyệt.";
                                else if (status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase))
                                    ViewBag.Error = "Tài khoản đã bị khoá. Vui lòng liên hệ quản trị.";
                                else
                                    ViewBag.Error = $"Tài khoản chưa sẵn sàng (trạng thái: {status}).";
                                return View();
                            }

                            // ==== SET SESSION CHUẨN ====
                            Session["User"] = user?.Trim();
                            Session["TK_Role"] = role;
                            Session["Role"] = role;       // backward-compat nếu nơi khác dùng "Role"
                            Session["TK_StaffType"] = staffType;
                            Session["StaffType"] = staffType;  // backward-compat
                            Session["StaffTypeRaw"] = staffType;
                            Session["BS_MaBacSi"] = bsMa;
                            Session["ND_IdNguoiDung"] = ndId;
                            Session["TK_TrangThai"] = status;

                            // ================== ĐIỀU HƯỚNG ==================

                            // 1) Nếu có returnUrl hợp lệ -> quay lại (nếu bạn muốn ép bác sĩ vào Dashboard, đưa check bác sĩ lên trước)
                            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                                return Redirect(returnUrl);

                            // 2) Admin -> Admin
                            if (role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))
                                return RedirectToAction("Home", "Admin"); // hoặc "Dashboard"

                            // 3) User + Bác sĩ -> Doctor
                            if (role.Equals("USER", StringComparison.OrdinalIgnoreCase) && IsDoctor(staffType))
                            {
                                if (string.IsNullOrWhiteSpace(bsMa))
                                {
                                    ViewBag.Error = "Tài khoản bác sĩ chưa liên kết BS_MaBacSi. Vui lòng liên hệ quản trị.";
                                    return View();
                                }
                                return RedirectToAction("Dashboard", "Doctor");
                            }

                            // 4) Mặc định -> Home
                            return RedirectToAction("Index", "Home");
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                ViewBag.Error = $"Lỗi Oracle ORA-{ex.Number}: {ex.Message}";
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Lỗi: " + ex.Message;
                return View();
            }
        }

        // Nhận diện "Bác sĩ" / "Doctor" (khử dấu + chỉ giữ ký tự chữ)
        private static bool IsDoctor(string staffType)
        {
            if (string.IsNullOrWhiteSpace(staffType)) return false;
            var s = RemoveDiacritics(staffType).ToUpperInvariant();
            s = new string(s.Where(char.IsLetter).ToArray());
            return s == "BACSI" || s == "DOCTOR";
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




        [HttpGet]
        public ActionResult TestConnection()
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"]?.ConnectionString;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                using (var cmd = new OracleCommand(
                    "SELECT USER, SYS_CONTEXT('USERENV','DB_NAME') FROM dual", conn))
                using (var r = cmd.ExecuteReader())
                {
                    r.Read();
                    return Content($"USER={r.GetString(0)}, DB={r.GetString(1)}");
                }
            }
        }

        private static string BuildId(string prefix, decimal nextVal)
            => prefix + nextVal.ToString("00000000");

        private static string MapRole(string staffType)
            => string.Equals(staffType, "BacSi", StringComparison.OrdinalIgnoreCase) ? "DOCTOR" : "USER";

        // GET: Account/Register
        [HttpGet, AllowAnonymous]
        public ActionResult Register()
        {
            // giữ default nếu bạn cần
            var model = new TaiKhoan { StaffType = "BenhNhan", Role = "USER" };

            // nhận cờ/thông điệp từ TempData (sau khi Redirect)
            ViewBag.PendingOk = TempData["PendingOk"] as bool?;
            ViewBag.PendingMsg = TempData["PendingMsg"] as string;
            ViewBag.PendingPhone = TempData["PendingPhone"] as string;

            return View(model);
        }

        // POST: Account/Register  
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Register(TaiKhoan model)
        {
            // Lấy các field KHÔNG có trong TaiKhoan từ form
            var confirmPassword = Request.Form["ConfirmPassword"];
            var email = Request.Form["Email"];
            var phoneNumber = Request.Form["PhoneNumber"];
            var address = Request.Form["Address"];
            DateTime? dateOfBirth = null;
            if (DateTime.TryParse(Request.Form["DateOfBirth"], out var dobVal)) dateOfBirth = dobVal;

            // Validate cơ bản
            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("", "Vui lòng nhập Username và Password.");
                return View(model);
            }
            if (!string.Equals(model.Password?.Trim(), confirmPassword?.Trim()))
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp.");
                return View(model);
            }

            // Chỉ cho phép đăng ký Bệnh nhân; Role mặc định USER
            model.StaffType = "BenhNhan";
            model.Role = "USER";

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"]?.ConnectionString;

            try
            {
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        try
                        {
                            // 0) Check username đã tồn tại trong TAIKHOAN (đã Active/Pending/Locked gì cũng tính)
                            using (var cmd = new OracleCommand(@"
                        SELECT COUNT(*) FROM TAIKHOAN 
                        WHERE TRIM(UPPER(TK_UserName)) = TRIM(UPPER(:u))", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("u", model.Username?.Trim());
                                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                                {
                                    ModelState.AddModelError("", "Username đã tồn tại trong hệ thống.");
                                    tx.Rollback();
                                    return View(model);
                                }
                            }

                            // 0.1) Check username đã nằm trong hàng chờ PENDING_TAIKHOAN chưa
                            using (var cmd = new OracleCommand(@"
                        SELECT COUNT(*) FROM PENDING_TAIKHOAN
                        WHERE TRIM(UPPER(PT_UserName)) = TRIM(UPPER(:u))", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("u", model.Username?.Trim());
                                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                                {
                                    ModelState.AddModelError("", "Username này đang chờ duyệt, vui lòng đợi Admin phê duyệt.");
                                    tx.Rollback();
                                    return View(model);
                                }
                            }

                            // 1) Tạo mã yêu cầu mới cho bảng PENDING_TAIKHOAN (PT_MaYeuCau)
                            string reqId = NextId(conn, tx, "PENDING_TAIKHOAN", "PT_MaYeuCau", "PT");

                            // 2) Ghi vào bảng chờ duyệt
                            using (var ins = new OracleCommand(@"
                        INSERT INTO PENDING_TAIKHOAN
                        (PT_MaYeuCau, PT_UserName, PT_PassWord, PT_Email, PT_SoDienThoai, PT_NgaySinh, PT_DiaChi, PT_Stafftype, PT_NgayYeuCau)
                        VALUES
                        (:id, :u, :p, :email, :sdt, :dob, :addr, :st, SYSDATE)", conn))
                            {
                                ins.Transaction = tx;
                                ins.BindByName = true;
                                ins.Parameters.Add("id", reqId);
                                ins.Parameters.Add("u", model.Username?.Trim());
                                ins.Parameters.Add("p", model.Password?.Trim()); // TODO: mã hoá mật khẩu sau
                                ins.Parameters.Add("email", (object)email ?? DBNull.Value);
                                ins.Parameters.Add("sdt", (object)phoneNumber ?? DBNull.Value);
                                ins.Parameters.Add("dob", (object)dateOfBirth ?? DBNull.Value);
                                ins.Parameters.Add("addr", (object)address ?? DBNull.Value);
                                ins.Parameters.Add("st", model.StaffType); // luôn 'BenhNhan'
                                ins.ExecuteNonQuery();
                            }

                            tx.Commit();
                            // Gửi thông điệp qua TempData để hiển thị ở Register.cshtml
                            TempData["PendingOk"] = true;
                            TempData["PendingMsg"] = "Tài khoản của bạn đã được gửi để chờ xác minh. Khi xác minh xong chúng tôi sẽ gửi thông báo về số điện thoại bạn đã cung cấp.";
                            TempData["PendingPhone"] = MaskPhone(phoneNumber);

                            // Redirect về GET Register (PRG) để tránh F5 tạo trùng
                            return RedirectToAction("Register");
                        }
                        catch (Exception ex)
                        {
                            tx.Rollback();
                            ModelState.AddModelError("", "Lỗi khi lưu yêu cầu: " + ex.Message);
                            return View(model);
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                ModelState.AddModelError("", $"Kết nối Oracle lỗi ORA-{ex.Number}: {ex.Message}");
                return View(model);
            }
        }
        private static string MaskPhone(string s) // ẩn bớt số
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var digits = new string(s.Where(char.IsDigit).ToArray());
            if (digits.Length <= 4) return s;
            var prefix = digits.Substring(0, Math.Min(3, digits.Length));
            var suffix = digits.Substring(digits.Length - 2);
            return $"{prefix}******{suffix}";
        }




        // Sinh mã kế tiếp
        private static string NextId(
            OracleConnection conn, OracleTransaction tx,
            string table, string idColumn, string prefix)
        {
            using (var lockCmd = new OracleCommand($"LOCK TABLE {table} IN EXCLUSIVE MODE", conn))
            {
                lockCmd.Transaction = tx;
                lockCmd.ExecuteNonQuery();
            }

            var sqlMax = $@"
                SELECT NVL(MAX(TO_NUMBER(SUBSTR({idColumn}, -8))), 0)
                FROM {table}";
            decimal maxTail;
            using (var cmd = new OracleCommand(sqlMax, conn))
            {
                cmd.Transaction = tx;
                var ret = cmd.ExecuteScalar();
                maxTail = Convert.ToDecimal(ret);
            }

            var next = maxTail + 1;
            return prefix + next.ToString("00000000");
        }






        public ActionResult Logout()
        {
            // Xoá thông tin đăng nhập trên Session
            Session.Clear();
            Session.Abandon();

            // (tuỳ chọn) đăng xuất FormsAuthentication nếu có dùng
            try { FormsAuthentication.SignOut(); } catch { }

            // (tuỳ chọn) xoá cookie session cũ
            Response.Cookies.Add(new HttpCookie("ASP.NET_SessionId", "")
            {
                Expires = DateTime.UtcNow.AddDays(-1)
            });

            TempData["Msg"] = "Bạn đã đăng xuất.";
            return RedirectToAction("Login", "Account");
        }



        // Hiển thị form quên mật khẩu
        [HttpGet, AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // Xử lý đổi mật khẩu khi quên
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string user, string email, string phone,
                                   string newPassword, string confirmPassword)
        {
            // 1) Validate cơ bản
            if (string.IsNullOrWhiteSpace(user))
            {
                ViewBag.Error = "Vui lòng nhập tài khoản (Username).";
                return View();
            }
            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            {
                ViewBag.Error = "Nhập email hoặc số điện thoại đã đăng ký để xác minh.";
                return View();
            }
            if (string.IsNullOrWhiteSpace(newPassword) ||
                !string.Equals(newPassword?.Trim(), confirmPassword?.Trim()))
            {
                ViewBag.Error = "Mật khẩu mới và xác nhận không khớp.";
                return View();
            }

            // Hàm chuẩn hoá
            string N(string s) => (s ?? "").Trim();
            string NormPhone(string s) => Regex.Replace(N(s), "[^0-9]", ""); // bỏ mọi ký tự không phải số

            try
            {
                var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();

                    // 2) Lấy thông tin email/phone theo username
                    const string sqlFind = @"
                                                SELECT tk.TK_MaTK,
                                                       NVL(tk.TK_TrangThai,'PENDING') AS TrangThai,
                                                       nd.ND_Email,
                                                       nd.ND_SoDienThoai
                                                FROM   TAIKHOAN tk
                                                JOIN   NGUOIDUNG nd ON nd.ND_IdNguoiDung = tk.ND_IdNguoiDung
                                                WHERE  TRIM(UPPER(tk.TK_UserName)) = TRIM(UPPER(:u))";

                    string tkId = null, status = null, dbEmail = null, dbPhone = null;

                    using (var cmd = new OracleCommand(sqlFind, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("u", N(user).ToUpperInvariant());
                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                ViewBag.Error = "Không tìm thấy tài khoản.";
                                return View();
                            }
                            tkId = r["TK_MaTK"]?.ToString();
                            status = r["TrangThai"]?.ToString();
                            dbEmail = r["ND_Email"]?.ToString();
                            dbPhone = r["ND_SoDienThoai"]?.ToString();
                        }
                    }

                    // 3) (Tùy chọn) ràng buộc trạng thái
                    // if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                    // {
                    //     ViewBag.Error = "Tài khoản chưa sẵn sàng (không ở trạng thái ACTIVE).";
                    //     return View();
                    // }

                    // 4) So khớp theo trường người dùng đã nhập
                    bool emailProvided = !string.IsNullOrWhiteSpace(email);
                    bool phoneProvided = !string.IsNullOrWhiteSpace(phone);

                    bool okEmail = !emailProvided
                                   || string.Equals(N(dbEmail).ToUpperInvariant(),
                                                    N(email).ToUpperInvariant());
                    bool okPhone = !phoneProvided
                                   || string.Equals(NormPhone(dbPhone), NormPhone(phone));

                    if (!okEmail || !okPhone)
                    {
                        // Gợi ý lỗi rõ ràng hơn
                        if (emailProvided && !okEmail && phoneProvided && !okPhone)
                            ViewBag.Error = "Email và số điện thoại xác minh đều không khớp.";
                        else if (emailProvided && !okEmail)
                            ViewBag.Error = "Email xác minh không khớp.";
                        else if (phoneProvided && !okPhone)
                            ViewBag.Error = "Số điện thoại xác minh không khớp.";
                        else
                            ViewBag.Error = "Thông tin xác minh không khớp.";
                        return View();
                    }

                    // 5) Cập nhật mật khẩu mới (TODO: hash)
                    const string sqlUpdate = @"UPDATE TAIKHOAN SET TK_PassWord = :p WHERE TK_MaTK = :id";
                    using (var up = new OracleCommand(sqlUpdate, conn))
                    {
                        up.BindByName = true;
                        up.Parameters.Add("p", N(newPassword));
                        up.Parameters.Add("id", tkId);
                        up.ExecuteNonQuery();
                    }
                }

                TempData["Msg"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login");
            }
            catch (OracleException ex)
            {
                ViewBag.Error = $"Lỗi Oracle ORA-{ex.Number}: {ex.Message}";
                return View();
            }
            catch (System.Exception ex)
            {
                ViewBag.Error = "Lỗi: " + ex.Message;
                return View();
            }
        }


    }
}