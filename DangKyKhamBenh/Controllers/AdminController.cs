using DangKyKhamBenh.Filters;                       // Dùng attribute [AdminOnly] để khóa controller cho ADMIN
using DangKyKhamBenh.Models.ViewModels;
using System.Data;
using Oracle.ManagedDataAccess.Client;              
using System;
using System.Collections.Generic;
using System.Configuration;                         
using System.Web.Mvc;

namespace DangKyKhamBenh.Controllers
{
    [AdminOnly]   // Chỉ ADMIN (Session["Role"] == "ADMIN") mới vào được toàn bộ controller này
    public class AdminController : Controller
    {

        [HttpGet]
        public ActionResult Home()
        {
            ViewBag.Active = "Home";   // để highlight menu
            return View();
        }



        // ====== A) DANH SÁCH YÊU CẦU CHỜ DUYỆT ======
        [AdminOnly]
        public ActionResult Pending()
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            var tb = new DataTable();

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();

                var sql = @"
            SELECT
                PT_MaYeuCau       AS TK_MaTK,
                PT_UserName       AS TK_UserName,
                PT_Stafftype      AS TK_StaffType,
                'USER'            AS TK_Role,
                'PENDING'         AS TrangThai,
                PT_NgayYeuCau     AS TK_NgayTao
            FROM PENDING_TAIKHOAN
            ORDER BY PT_NgayYeuCau DESC";

                using (var cmd = new OracleCommand(sql, conn))
                using (var r = cmd.ExecuteReader())
                {
                    tb.Load(r);
                }
            }

