using DangKyKhamBenh.Filters;                       // Dùng attribute [AdminOnly] để khóa controller cho ADMIN
using DangKyKhamBenh.Models.ViewModels;
using DangKyKhamBenh.Models;
using DangKyKhamBenh.Services;
using Oracle.ManagedDataAccess.Client;              
using System;
using System.Collections.Generic;
using System.Configuration;                         
using System.Data;
using System.Web.Mvc;
using DangKyKhamBenh.Models;
using static System.Collections.Specialized.BitVector32;

namespace DangKyKhamBenh.Controllers
{
    [AdminOnly]   // Chỉ ADMIN (Session["Role"] == "ADMIN") mới vào được toàn bộ controller này
    public class AdminController : Controller
    {
        private readonly CaesarCipher _caesarCipher;
        private readonly RsaService _rsaService;
        private readonly HybridService _hybridService;

        public AdminController()
        {
            _caesarCipher = new CaesarCipher();
            _rsaService = new RsaService();
            _hybridService = new HybridService();
        }


        [HttpGet]
        public ActionResult Home()
        {
            ViewBag.Active = "Home";
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
        [HttpPost] 
        [ValidateAntiForgeryToken] // Chống CSRF
        public ActionResult Approve(string id)
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
        public ActionResult Reject(string id)
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
                    var rows = cmd.ExecuteNonQuery();     
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
        [HttpGet]
        public ActionResult CreateDoctor()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateDoctor(string username, string password, string fullName, string chuyenKhoa)
        {
            // Validate tối thiểu
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Err"] = "Vui lòng nhập Username và Password.";
                return RedirectToAction("CreateDoctor");
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                TempData["Err"] = "Vui lòng nhập Họ tên bác sĩ.";
                return RedirectToAction("CreateDoctor");
            }

            if (string.IsNullOrWhiteSpace(chuyenKhoa))
            {
                TempData["Err"] = "Vui lòng nhập Chuyên khoa.";
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
                        string u = username.Trim();
                        string p = password.Trim();

                        // B1: Check trùng username t   heo ciphertext
                        using (var check = new OracleCommand(@"
                            SELECT COUNT(*)
                            FROM   TAIKHOAN
                            WHERE  TK_UserName = PKG_SECURITY.AES_ENCRYPT_B64(:u)", conn))
                        {
                            check.Transaction = tx;
                            check.BindByName = true;
                            check.Parameters.Add("u", u);

                            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                            {
                                tx.Rollback();
                                TempData["Err"] = "Username đã tồn tại.";
                                return RedirectToAction("CreateDoctor");
                            }
                        }

                        // B2: Sinh mã
                        string ndId = NextId(conn, tx, "NGUOIDUNG", "ND_IdNguoiDung", "ND");
                        string bsId = NextId(conn, tx, "BACSI", "BS_MaBacSi", "BS");
                        string bnId = NextId(conn, tx, "BENHNHAN", "BN_MaBenhNhan", "BN");
                        string tkId = NextId(conn, tx, "TAIKHOAN", "TK_MaTK", "TK");

                        // B3: Insert NGUOIDUNG (chỉ có họ tên, còn lại null)
                        using (var cmdNd = new OracleCommand(@"
                            INSERT INTO NGUOIDUNG
                                (ND_IdNguoiDung,
                                 ND_HoTen,
                                 ND_SoDienThoai,
                                 ND_Email,
                                 ND_NgaySinh,
                                 ND_DiaChiThuongChu)
                            VALUES
                                (:id,
                                 :hoten,
                                 NULL,
                                 NULL,
                                 NULL,
                                 NULL)", conn))
                        {
                            cmdNd.Transaction = tx;
                            cmdNd.BindByName = true;
                            cmdNd.Parameters.Add("id", ndId);
                            cmdNd.Parameters.Add("hoten", fullName.Trim());
                            cmdNd.ExecuteNonQuery();
                        }

                        // B4: Insert BACSI
                        using (var cmdBs = new OracleCommand(@"
                            INSERT INTO BACSI
                                (BS_MaBacSi,
                                 BS_ChuyenKhoa,
                                 BS_ChucDanh,
                                 BS_NamKinhNghiem,
                                 ND_IdNguoiDung)
                            VALUES
                                (:bs,
                                 :ck,
                                 :cd,
                                 :nam,
                                 :nd)", conn))
                        {
                            cmdBs.Transaction = tx;
                            cmdBs.BindByName = true;
                            cmdBs.Parameters.Add("bs", bsId);
                            cmdBs.Parameters.Add("ck", chuyenKhoa.Trim());
                            cmdBs.Parameters.Add("cd", "BS");
                            cmdBs.Parameters.Add("nam", OracleDbType.Int32).Value = 0;
                            cmdBs.Parameters.Add("nd", ndId);
                            cmdBs.ExecuteNonQuery();
                        }

                        // B5: Insert BENHNHAN dummy (nếu FK không cho NULL)
                        using (var cmdBn = new OracleCommand(@"
                            INSERT INTO BENHNHAN
                                (BN_MaBenhNhan,
                                 BN_SoBaoHiemYT,
                                 BN_NhomMau,
                                 BN_TieuSuBenhAn,
                                 ND_IdNguoiDung)
                            VALUES
                                (:bn,
                                 NULL,
                                 NULL,
                                 NULL,
                                 :nd)", conn))
                        {
                            cmdBn.Transaction = tx;
                            cmdBn.BindByName = true;
                            cmdBn.Parameters.Add("bn", bnId);
                            cmdBn.Parameters.Add("nd", ndId);
                            cmdBn.ExecuteNonQuery();
                        }

                        // B6: Insert TAIKHOAN (mã hóa username + hash password)
                        using (var cmdTk = new OracleCommand(@"
                            INSERT INTO TAIKHOAN
                                (TK_MaTK,
                                 TK_UserName,
                                 TK_PassWord,
                                 TK_Role,
                                 TK_TrangThai,
                                 TK_StaffType,
                                 BN_MaBenhNhan,
                                 BS_MaBacSi,
                                 ND_IdNguoiDung)
                            VALUES
                                (:tkId,
                                 PKG_SECURITY.AES_ENCRYPT_B64(:u),
                                 PKG_SECURITY.HASH_PASSWORD(:p),
                                 :role,
                                 :status,
                                 :st,
                                 :bn,
                                 :bs,
                                 :nd)", conn))
                        {
                            cmdTk.Transaction = tx;
                            cmdTk.BindByName = true;
                            cmdTk.Parameters.Add("tkId", tkId);
                            cmdTk.Parameters.Add("u", u);
                            cmdTk.Parameters.Add("p", p);
                            cmdTk.Parameters.Add("role", "User");
                            cmdTk.Parameters.Add("status", "ACTIVE");
                            cmdTk.Parameters.Add("st", "BacSi");
                            cmdTk.Parameters.Add("bn", bnId);
                            cmdTk.Parameters.Add("bs", bsId);
                            cmdTk.Parameters.Add("nd", ndId);
                            cmdTk.ExecuteNonQuery();
                        }

                        tx.Commit();
                        TempData["Msg"] = "Tạo tài khoản bác sĩ thành công.";
                        return RedirectToAction("CreateDoctor");
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        tx.Rollback();
                        System.Diagnostics.Debug.WriteLine("===== ARGUMENT OUT OF RANGE =====");
                        System.Diagnostics.Debug.WriteLine(ex.ToString());
                        TempData["Err"] = "Lỗi tạo bác sĩ (ArgumentOutOfRangeException): " + ex.Message;
                        return RedirectToAction("CreateDoctor");
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        System.Diagnostics.Debug.WriteLine(ex.ToString());
                        TempData["Err"] = "Lỗi tạo bác sĩ (" + ex.GetType().Name + "): " + ex.Message;
                        return RedirectToAction("CreateDoctor");
                    }
                }
            }
        }

        // ==============================================================
        // HÀM PHỤ: Sinh mã ID theo prefix (ND, BN, BS, TK)
        // ==============================================================

        private static string NextId(
            OracleConnection conn,
            OracleTransaction tx,
            string table,
            string idColumn,
            string prefix)
        {
            using (var lockCmd = new OracleCommand($"LOCK TABLE {table} IN EXCLUSIVE MODE", conn))
            {
                lockCmd.Transaction = tx;
                lockCmd.ExecuteNonQuery();
            }

            var sqlMax = $@"
                SELECT NVL(MAX(TO_NUMBER(SUBSTR({idColumn}, -8))), 0)
                FROM   {table}
                WHERE  {idColumn} LIKE :pfx";

            decimal maxTail;

            using (var cmd = new OracleCommand(sqlMax, conn))
            {
                cmd.Transaction = tx;
                cmd.BindByName = true;
                cmd.Parameters.Add("pfx", OracleDbType.Varchar2).Value = prefix + "%";

                var ret = cmd.ExecuteScalar();
                maxTail = Convert.ToDecimal(ret);
            }

            var next = maxTail + 1;
            return prefix + next.ToString("00000000");   // ND00000001, BS00000001,...
        }




        // ==============================================================
        // ====== F) SỬA THÔNG TIN BÁC SĨ ======
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

        // ====== F) DANH SÁCH BÁC SĨ ======
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


        // Quanly benh nhan
        // ====== A. Danh sách / tìm kiếm bệnh nhân ======
        [HttpGet]
        public ActionResult Patients(string kw = null, int page = 1, int pageSize = 20)
        {
            ViewBag.Title = "Quản lý bệnh nhân";
            ViewBag.Active = "Patients";
            kw = (kw ?? "").Trim();

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            var list = new List<BenhNhan>();

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();

                // Chú ý: StaffType có thể lưu 'Bệnh nhân' hoặc 'BenhNhan' tùy trước đó.
                var sql = @"
                            SELECT  bn.BN_MaBenhNhan,
                                    nd.ND_HoTen, nd.ND_SoDienThoai, nd.ND_Email, nd.ND_NgaySinh, nd.ND_DiaChiThuongChu,
                                    tk.TK_UserName, NVL(tk.TK_TrangThai, 'PENDING') AS TK_TrangThai,
                                    bn.BN_SoBaoHiemYT, bn.BN_NhomMau, bn.BN_TieuSuBenhAn
                            FROM    BENHNHAN bn
                            JOIN    NGUOIDUNG nd ON nd.ND_IdNguoiDung = bn.ND_IdNguoiDung
                            LEFT JOIN TAIKHOAN tk ON tk.BN_MaBenhNhan = bn.BN_MaBenhNhan
                            WHERE   (:kw IS NULL OR :kw = '' OR
                                    UPPER(nd.ND_HoTen)      LIKE UPPER('%' || :kw || '%') OR
                                    UPPER(tk.TK_UserName)   LIKE UPPER('%' || :kw || '%') OR
                                    UPPER(bn.BN_MaBenhNhan) LIKE UPPER('%' || :kw || '%'))
                            ORDER BY nd.ND_HoTen";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("kw", kw);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new BenhNhan
                            {
                                BN_MaBenhNhan = r["BN_MaBenhNhan"]?.ToString(),
                                ND_HoTen = r["ND_HoTen"]?.ToString(),
                                ND_SoDienThoai = r["ND_SoDienThoai"]?.ToString(),
                                ND_Email = r["ND_Email"]?.ToString(),
                                ND_NgaySinh = r.IsDBNull(r.GetOrdinal("ND_NgaySinh")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("ND_NgaySinh")),
                                ND_DiaChiThuongChu = r["ND_DiaChiThuongChu"]?.ToString(),
                                UserName = r["TK_UserName"]?.ToString(),
                                TrangThai = r["TK_TrangThai"]?.ToString(),
                                BN_SoBaoHiemYT = r["BN_SoBaoHiemYT"]?.ToString(),
                                BN_NhomMau = r["BN_NhomMau"]?.ToString(),
                                BN_TieuSuBenhAn = r["BN_TieuSuBenhAn"]?.ToString()
                            });
                        }
                    }
                }
            }

            // (tuỳ) phân trang phía server sau
            return View(list);
        }

        // ====== B. Sửa bệnh nhân (GET) ======
        [HttpGet]
        public ActionResult EditPatient(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return RedirectToAction("Patients");

            ViewBag.Title = "Sửa bệnh nhân";
            ViewBag.Active = "Patients";

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            BenhNhan bn = null;

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                var sql = @"
                            SELECT  bn.BN_MaBenhNhan,
                                    nd.ND_HoTen, nd.ND_SoDienThoai, nd.ND_Email,nd.ND_CCCD, nd.ND_NgaySinh,nd.ND_GioiTinh,nd.ND_QuocGia,nd.ND_DanToc,nd.ND_NgheNghiep,nd.ND_TinhThanh,nd.ND_QuanHuyen,nd.ND_PhuongXa,nd.ND_DiaChiThuongChu,
                                    tk.TK_UserName, NVL(tk.TK_TrangThai, 'PENDING') AS TK_TrangThai,
                                    bn.BN_SoBaoHiemYT, bn.BN_NhomMau, bn.BN_TieuSuBenhAn
                            FROM    BENHNHAN bn
                            JOIN    NGUOIDUNG nd ON nd.ND_IdNguoiDung = bn.ND_IdNguoiDung
                            LEFT JOIN TAIKHOAN tk ON tk.BN_MaBenhNhan = bn.BN_MaBenhNhan
                            WHERE   bn.BN_MaBenhNhan = :id";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("id", id);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            bn = new BenhNhan
                            {
                                BN_MaBenhNhan = r["BN_MaBenhNhan"]?.ToString(),
                                ND_HoTen = r["ND_HoTen"]?.ToString(),
                                ND_SoDienThoai = r["ND_SoDienThoai"]?.ToString(),
                                ND_Email = r["ND_Email"]?.ToString(),
                                ND_CCCD = r["ND_CCCD"]?.ToString(),
                                ND_NgaySinh = r.IsDBNull(r.GetOrdinal("ND_NgaySinh")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("ND_NgaySinh")),
                                ND_GioiTinh = r.IsDBNull(r.GetOrdinal("ND_GioiTinh")) ? (string)null : r.GetString(r.GetOrdinal("ND_GioiTinh")),
                                ND_QuocGia = r["ND_QuocGia"]?.ToString(),
                                ND_DanToc = r["ND_DanToc"]?.ToString(),
                                ND_NgheNghiep = r["ND_NgheNghiep"]?.ToString(),
                                ND_TinhThanh = r["ND_TinhThanh"]?.ToString(),
                                ND_QuanHuyen = r["ND_QuanHuyen"]?.ToString(),
                                ND_PhuongXa = r["ND_PhuongXa"]?.ToString(),
                                ND_DiaChiThuongChu = r["ND_DiaChiThuongChu"]?.ToString(),
                                UserName = r["TK_UserName"]?.ToString(),
                                TrangThai = r["TK_TrangThai"]?.ToString(),
                                BN_SoBaoHiemYT = r["BN_SoBaoHiemYT"]?.ToString(),
                                BN_NhomMau = r["BN_NhomMau"]?.ToString(),
                                BN_TieuSuBenhAn = r["BN_TieuSuBenhAn"]?.ToString()
                            };
                        }
                    }
                }
            }

