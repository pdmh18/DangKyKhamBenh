using DangKyKhamBenh.Filters;                       // Dùng attribute [AdminOnly] để khóa controller cho ADMIN
using Oracle.ManagedDataAccess.Client;              
using System;
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
        [HttpGet]   // Action GET: hiển thị danh sách yêu cầu đang chờ
        public ActionResult Pending(string keyword = null)
        {   
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString; // Lấy connection string
            using (var conn = new OracleConnection(cs))  // // Tạo kết nối Oracle
            {
                conn.Open(); // Mở kết nối
                // SQL lấy danh sách yêu cầu
                var sql = @"
                            SELECT PT_MaYeuCau, PT_UserName, PT_Email, PT_SoDienThoai, PT_NgaySinh, PT_DiaChi, PT_Stafftype, PT_NgayYeuCau
                            FROM   PENDING_TAIKHOAN
                            WHERE  (:kw IS NULL 
                                    OR UPPER(TRIM(PT_UserName)) LIKE UPPER('%' || TRIM(:kw) || '%'))
                            ORDER BY PT_NgayYeuCau DESC";

                using (var cmd = new OracleCommand(sql, conn)) // Tạo command
                {
                    cmd.BindByName = true; // Dùng bind name để đặt tham số theo tên
                    cmd.Parameters.Add("kw", (object)keyword ?? DBNull.Value);// Nếu không có keyword → NULL để WHERE bỏ qua

                    using (var r = cmd.ExecuteReader())// Thực thi và đọc kết quả
                    {
                        var rows = new System.Data.DataTable();// Dùng DataTable để đổ dữ liệu nhanh (render view đơn giản)
                        rows.Load(r); // Nạp toàn bộ kết quả vào DataTable
                        return View(rows); // Trả về view Pending.cshtml (model là DataTable)
                    }
                }
            }
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
        [HttpGet, AdminOnly]
        public ActionResult History(DateTime? from = null, DateTime? to = null)
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();

                var sql = @"
            SELECT 
                PT_MaYeuCau,
                PT_UserName,
                PT_Email,
                PT_SoDienThoai,
                PT_NgaySinh,
                PT_DiaChi,
                PT_StaffType,
                PT_NgayYeuCau
            FROM PENDING_TAIKHOAN
            WHERE (:d1 IS NULL OR PT_NgayYeuCau >= :d1)
              AND (:d2 IS NULL OR PT_NgayYeuCau <= :d2)
            ORDER BY PT_NgayYeuCau DESC";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;

                    // Khai báo KIỂU DATE rõ ràng cho 2 tham số
                    var pFrom = new OracleParameter("d1", OracleDbType.Date);
                    pFrom.Value = (object)from ?? DBNull.Value;

                    var pTo = new OracleParameter("d2", OracleDbType.Date);
                    pTo.Value = (object)to ?? DBNull.Value;

                    cmd.Parameters.Add(pFrom);
                    cmd.Parameters.Add(pTo);

                    var tb = new System.Data.DataTable();
                    using (var r = cmd.ExecuteReader())
                    {
                        tb.Load(r);
                    }
                    return View(tb);
                }
            }
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
    }
}