            return View(tb);  // Pending.cshtml: @model System.Data.DataTable
        }


        // ====== B) PHÊ DUYỆT (APPROVE) 1 YÊU CẦU ======
        [HttpPost]   // Action POST: admin bấm Duyệt 
        [ValidateAntiForgeryToken] // Chống CSRF
        public ActionResult Approve(string id)// id = PT_MaYeuCau (vd: RQ00000001)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Err"] = "Thiếu mã yêu cầu.";
                return RedirectToAction("Pending");
            }

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();  // Mở kết nối
                using (var tx = conn.BeginTransaction()) // Bắt đầu transaction để đảm bảo all-or-nothing
                {
                    try
                    {
                        // 1) Lấy dữ liệu từ bảng chờ (đang duyệt)
                        string u, p, email, phone, addr, staff; DateTime? dob = null; // // Biến tạm giữ giá trị
                        using (var getCmd = new OracleCommand(@"
                                    SELECT PT_UserName, PT_PassWord, PT_Email, PT_SoDienThoai, PT_NgaySinh, PT_DiaChi, PT_Stafftype
                                    FROM   PENDING_TAIKHOAN
                                    WHERE  PT_MaYeuCau = :id", conn))
                        {
                            getCmd.Transaction = tx; // Gắn cùng transaction
                            getCmd.BindByName = true;
                            getCmd.Parameters.Add("id", id);

                            using (var r = getCmd.ExecuteReader())
                            {
                                if (!r.Read()) // Không có bản ghi tương ứng thì báo lỗi
                                {
                                    tx.Rollback();
                                    TempData["Err"] = "Không tìm thấy yêu cầu.";
                                    return RedirectToAction("Pending");
                                }

                                u = r.GetString(0); // PT_UserName
                                p = r.GetString(1); // PT_PassWord (chưa hash theo thiết kế ban đầu)
                                email = r.IsDBNull(2) ? null : r.GetString(2);
                                phone = r.IsDBNull(3) ? null : r.GetString(3);
                                dob = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
                                addr = r.IsDBNull(5) ? null : r.GetString(5);
                                staff = r.IsDBNull(6) ? "BenhNhan" : r.GetString(6);// Mặc định BenhNhan
                            }
                        }

                        // 2) Kiểm tra trùng username ở bảng TAIKHOAN
                        using (var check = new OracleCommand(@"
                                          SELECT COUNT(*) FROM TAIKHOAN WHERE UPPER(TRIM(TK_UserName)) = UPPER(TRIM(:u))", conn))
                        {
                            check.Transaction = tx;
                            check.BindByName = true;
                            check.Parameters.Add("u", u);
                            var exists = Convert.ToInt32(check.ExecuteScalar()) > 0;// true nếu đã tồn tại
                            if (exists)
                            {
                                tx.Rollback();
                                TempData["Err"] = "Username đã tồn tại, không thể duyệt.";
                                return RedirectToAction("Pending");
                            }
                        }

                        // 3) Tạo NGUOIDUNG
                        var ndId = NextId(conn, tx, "NGUOIDUNG", "ND_IdNguoiDung", "ND");// Sinh mã ND00000001
                        using (var insND = new OracleCommand(@"
                                    INSERT INTO NGUOIDUNG
                                    (ND_IdNguoiDung, ND_HoTen, ND_SoDienThoai, ND_Email, ND_NgaySinh, ND_DiaChiThuongChu)
                                    VALUES (:id, :hoten, :sdt, :email, :dob, :addr)", conn))
                        {
                            insND.Transaction = tx;
                            insND.BindByName = true;
                            insND.Parameters.Add("id", ndId);
                            insND.Parameters.Add("hoten", (object)u ?? DBNull.Value);
                            insND.Parameters.Add("sdt", (object)phone ?? DBNull.Value);
                            insND.Parameters.Add("email", (object)email ?? DBNull.Value);
                            insND.Parameters.Add("dob", (object)dob ?? DBNull.Value);
                            insND.Parameters.Add("addr", (object)addr ?? DBNull.Value);
                            insND.ExecuteNonQuery();         // // Thêm người dùng cơ bản
                        }

                        // 4) Luôn tạo BỆNH NHÂN (theo yêu cầu hiện tại chỉ duyệt BN)
                        var bnId = NextId(conn, tx, "BENHNHAN", "BN_MaBenhNhan", "BN"); // // Mã BN
                        using (var insBN = new OracleCommand(@"
                            INSERT INTO BENHNHAN (BN_MaBenhNhan, BN_SoBaoHiemYT, BN_NhomMau, BN_TieuSuBenhAn, ND_IdNguoiDung)
                            VALUES (:id, :bh, :nhom, :tieuSu, :nd)", conn))
                        {
                            insBN.Transaction = tx;
                            insBN.BindByName = true;
                            insBN.Parameters.Add("id", bnId);
                            insBN.Parameters.Add("bh", DBNull.Value);
                            insBN.Parameters.Add("nhom", DBNull.Value);
                            insBN.Parameters.Add("tieuSu", DBNull.Value);
                            insBN.Parameters.Add("nd", ndId);
                            insBN.ExecuteNonQuery();         // // Thêm thông tin bệnh nhân
                        }

                        // 5) Tạo TÀI KHOẢN ACTIVE cho BN
                        var tkId = NextId(conn, tx, "TAIKHOAN", "TK_MaTK", "TK"); // // Mã TK
                        using (var insTK = new OracleCommand(@"
                                INSERT INTO TAIKHOAN
                                (TK_MaTK, TK_UserName, TK_PassWord, TK_Role, TK_TrangThai, TK_StaffType,
                                 BN_MaBenhNhan, BS_MaBacSi, ND_IdNguoiDung)
                                VALUES
                                (:tk, :u, :p, :r, :tt, :st, :bn, :bs, :nd)", conn))
                        {
                            insTK.Transaction = tx;
                            insTK.BindByName = true;
                            insTK.Parameters.Add("tk", tkId);
                            insTK.Parameters.Add("u", u.Trim());
                            insTK.Parameters.Add("p", p.Trim());          // // TODO: sau này hash
                            insTK.Parameters.Add("r", "USER");            // // BN → role USER
                            insTK.Parameters.Add("tt", "ACTIVE");         // // Trạng thái ACTIVE ngay khi duyệt
                            insTK.Parameters.Add("st", "Bệnh nhân");      // // StaffType hiển thị
                            insTK.Parameters.Add("bn", bnId);             // // Liên kết BN
                            insTK.Parameters.Add("bs", DBNull.Value);     // // Không phải bác sĩ
                            insTK.Parameters.Add("nd", ndId);             // // Liên kết NGUOIDUNG
                            insTK.ExecuteNonQuery();                       // // Tạo tài khoản
                        }

                        // 6) Xóa bản ghi khỏi hàng chờ
                        using (var del = new OracleCommand("DELETE FROM PENDING_TAIKHOAN WHERE PT_MaYeuCau = :id", conn))
                        {
                            del.Transaction = tx;
                            del.BindByName = true;
                            del.Parameters.Add("id", id);
                            del.ExecuteNonQuery(); // Xóa yêu cầu đã xử lý
                        }

                        tx.Commit();// Tất cả OK → commit
                        TempData["Msg"] = "Duyệt thành công. Tài khoản đã ACTIVE.";
                        return RedirectToAction("Pending"); // Quay về danh sách
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();  // Lỗi → rollback toàn bộ
                        TempData["Err"] = "Lỗi duyệt: " + ex.Message;
                        return RedirectToAction("Pending");
                    }
                }
            }
        }

        // ====== C) TỪ CHỐI (REJECT) 1 YÊU CẦU ======
        [HttpPost]  // Action POST: admin bấm Từ chối → chỉ cần xóa khỏi hàng chờ
        [ValidateAntiForgeryToken]
        public ActionResult Reject(string id)// id = PT_MaYeuCau
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Err"] = "Thiếu mã yêu cầu.";
                return RedirectToAction("Pending");
            }

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                using (var cmd = new OracleCommand("DELETE FROM PENDING_TAIKHOAN WHERE PT_MaYeuCau = :id", conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("id", id);
                    var rows = cmd.ExecuteNonQuery();       // // rows = số bản ghi xóa
                    TempData["Msg"] = rows > 0 ? "Đã từ chối yêu cầu." : "Không tìm thấy yêu cầu.";
                    return RedirectToAction("Pending");
                }
            }
        }

        // ====== D) LỊCH SỬ YÊU CẦU ĐĂNG KÝ ======
        [AdminOnly]
        public ActionResult History(DateTime? from = null, DateTime? to = null)
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            var tb = new System.Data.DataTable();

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                var sql = @"
            SELECT
                PT_MaYeuCau   AS TK_MaTK,
                PT_UserName   AS TK_UserName,
                PT_Stafftype  AS TK_StaffType,
                'USER'        AS TK_Role,
                'PENDING'     AS TrangThai,
                PT_NgayYeuCau AS TK_NgayTao
            FROM PENDING_TAIKHOAN
            WHERE (:d1 IS NULL OR PT_NgayYeuCau >= :d1)
              AND (:d2 IS NULL OR PT_NgayYeuCau <= :d2)
            ORDER BY PT_NgayYeuCau DESC";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    // Tránh ORA-00932: set đúng kiểu DATE, cho phép null
                    cmd.Parameters.Add("d1", OracleDbType.Date).Value = (object)from ?? DBNull.Value;
                    cmd.Parameters.Add("d2", OracleDbType.Date).Value = (object)to ?? DBNull.Value;

                    using (var r = cmd.ExecuteReader())
                        tb.Load(r);
                }
            }

            return View(tb); // History.cshtml: @model System.Data.DataTable
        }



        // ====== E) TẠO TÀI KHOẢN BÁC SĨ (ADMIN TỰ TẠO) ======
        [HttpGet] // Form nhập thông tin bác sĩ
        public ActionResult CreateDoctor()
        {
            return View();   // Trả view CreateDoctor.cshtml (form)
        }

        [HttpPost]     // Nhận dữ liệu form tạo bác sĩ
        [ValidateAntiForgeryToken]
        public ActionResult CreateDoctor(string username, string password, string fullName,
                                         string chuyenKhoa, string chucDanh, int? namKinhNghiem,
                                         string email, string phone, DateTime? ngaySinh, string diaChi)
        {
            // Validate tối thiểu:
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Err"] = "Vui lòng nhập Username và Password.";
                return RedirectToAction("CreateDoctor");
            }

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        // Kiểm tra trùng username
                        using (var check = new OracleCommand(@"
                                SELECT COUNT(*) FROM TAIKHOAN WHERE UPPER(TRIM(TK_UserName)) = UPPER(TRIM(:u))", conn))
                        {
                            check.Transaction = tx;
                            check.BindByName = true;
                            check.Parameters.Add("u", username);
                            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                            {
                                tx.Rollback();
                                TempData["Err"] = "Username đã tồn tại.";
                                return RedirectToAction("CreateDoctor");
                            }
                        }

                        // 1) NGUOIDUNG
                        var ndId = NextId(conn, tx, "NGUOIDUNG", "ND_IdNguoiDung", "ND");
                        using (var insND = new OracleCommand(@"
                                INSERT INTO NGUOIDUNG (ND_IdNguoiDung, ND_HoTen, ND_SoDienThoai, ND_Email, ND_NgaySinh, ND_DiaChiThuongChu)
                                VALUES (:id, :hoten, :sdt, :email, :dob, :addr)", conn))
                        {
                            insND.Transaction = tx;
                            insND.BindByName = true;
                            insND.Parameters.Add("id", ndId);
                            insND.Parameters.Add("hoten", (object)fullName ?? username);
                            insND.Parameters.Add("sdt", (object)phone ?? DBNull.Value);
                            insND.Parameters.Add("email", (object)email ?? DBNull.Value);
                            insND.Parameters.Add("dob", (object)ngaySinh ?? DBNull.Value);
                            insND.Parameters.Add("addr", (object)diaChi ?? DBNull.Value);
                            insND.ExecuteNonQuery();
                        }

                        // 2) BÁC SĨ
                        var bsId = NextId(conn, tx, "BACSI", "BS_MaBacSi", "BS");
                        using (var insBS = new OracleCommand(@"
                                INSERT INTO BACSI (BS_MaBacSi, BS_ChuyenKhoa, BS_ChucDanh, BS_NamKinhNghiem, ND_IdNguoiDung)
                                VALUES (:id, :ck, :cd, :nam, :nd)", conn))
                        {
                            insBS.Transaction = tx;
                            insBS.BindByName = true;
                            insBS.Parameters.Add("id", bsId);
                            insBS.Parameters.Add("ck", (object)chuyenKhoa ?? "Tổng quát");
                            insBS.Parameters.Add("cd", (object)chucDanh ?? "BS");
                            insBS.Parameters.Add("nam", (object)namKinhNghiem ?? 0);
                            insBS.Parameters.Add("nd", ndId);
                            insBS.ExecuteNonQuery();
                        }

                        // 3) TÀI KHOẢN: DOCTOR + ACTIVE
                        var tkId = NextId(conn, tx, "TAIKHOAN", "TK_MaTK", "TK");
                        using (var insTK = new OracleCommand(@"
                                    INSERT INTO TAIKHOAN
                                    (TK_MaTK, TK_UserName, TK_PassWord, TK_Role, TK_TrangThai, TK_StaffType,
                                     BN_MaBenhNhan, BS_MaBacSi, ND_IdNguoiDung)
                                    VALUES
                                    (:tk, :u, :p, :r, :tt, :st, :bn, :bs, :nd)", conn))
                        {
                            insTK.Transaction = tx;
                            insTK.BindByName = true;
                            insTK.Parameters.Add("tk", tkId);
                            insTK.Parameters.Add("u", username.Trim());
                            insTK.Parameters.Add("p", password.Trim()); // TODO: hash mật khẩu
                            insTK.Parameters.Add("r", "DOCTOR");  // Role bác sĩ
                            insTK.Parameters.Add("tt", "ACTIVE");  // Active ngay
                            insTK.Parameters.Add("st", "Bác sĩ");   // StaffType hiển thị
                            insTK.Parameters.Add("bn", DBNull.Value);   // Không phải BN
                            insTK.Parameters.Add("bs", bsId);  // Liên kết BS
                            insTK.Parameters.Add("nd", ndId);   // Liên kết ND
                            insTK.ExecuteNonQuery();
                        }

                        tx.Commit(); // Thành công → commit
                        TempData["Msg"] = "Tạo bác sĩ thành công.";
                        return RedirectToAction("CreateDoctor"); // Quay lại form (hoặc chuyển sang danh sách bác sĩ tùy bạn)
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();  // Lỗi → rollback
                        TempData["Err"] = "Lỗi tạo bác sĩ: " + ex.Message;
                        return RedirectToAction("CreateDoctor");
                    }
                }
            }
        }

        // ====== HÀM PHỤ: Sinh mã ID theo prefix ======
        private static string NextId(OracleConnection conn, OracleTransaction tx, string table, string idColumn, string prefix)
        {   // Khoá bảng để tránh race-condition khi nhiều admin duyệt cùng lúc
            using (var lockCmd = new OracleCommand($"LOCK TABLE {table} IN EXCLUSIVE MODE", conn))
            {
                lockCmd.Transaction = tx;
                lockCmd.ExecuteNonQuery();
            }

            // Lấy max 8 chữ số đuôi của cột idColumn (định dạng PREFIX########)
            var sqlMax = $@"SELECT NVL(MAX(TO_NUMBER(SUBSTR({idColumn}, -8))), 0) FROM {table}";
            decimal maxTail;
            using (var cmd = new OracleCommand(sqlMax, conn))
            {
                cmd.Transaction = tx;
                var ret = cmd.ExecuteScalar();
                maxTail = Convert.ToDecimal(ret);
            }

            // +1 và ghép lại theo format
            var next = maxTail + 1;
            return prefix + next.ToString("00000000");
        }

        // ==============================================================
        // GET: /Admin/EditDoctor/BS00000001
        [HttpGet]
        public ActionResult EditDoctor(string id)
        {
            var vm = new BacSi();
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                var sql = @"
                SELECT b.BS_MaBacSi, b.BS_ChuyenKhoa, b.BS_ChucDanh, b.BS_NamKinhNghiem,
                       nd.ND_HoTen, nd.ND_SoDienThoai, nd.ND_Email, nd.ND_NgaySinh, nd.ND_DiaChiThuongChu,
                       tk.TK_UserName, NVL(tk.TK_TrangThai,'ACTIVE') as TrangThai
                  FROM BACSI b
                  JOIN NGUOIDUNG nd ON nd.ND_IdNguoiDung = b.ND_IdNguoiDung
                  JOIN TAIKHOAN tk ON tk.BS_MaBacSi = b.BS_MaBacSi
                 WHERE b.BS_MaBacSi = :id";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("id", id);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return HttpNotFound();

                        vm.MaBacSi = r.GetString(0);
                        vm.ChuyenKhoa = r.IsDBNull(1) ? null : r.GetString(1);
                        vm.ChucDanh = r.IsDBNull(2) ? null : r.GetString(2);
                        vm.NamKinhNghiem = r.IsDBNull(3) ? (int?)null : Convert.ToInt32(r.GetDecimal(3));
                        vm.HoTen = r.IsDBNull(4) ? null : r.GetString(4);
                        vm.SoDienThoai = r.IsDBNull(5) ? null : r.GetString(5);
                        vm.Email = r.IsDBNull(6) ? null : r.GetString(6);
                        vm.NgaySinh = r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7);
                        vm.DiaChi = r.IsDBNull(8) ? null : r.GetString(8);
                        vm.UserName = r.GetString(9);
                        vm.TrangThai = r.GetString(10);
                    }
                }
            }

            ViewBag.Title = "Sửa bác sĩ";
            ViewBag.Active = "CreateDoctor"; // hoặc "Doctors"
            return View(vm);                  // Views/Admin/EditDoctor.cshtml
        }

        // POST: /Admin/EditDoctor
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult EditDoctor(BacSi vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        // Update BACSI
                        using (var cmd = new OracleCommand(@"
                        UPDATE BACSI
                           SET BS_ChuyenKhoa   = :ck,
                               BS_ChucDanh     = :cd,
                               BS_NamKinhNghiem= :nam
                         WHERE BS_MaBacSi     = :id", conn))
                        {
                            cmd.Transaction = tx;
                            cmd.BindByName = true;
                            cmd.Parameters.Add("ck", (object)vm.ChuyenKhoa ?? DBNull.Value);
                            cmd.Parameters.Add("cd", (object)vm.ChucDanh ?? DBNull.Value);
                            cmd.Parameters.Add("nam", (object)vm.NamKinhNghiem ?? DBNull.Value);
                            cmd.Parameters.Add("id", vm.MaBacSi);
                            cmd.ExecuteNonQuery();
                        }

                        // Update NGUOIDUNG
                        using (var cmd = new OracleCommand(@"
                        UPDATE NGUOIDUNG nd
                           SET nd.ND_HoTen          = :ten,
                               nd.ND_SoDienThoai    = :sdt,
                               nd.ND_Email          = :email,
                               nd.ND_NgaySinh       = :ns,
                               nd.ND_DiaChiThuongChu= :dc
                         WHERE nd.ND_IdNguoiDung = (
                               SELECT ND_IdNguoiDung FROM BACSI WHERE BS_MaBacSi=:id)", conn))
                        {
                            cmd.Transaction = tx;
                            cmd.BindByName = true;
                            cmd.Parameters.Add("ten", (object)vm.HoTen ?? DBNull.Value);
                            cmd.Parameters.Add("sdt", (object)vm.SoDienThoai ?? DBNull.Value);
                            cmd.Parameters.Add("email", (object)vm.Email ?? DBNull.Value);
                            cmd.Parameters.Add("ns", (object)vm.NgaySinh ?? DBNull.Value);
                            cmd.Parameters.Add("dc", (object)vm.DiaChi ?? DBNull.Value);
                            cmd.Parameters.Add("id", vm.MaBacSi);
                            cmd.ExecuteNonQuery();
                        }

                        // Update TAIKHOAN (trạng thái, đổi pass nếu nhập)
                        using (var cmd = new OracleCommand(@"
                        UPDATE TAIKHOAN
                           SET TK_TrangThai = :tt
                         WHERE BS_MaBacSi   = :id", conn))
                        {
                            cmd.Transaction = tx;
                            cmd.BindByName = true;
                            cmd.Parameters.Add("tt", (object)vm.TrangThai ?? "ACTIVE");
                            cmd.Parameters.Add("id", vm.MaBacSi);
                            cmd.ExecuteNonQuery();
                        }

                        if (!string.IsNullOrWhiteSpace(vm.NewPassword))
                        {
                            using (var cmd = new OracleCommand(@"
                            UPDATE TAIKHOAN
                               SET TK_PassWord = :p
                             WHERE BS_MaBacSi  = :id", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("p", vm.NewPassword.Trim()); // TODO: hash
                                cmd.Parameters.Add("id", vm.MaBacSi);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        tx.Commit();
                        TempData["Msg"] = "Cập nhật bác sĩ thành công.";
                        return RedirectToAction("EditDoctor", new { id = vm.MaBacSi });
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        TempData["Err"] = "Lỗi cập nhật: " + ex.Message;
                        return View(vm);
                    }
                }
            }
        }

        [AdminOnly]
        public ActionResult Doctors(string kw = null)
        {
            ViewBag.Title = "Quản lý bác sĩ";
            ViewBag.Active = "Doctors";

            var list = new List<BacSi>();
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();

                var sql = @"
            SELECT
                bs.BS_MaBacSi           AS BS_MaBacSi,
                nd.ND_HoTen             AS ND_HoTen,
                bs.BS_ChuyenKhoa        AS BS_ChuyenKhoa,
                bs.BS_ChucDanh          AS BS_ChucDanh,
                bs.BS_NamKinhNghiem     AS BS_NamKinhNghiem,
                tk.TK_UserName          AS TK_UserName,
                NVL(tk.TK_TrangThai,'') AS TK_TrangThai,
                nd.ND_SoDienThoai       AS ND_SoDienThoai,
                nd.ND_Email             AS ND_Email
            FROM BACSI bs
            JOIN NGUOIDUNG nd ON nd.ND_IdNguoiDung = bs.ND_IdNguoiDung
            LEFT JOIN TAIKHOAN tk ON tk.BS_MaBacSi = bs.BS_MaBacSi
            WHERE ( :kw IS NULL
                 OR UPPER(bs.BS_MaBacSi) LIKE '%'||UPPER(:kw)||'%'
                 OR UPPER(nd.ND_HoTen)    LIKE '%'||UPPER(:kw)||'%'
                 OR UPPER(tk.TK_UserName) LIKE '%'||UPPER(:kw)||'%' )
            ORDER BY nd.ND_HoTen";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    // có thể truyền 1 lần; Oracle cho phép lặp lại tên tham số khi BindByName = true
                    cmd.Parameters.Add("kw", string.IsNullOrWhiteSpace(kw) ? (object)DBNull.Value : kw.Trim());

                    using (var r = cmd.ExecuteReader())
                    {
                        // lấy sẵn ordinal để tránh sai alias & tăng tốc
                        int cMaBS = r.GetOrdinal("BS_MaBacSi");
                        int cHoTen = r.GetOrdinal("ND_HoTen");
                        int cCK = r.GetOrdinal("BS_ChuyenKhoa");
                        int cCD = r.GetOrdinal("BS_ChucDanh");
                        int cKN = r.GetOrdinal("BS_NamKinhNghiem");
                        int cUser = r.GetOrdinal("TK_UserName");
                        int cTrang = r.GetOrdinal("TK_TrangThai");
                        int cSdt = r.GetOrdinal("ND_SoDienThoai");
                        int cEmail = r.GetOrdinal("ND_Email");

                        while (r.Read())
                        {
                            list.Add(new BacSi
                            {
                                MaBacSi = r.IsDBNull(cMaBS) ? null : r.GetString(cMaBS),
                                HoTen = r.IsDBNull(cHoTen) ? null : r.GetString(cHoTen),
                                ChuyenKhoa = r.IsDBNull(cCK) ? null : r.GetString(cCK),
                                ChucDanh = r.IsDBNull(cCD) ? null : r.GetString(cCD),
                                // Oracle NUMBER -> đọc decimal, rồi Convert.ToInt32
                                NamKinhNghiem = r.IsDBNull(cKN) ? (int?)null : Convert.ToInt32(r.GetDecimal(cKN)),
                                UserName = r.IsDBNull(cUser) ? null : r.GetString(cUser),
                                TrangThai = r.IsDBNull(cTrang) ? null : r.GetString(cTrang),
                                SoDienThoai = r.IsDBNull(cSdt) ? null : r.GetString(cSdt),
                                Email = r.IsDBNull(cEmail) ? null : r.GetString(cEmail),
                            });
                        }
                    }
                }
            }

            return View("Doctors", list);
        }


        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ToggleDoctorStatus(string id)
        {
            // id = BS_MaBacSi
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            try
            {
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();
                    var sql = @"
                    UPDATE TAIKHOAN
                       SET TK_TrangThai = CASE UPPER(NVL(TK_TrangThai,'PENDING'))
                                            WHEN 'ACTIVE' THEN 'LOCKED'
                                            ELSE 'ACTIVE'
                                          END
                     WHERE BS_MaBacSi = :id";
                    using (var cmd = new OracleCommand(sql, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("id", id);
                        var aff = cmd.ExecuteNonQuery();
                        TempData["Msg"] = aff > 0 ? "Đã cập nhật trạng thái tài khoản bác sĩ." : "Không tìm thấy tài khoản.";
                    }
                }
            }
            catch (OracleException ex)
            {
                TempData["Err"] = $"ORA-{ex.Number}: {ex.Message}";
            }
            return RedirectToAction("Doctors");
        }
    }
}