            if (bn == null) return RedirectToAction("Patients");
            return View(bn);
        }

        // ====== C. Sửa bệnh nhân (POST) ======
        [HttpPost, ValidateAntiForgeryToken]
        [ValidateInput(false)]

        public ActionResult EditPatient(DangKyKhamBenh.Models.ViewModels.BenhNhan model, string action)
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            if (action == "Decrypt")
            {

                try
                {
                    // Giải mã dữ liệu Caesar
                    //model.ND_HoTen = _caesarCipher.Decrypt(model.ND_HoTen, 15);
                    model.ND_TinhThanh = _caesarCipher.Decrypt(model.ND_TinhThanh, 15);
                    model.ND_QuanHuyen = _caesarCipher.Decrypt(model.ND_QuanHuyen, 15);
                    model.ND_PhuongXa = _caesarCipher.Decrypt(model.ND_PhuongXa, 15);

                    // Giải mã dữ liệu RSA
                    try
                    {
                        model.ND_SoDienThoai = _rsaService.Decrypt(model.ND_SoDienThoai);
                        System.Diagnostics.Debug.WriteLine("Số điện thoại sau giải mã: " + model.ND_SoDienThoai);
                    }
                    catch (Exception ex)
                    {
                        TempData["Err"] = "Lỗi giải mã số điện thoại: " + ex.Message;
                        return View(model);
                    }
                    model.ND_DiaChiThuongChu = _rsaService.Decrypt(model.ND_DiaChiThuongChu);
                    model.BN_TieuSuBenhAn = _rsaService.Decrypt(model.BN_TieuSuBenhAn);

                    // Giải mã dữ liệu Hybrid (không cần gọi key từ DB)
                    model.ND_Email = _hybridService.Decrypt(model.ND_Email, model.BN_MaBenhNhan);

                    model.ND_CCCD = _hybridService.Decrypt(model.ND_CCCD, model.BN_MaBenhNhan);
                    model.BN_SoBaoHiemYT = _hybridService.Decrypt(model.BN_SoBaoHiemYT, model.BN_MaBenhNhan);

                    TempData["Msg"] = "Giải mã thành công.";
                }
                catch (Exception ex)
                {
                    TempData["Err"] = "Lỗi giải mã: " + ex.Message;
                }

                ModelState.Clear(); // Bỏ qua lỗi validation do dữ liệu đang mã hóa
                return View(model);

            }



            if (action == "Save")
            {
                if (!ModelState.IsValid)
                {
                    TempData["Err"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.";
                    return View(model);
                }


            }
            try
            {
                // ✅ Mã hóa lại trước khi lưu
                //model.ND_HoTen = _caesarCipher.Encrypt(model.ND_HoTen, 15);
                model.ND_TinhThanh = _caesarCipher.Encrypt(model.ND_TinhThanh, 15);
                model.ND_QuanHuyen = _caesarCipher.Encrypt(model.ND_QuanHuyen, 15);
                model.ND_PhuongXa = _caesarCipher.Encrypt(model.ND_PhuongXa, 15);

                model.ND_SoDienThoai = _rsaService.Encrypt(model.ND_SoDienThoai);
                model.ND_DiaChiThuongChu = _rsaService.Encrypt(model.ND_DiaChiThuongChu);
                model.BN_TieuSuBenhAn = _rsaService.Encrypt(model.BN_TieuSuBenhAn);

                model.ND_Email = _hybridService.Encrypt(model.ND_Email, model.BN_MaBenhNhan);
                model.ND_CCCD = _hybridService.Encrypt(model.ND_CCCD, model.BN_MaBenhNhan);
                model.BN_SoBaoHiemYT = _hybridService.Encrypt(model.BN_SoBaoHiemYT, model.BN_MaBenhNhan);

                try
                {
                    using (var conn = new OracleConnection(cs))
                    {
                        conn.Open();
                        using (var tx = conn.BeginTransaction())
                        {

                            // Update NGUOIDUNG qua subquery (lấy ND_IdNguoiDung từ BN)
                            var sqlND = @"
                                        UPDATE NGUOIDUNG nd
                                        SET nd.ND_HoTen          = :hoten,
                                            nd.ND_SoDienThoai    = :sdt,
                                            nd.ND_Email          = :email,
                                            nd.ND_CCCD          = :cccd,
                                            nd.ND_NgaySinh       = :ngaysinh,
                                            nd.ND_GioiTinh          = :gioitinh,
                                            nd.ND_QuocGia          = :quocgia,
                                            nd.ND_DanToc          = :dantoc,
                                            nd.ND_NgheNghiep          = :nghenghiep,
                                            nd.ND_TinhThanh          = :tinhthanh,
                                            nd.ND_QuanHuyen          = :quanhuyen,
                                            nd.ND_PhuongXa          = :phuongxa,
                                            nd.ND_DiaChiThuongChu= :diachi
                                        WHERE nd.ND_IdNguoiDung = (SELECT ND_IdNguoiDung FROM BENHNHAN WHERE BN_MaBenhNhan = :bnid)";

                            using (var cmd = new OracleCommand(sqlND, conn))
                            {
                                cmd.Transaction = tx; cmd.BindByName = true;
                                cmd.Parameters.Add("hoten", (object)model.ND_HoTen ?? DBNull.Value);
                                cmd.Parameters.Add("sdt", (object)model.ND_SoDienThoai ?? DBNull.Value);
                                cmd.Parameters.Add("email", (object)model.ND_Email ?? DBNull.Value);
                                cmd.Parameters.Add("cccd", (object)model.ND_CCCD ?? DBNull.Value);
                                cmd.Parameters.Add("gioitinh", (object)model.ND_GioiTinh ?? DBNull.Value);
                                cmd.Parameters.Add("quocgia", (object)model.ND_QuocGia ?? DBNull.Value);
                                cmd.Parameters.Add("dantoc", (object)model.ND_DanToc ?? DBNull.Value);
                                cmd.Parameters.Add("nghenghiep", (object)model.ND_NgheNghiep ?? DBNull.Value);
                                cmd.Parameters.Add("tinhthanh", (object)model.ND_TinhThanh ?? DBNull.Value);
                                cmd.Parameters.Add("quanhuyen", (object)model.ND_QuanHuyen ?? DBNull.Value);
                                cmd.Parameters.Add("phuongxa", (object)model.ND_PhuongXa ?? DBNull.Value);
                                cmd.Parameters.Add("ngaysinh", (object)model.ND_NgaySinh ?? DBNull.Value);
                                cmd.Parameters.Add("diachi", (object)model.ND_DiaChiThuongChu ?? DBNull.Value);
                                cmd.Parameters.Add("bnid", model.BN_MaBenhNhan);
                                cmd.ExecuteNonQuery();
                            }

                            // Update BN (nếu bạn dùng các field phụ)
                            var sqlBN = @"
                                        UPDATE BENHNHAN
                                        SET BN_SoBaoHiemYT = :bh,
                                            BN_NhomMau     = :nm,
                                            BN_TieuSuBenhAn= :ts
                                        WHERE BN_MaBenhNhan = :bnid";

                            using (var cmd = new OracleCommand(sqlBN, conn))
                            {
                                cmd.Transaction = tx; cmd.BindByName = true;
                                cmd.Parameters.Add("bh", (object)model.BN_SoBaoHiemYT ?? DBNull.Value);
                                cmd.Parameters.Add("nm", (object)model.BN_NhomMau ?? DBNull.Value);
                                cmd.Parameters.Add("ts", (object)model.BN_TieuSuBenhAn ?? DBNull.Value);
                                cmd.Parameters.Add("bnid", model.BN_MaBenhNhan);
                                cmd.ExecuteNonQuery();
                            }

                            tx.Commit();
                            TempData["Msg"] = "Cập nhật bệnh nhân thành công.";
                            return RedirectToAction("Patients");
                        }
                    }
                }
                catch (Exception ex)
                {
                    TempData["Err"] = "Lỗi cập nhật: " + ex.Message;
                    return View(model);
                }

                
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Lỗi lưu: " + ex.Message;
                return View(model);
            }

            
        }
       

        // ====== D. Khoá/Mở tài khoản bệnh nhân (TOGGLE) ======
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult TogglePatientStatus(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return RedirectToAction("Patients");

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            try
            {
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();

                    // Lấy trạng thái hiện tại
                    string status = null;
                    using (var get = new OracleCommand(
                        "SELECT TK_TrangThai FROM TAIKHOAN WHERE BN_MaBenhNhan = :id", conn))
                    {
                        get.BindByName = true;
                        get.Parameters.Add("id", id);
                        var o = get.ExecuteScalar();
                        status = (o == null || o == DBNull.Value) ? null : o.ToString();
                    }

                    if (string.IsNullOrEmpty(status))
                    {
                        TempData["Err"] = "Bệnh nhân chưa có tài khoản đăng nhập.";
                        return RedirectToAction("Patients");
                    }

                    var newStatus = string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                                    ? "LOCKED" : "ACTIVE";

                    using (var upd = new OracleCommand(
                        "UPDATE TAIKHOAN SET TK_TrangThai = :st WHERE BN_MaBenhNhan = :id", conn))
                    {
                        upd.BindByName = true;
                        upd.Parameters.Add("st", newStatus);
                        upd.Parameters.Add("id", id);
                        upd.ExecuteNonQuery();
                    }
                }

                TempData["Msg"] = "Cập nhật trạng thái tài khoản thành công.";
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Lỗi: " + ex.Message;
            }

            return RedirectToAction("Patients");
        }
    }
}