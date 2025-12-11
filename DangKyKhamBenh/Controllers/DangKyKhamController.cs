using DangKyKhamBenh.Models.ViewModels;
using DangKyKhamBenh.Services;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
namespace DangKyKhamBenh.Controllers
{
    public class DangKyKhamController : Controller
    {
        // GET: DangKyKham
        private readonly CaesarCipher _caesarCipher;
        private readonly RsaService _rsaService;
        private readonly HybridService _hybridService;

        public DangKyKhamController()
        {
            _caesarCipher = new CaesarCipher();
            _rsaService = new RsaService();
            _hybridService = new HybridService();
        }

        public ActionResult Index()
        {
            var userId = Session["ND_IdNguoiDung"]?.ToString();
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Err"] = "Bạn chưa đăng nhập.";
                return RedirectToAction("Login", "Account");
            }

            var model = new List<BenhNhan>();

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();

                var sql = @"
                    SELECT BN_MaBenhNhan, ND_HoTen, ND_SoDienThoai, 
                           BN_SoBaoHiemYT, BN_TieuSuBenhAn, ND_CCCD, ND_GioiTinh, 
                           ND_QuocGia, ND_DanToc, ND_NgheNghiep, ND_TinhThanh, 
                           ND_QuanHuyen, ND_PhuongXa
                    FROM BENHNHAN bn
                    JOIN NGUOIDUNG nd ON bn.ND_IdNguoiDung = nd.ND_IdNguoiDung
                    WHERE bn.ND_IdNguoiDung = :userId";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.Parameters.Add("userId", OracleDbType.Varchar2).Value = userId;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var benhNhan = new BenhNhan
                            {
                                BN_MaBenhNhan = reader["BN_MaBenhNhan"].ToString(),
                                ND_HoTen = reader["ND_HoTen"].ToString(),
                                ND_SoDienThoai = reader["ND_SoDienThoai"].ToString(),
                                BN_SoBaoHiemYT = reader["BN_SoBaoHiemYT"] as string,
                                BN_TieuSuBenhAn = reader["BN_TieuSuBenhAn"] as string,
                                ND_CCCD = reader["ND_CCCD"] as string,
                                ND_GioiTinh = reader["ND_GioiTinh"] as string,
                                ND_QuocGia = reader["ND_QuocGia"] as string,
                                ND_DanToc = reader["ND_DanToc"] as string,
                                ND_NgheNghiep = reader["ND_NgheNghiep"] as string,
                                ND_TinhThanh = reader["ND_TinhThanh"] as string,
                                ND_QuanHuyen = reader["ND_QuanHuyen"] as string,
                                ND_PhuongXa = reader["ND_PhuongXa"] as string
                            };

                            model.Add(benhNhan);
                        }
                    }
                }
              
                

            }

            // Kiểm tra hồ sơ đầy đủ
            var hasCompleteProfile = model.All(item =>
                !string.IsNullOrEmpty(item.BN_SoBaoHiemYT) &&
                !string.IsNullOrEmpty(item.BN_TieuSuBenhAn) &&
                !string.IsNullOrEmpty(item.ND_CCCD) &&
                !string.IsNullOrEmpty(item.ND_GioiTinh) &&
                !string.IsNullOrEmpty(item.ND_QuocGia) &&
                !string.IsNullOrEmpty(item.ND_DanToc) &&
                !string.IsNullOrEmpty(item.ND_NgheNghiep) &&
                !string.IsNullOrEmpty(item.ND_TinhThanh) &&
                !string.IsNullOrEmpty(item.ND_QuanHuyen) &&
                !string.IsNullOrEmpty(item.ND_PhuongXa));

            // Nếu hồ sơ chưa đầy đủ, hiển thị thông báo yêu cầu tạo hồ sơ
            if (!hasCompleteProfile)
            {
                TempData["Err"] = "Bạn chưa có hồ sơ đầy đủ. Vui lòng tạo hồ sơ.";
                return View(model);  // Quay lại trang với thông báo
            }

            try
            {
                foreach (var item in model)
                {
                    item.ND_SoDienThoai = _rsaService.Decrypt(item.ND_SoDienThoai);
                    System.Diagnostics.Debug.WriteLine("Số điện thoại sau giải mã: " + item.ND_SoDienThoai);
                }
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Lỗi giải mã số điện thoại: " + ex.Message;
                return View(model);
            }


            return View(model); // Trả về danh sách hồ sơ
        }



        public ActionResult ChonChuyenKhoa(string bnId,string TuKhoa = null)
        {
            if (!string.IsNullOrWhiteSpace(bnId))
                Session["BN_MaBenhNhan"] = bnId;


            var model = new ChonChuyenKhoaVM { TuKhoa = TuKhoa };
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();

                var sql = @"
                SELECT dv.DV_MaDichVu,
                       k.K_TenKhoa,
                       dv.DV_TenDichVu,
                       dv.DV_GiaTien
                FROM   DICHVU dv
                JOIN   KHOA k ON k.K_MaKhoa = dv.K_MaKhoa
                WHERE  (:kw IS NULL 
                        OR LOWER(k.K_TenKhoa) LIKE '%' || LOWER(:kw) || '%')
                ORDER BY k.K_TenKhoa";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("kw",
                        string.IsNullOrWhiteSpace(TuKhoa) ? (object)DBNull.Value : TuKhoa);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            model.Items.Add(new ChonChuyenKhoaItemVM
                            {
                                DV_MaDichVu = r["DV_MaDichVu"].ToString(),
                                TenChuyenKhoa = r["K_TenKhoa"].ToString(),
                                MoTa = r["DV_TenDichVu"].ToString(),   // dòng nhỏ phía dưới
                                GiaTien = Convert.ToDecimal(r["DV_GiaTien"])
                            });
                        }
                    }
                }
            }

            return View(model);
        }

        public ActionResult DangKyTheoChuyenKhoa(string dvId)
        {
            return RedirectToAction("ChonNgayKham", new { dvId });
        }
        /////////////////////////////////////////////////////////////
        public ActionResult ChonNgayKham(string dvId, int? year, int? month)
        {
            if (string.IsNullOrEmpty(dvId))
                return RedirectToAction("ChonChuyenKhoa");

            var today = DateTime.Today;

            int y = year ?? today.Year;
            int m = month ?? today.Month;

            var firstDayOfMonth = new DateTime(y, m, 1);
            int daysInMonth = DateTime.DaysInMonth(y, m);

            var model = new ChonNgayKhamVM
            {
                DV_MaDichVu = dvId,
                Year = y,
                Month = m
            };

            for (int d = 1; d <= daysInMonth; d++)
            {
                var date = new DateTime(y, m, d);

               
                bool selectable = date >= today;

                model.Days.Add(new NgayKhamItemVM
                {
                    Date = date,
                    IsSelectable = selectable,
                    IsToday = date == today
                });
            }

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult XacNhanNgayKham(string dvId, DateTime ngayKham)
        {
           
            return RedirectToAction("ChonKhungGio", new { dvId = dvId, ngay = ngayKham.ToString("yyyy-MM-dd") });
        }
        

        private async Task<List<SlotChonVM>> GetSlotsForBacSi(string pcId, OracleConnection conn)
        {
            var slots = new List<SlotChonVM>();

            var cmd = new OracleCommand(@"
                    SELECT 
                        SLOT_Id, SLOT_GioBD, SLOT_GioKT, SLOT_GioiHan, SLOT_SoDaDK 
                    FROM 
                        SLOTKHAM 
                    WHERE 
                        PC_Id = :pcId", conn);

            cmd.Parameters.Add(":pcId", OracleDbType.Varchar2).Value = pcId;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    slots.Add(new SlotChonVM
                    {
                        SlotId = reader["SLOT_Id"].ToString(),
                        GioBD = reader["SLOT_GioBD"].ToString(),
                        GioKT = reader["SLOT_GioKT"].ToString(),
                        GioiHan = reader.GetInt32(reader.GetOrdinal("SLOT_GioiHan")),
                        SoDaDK = reader.GetInt32(reader.GetOrdinal("SLOT_SoDaDK"))
                    });
                }
            }

            return slots;
        }
        [HttpGet]
        public ActionResult ChonKhungGio(string dvId, string ngay)
        {
            if (string.IsNullOrWhiteSpace(dvId) || string.IsNullOrWhiteSpace(ngay))
                return RedirectToAction("ChonChuyenKhoa");

            // parse yyyy-MM-dd
            var ngayChon = DateTime.ParseExact(ngay, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var con = new OracleConnection(cs))
            {
                con.Open();

                // 1) Lấy K_MaKhoa + TenKhoa từ dvId
                string maKhoa = null, tenKhoa = null;
                using (var cmd = new OracleCommand(@"
                    SELECT dv.K_MaKhoa, k.K_TenKhoa
                    FROM DICHVU dv
                    JOIN KHOA k ON k.K_MaKhoa = dv.K_MaKhoa
                    WHERE dv.DV_MaDichVu = :dvId", con))
                {
                    cmd.Parameters.Add(":dvId", dvId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            maKhoa = rd.GetString(0);
                            tenKhoa = rd.GetString(1);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(maKhoa))
                    return RedirectToAction("ChonChuyenKhoa");

                var vm = new ChonKhungGioVM
                {
                    DV_MaDichVu = dvId,
                    K_MaKhoa = maKhoa,
                    TenKhoa = tenKhoa,
                    NgayDangChon = ngayChon
                };

                // 2) Lấy list ngày có lịch (để hiện dải ngày như ảnh)
                using (var cmd = new OracleCommand(@"
                        SELECT * FROM (
                          SELECT DISTINCT pc.PC_Ngay
                          FROM PHANCONG pc
                          JOIN PHONGKHAM pk ON pk.PK_MaPK = pc.PK_MaPK
                          WHERE pk.K_MaKhoa = :khoa
                            AND pc.PC_Ngay >= TRUNC(SYSDATE)
                          ORDER BY pc.PC_Ngay
                        )
                        WHERE ROWNUM <= 10", con))
                            {
                    cmd.Parameters.Add(":khoa", maKhoa);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            vm.NgayCoLich.Add(rd.GetDateTime(0));
                    }
                }

                // 3) Lấy lịch + slot đúng ngày
                var map = new Dictionary<string, BacSiKhungGioCardVM>(); // key: PC_Id
                using (var cmd = new OracleCommand(@"
                        SELECT
                            pc.PC_Id,
                            pc.PC_CaTruc,
                            bs.BS_MaBacSi,
                            nd.ND_HoTen,
                            pk.PK_MaPK,
                            pk.PK_TenPhong,
                            pk.PK_ViTri,
                            s.SLOT_Id,
                            s.SLOT_GioBD,
                            s.SLOT_GioKT,
                            NVL(s.SLOT_GioiHan,0) AS GioiHan,
                            NVL(s.SLOT_SoDaDK,0)  AS SoDaDK
                        FROM PHANCONG pc
                        JOIN BACSI bs     ON bs.BS_MaBacSi = pc.BS_MaBacSi
                        JOIN NGUOIDUNG nd ON nd.ND_IdNguoiDung = bs.ND_IdNguoiDung
                        JOIN PHONGKHAM pk ON pk.PK_MaPK = pc.PK_MaPK
                        LEFT JOIN SLOTKHAM s ON s.PC_Id = pc.PC_Id
                        WHERE pk.K_MaKhoa = :khoa
                          AND TRUNC(pc.PC_Ngay) = TRUNC(:ngay)
                        ORDER BY nd.ND_HoTen, pc.PC_CaTruc, s.SLOT_GioBD", con))
                            {
                    cmd.BindByName = true;
                    cmd.Parameters.Add(":khoa", maKhoa);
                    cmd.Parameters.Add(":ngay", ngayChon);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var pcId = rd.GetString(0);

                            if (!map.TryGetValue(pcId, out var card))
                            {
                                card = new BacSiKhungGioCardVM
                                {
                                    PC_Id = pcId,
                                    CaTruc = rd.IsDBNull(1) ? null : rd.GetString(1),
                                    BS_MaBacSi = rd.IsDBNull(2) ? null : rd.GetString(2),
                                    HoTen = rd.IsDBNull(3) ? null : rd.GetString(3),
                                    PK_MaPK = rd.IsDBNull(4) ? null : rd.GetString(4),
                                    TenPhong = rd.IsDBNull(5) ? null : rd.GetString(5),
                                    ViTri = rd.IsDBNull(6) ? null : rd.GetString(6),
                                };
                                map[pcId] = card;
                            }

                            // slot có thể null nếu chưa tạo SLOTKHAM
                            if (!rd.IsDBNull(7))
                            {
                                card.Slots.Add(new SlotChonVM
                                {
                                    SlotId = rd.GetString(7),
                                    GioBD = rd.IsDBNull(8) ? null : rd.GetString(8),
                                    GioKT = rd.IsDBNull(9) ? null : rd.GetString(9),
                                    GioiHan = Convert.ToInt32(rd.GetDecimal(10)),
                                    SoDaDK = Convert.ToInt32(rd.GetDecimal(11)),
                                });
                            }
                        }
                    }
                }

                vm.Cards = map.Values.ToList();
                return View(vm); // Views/DangKyKham/ChonKhungGio.cshtml
            }
        }


        [HttpGet]
        public ActionResult XacNhanBHYT(string slotId, string dvId, string ngay)
        {
            if (string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(dvId) || string.IsNullOrWhiteSpace(ngay))
                return RedirectToAction("ChonChuyenKhoa");

            if (!DateTime.TryParseExact(ngay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngayChon))
                return RedirectToAction("ChonKhungGio", new { dvId, ngay = DateTime.Today.ToString("yyyy-MM-dd") });

            var vm = new XacNhanBHYTVM
            {
                SlotId = slotId,
                DV_MaDichVu = dvId,
                NgayKham = ngayChon.Date,
                BhytCase = "4",
                BaoLanhVienPhi = false
            };

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var con = new OracleConnection(cs))
            {
                con.Open();

                var sql = @"
                        SELECT
                            dv.DV_TenDichVu,
                            dv.DV_GiaTien,
                            sl.SLOT_GioBD,
                            sl.SLOT_GioKT,
                            pk.PK_TenPhong,
                            pk.PK_ViTri,
                            nd.ND_HoTen
                        FROM SLOTKHAM sl
                        JOIN PHANCONG pc   ON pc.PC_Id = sl.PC_Id
                        JOIN PHONGKHAM pk  ON pk.PK_MaPK = pc.PK_MaPK
                        JOIN BACSI bs      ON bs.BS_MaBacSi = pc.BS_MaBacSi
                        JOIN NGUOIDUNG nd  ON nd.ND_IdNguoiDung = bs.ND_IdNguoiDung
                        JOIN DICHVU dv     ON dv.DV_MaDichVu = :dvId
                        WHERE sl.SLOT_Id = :slotId
                          AND TRUNC(pc.PC_Ngay) = TRUNC(:ngay)";

                            using (var cmd = new OracleCommand(sql, con))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add(":dvId", OracleDbType.Char).Value = dvId;
                    cmd.Parameters.Add(":slotId", OracleDbType.Char).Value = slotId;
                    cmd.Parameters.Add(":ngay", OracleDbType.Date).Value = ngayChon.Date;

                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            TempData["Err"] = "Slot không tồn tại hoặc lịch đã thay đổi.";
                            return RedirectToAction("ChonKhungGio", new { dvId, ngay });
                        }

                        vm.TenDichVu = rd.IsDBNull(0) ? "" : rd.GetString(0);
                        vm.TienKham = rd.IsDBNull(1) ? 0 : rd.GetDecimal(1);
                        vm.GioBD = rd.IsDBNull(2) ? "" : rd.GetString(2);
                        vm.GioKT = rd.IsDBNull(3) ? "" : rd.GetString(3);
                        vm.TenPhong = rd.IsDBNull(4) ? "" : rd.GetString(4);
                        vm.ViTri = rd.IsDBNull(5) ? "" : rd.GetString(5);
                        vm.TenBacSi = rd.IsDBNull(6) ? "" : rd.GetString(6);
                    }
                }
            }

            return View(vm); // Views/DangKyKham/XacNhanBHYT.cshtml
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult XacNhanBHYT(XacNhanBHYTVM vm)
        {
            if (vm == null) return RedirectToAction("ChonChuyenKhoa");

            if (string.IsNullOrWhiteSpace(vm.BhytCase))
                ModelState.AddModelError("BhytCase", "Vui lòng chọn trường hợp BHYT.");

            if (!vm.BaoLanhVienPhi.HasValue)
                ModelState.AddModelError("BaoLanhVienPhi", "Vui lòng chọn bảo lãnh viện phí.");

            if (!ModelState.IsValid)
                return View(vm);

            // lưu lựa chọn để trang Phiếu tổng hợp đọc lại
            Session["DK_SlotId"] = vm.SlotId;
            Session["DK_DvId"] = vm.DV_MaDichVu;
            Session["DK_NgayKham"] = vm.NgayKham;
            Session["DK_BhytCase"] = vm.BhytCase;
            Session["DK_BaoLanhVienPhi"] = vm.BaoLanhVienPhi.Value;

            return RedirectToAction("PhieuTongHop");
        }

        [HttpGet]
        public ActionResult PhieuTongHop()
        {
            var userId = Session["ND_IdNguoiDung"]?.ToString();
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var slotId = Session["DK_SlotId"]?.ToString();
            var dvId = Session["DK_DvId"]?.ToString();
            var ngayObj = Session["DK_NgayKham"];
            var bhytCase = Session["DK_BhytCase"]?.ToString();
            var baoLanhObj = Session["DK_BaoLanhVienPhi"];

            if (string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(dvId) || ngayObj == null || baoLanhObj == null)
                return RedirectToAction("ChonChuyenKhoa");

            var ngayKham = (DateTime)ngayObj;
            var baoLanh = (bool)baoLanhObj;

            var vm = new DangKyKhamBenh.Models.ViewModels.PhieuTongHopVM
            {
                SlotId = slotId,
                DV_MaDichVu = dvId,
                NgayKham = ngayKham.Date,
                BhytCase = bhytCase,
                BaoLanhVienPhi = baoLanh,
                BhytCaseText = MapBhytCase(bhytCase)
            };
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var con = new OracleConnection(cs))
            {
                con.Open();

                // 1) lấy thông tin lịch khám từ slot + dv + ngày
                   using (var cmd = new OracleCommand(@"
                        SELECT
                            dv.DV_TenDichVu,
                            dv.DV_GiaTien,
                            sl.SLOT_GioBD,
                            sl.SLOT_GioKT,
                            pk.PK_TenPhong,
                            pk.PK_ViTri,
                            nd.ND_HoTen
                        FROM SLOTKHAM sl
                        JOIN PHANCONG pc   ON pc.PC_Id = sl.PC_Id
                        JOIN PHONGKHAM pk  ON pk.PK_MaPK = pc.PK_MaPK
                        JOIN BACSI bs      ON bs.BS_MaBacSi = pc.BS_MaBacSi
                        JOIN NGUOIDUNG nd  ON nd.ND_IdNguoiDung = bs.ND_IdNguoiDung
                        JOIN DICHVU dv     ON dv.DV_MaDichVu = :dvId
                        WHERE sl.SLOT_Id = :slotId
                          AND TRUNC(pc.PC_Ngay) = TRUNC(:ngay)", con))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add(":dvId", OracleDbType.Char).Value = dvId;
                    cmd.Parameters.Add(":slotId", OracleDbType.Char).Value = slotId;
                    cmd.Parameters.Add(":ngay", OracleDbType.Date).Value = ngayKham.Date;

                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            TempData["Err"] = "Không tìm thấy lịch khám (slot/dịch vụ/ngày).";
                            return RedirectToAction("ChonKhungGio", new { dvId = dvId, ngay = ngayKham.ToString("yyyy-MM-dd") });
                        }

                        vm.TenDichVu = rd.IsDBNull(0) ? "" : rd.GetString(0);
                        vm.TienKham = rd.IsDBNull(1) ? 0 : Convert.ToDecimal(rd.GetValue(1));
                        vm.GioBD = rd.IsDBNull(2) ? "" : rd.GetString(2);
                        vm.GioKT = rd.IsDBNull(3) ? "" : rd.GetString(3);
                        vm.TenPhong = rd.IsDBNull(4) ? "" : rd.GetString(4);
                        vm.ViTri = rd.IsDBNull(5) ? "" : rd.GetString(5);
                        vm.TenBacSi = rd.IsDBNull(6) ? "" : rd.GetString(6);
                    }
                }
                // 2) lấy thông tin bệnh nhân theo user đăng nhập (lấy 1 hồ sơ)
                using (var cmd = new OracleCommand(@"
                        SELECT 
                            bn.BN_MaBenhNhan,
                            nd.ND_HoTen,
                            nd.ND_SoDienThoai,
                            nd.ND_CCCD,
                            bn.BN_SoBaoHiemYT
                        FROM BENHNHAN bn
                        JOIN NGUOIDUNG nd ON nd.ND_IdNguoiDung = bn.ND_IdNguoiDung
                        WHERE bn.ND_IdNguoiDung = :userId
                        FETCH FIRST 1 ROWS ONLY", con))
                {

                    cmd.BindByName = true;
                    cmd.Parameters.Add(":userId", OracleDbType.Varchar2).Value = userId;

                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            vm.BN_MaBenhNhan = rd.IsDBNull(0) ? "" : rd.GetString(0).Trim();

                            vm.HoTen = rd.IsDBNull(1) ? "" : rd.GetString(1);

                            var phoneEnc = rd.IsDBNull(2) ? "" : rd.GetString(2);
                            try { vm.SoDienThoai = string.IsNullOrEmpty(phoneEnc) ? "" : _rsaService.Decrypt(phoneEnc); }
                            catch { vm.SoDienThoai = phoneEnc; }

                            var cccdEnc = rd.IsDBNull(3) ? "" : rd.GetString(3);
                            var key = (vm.BN_MaBenhNhan ?? "").Trim();
                            vm.CCCD = string.IsNullOrEmpty(cccdEnc) || string.IsNullOrEmpty(key)
                                ? cccdEnc
                                : _hybridService.Decrypt(cccdEnc, key);

                            var bhytEnc = rd.IsDBNull(4) ? "" : rd.GetString(4);
                            vm.SoBHYT = string.IsNullOrEmpty(bhytEnc) || string.IsNullOrEmpty(key)
                                ? bhytEnc
                                : _hybridService.Decrypt(bhytEnc, key);
                        }
                    }
                }

            }

            return View(vm);
        }

        private string MapBhytCase(string v)
        {
            switch (v)
            {
                case "1": return "Đăng ký KCB BHYT ban đầu tại BV ĐHYD";
                case "2": return "Có giấy chuyển BHYT đúng tuyến BV ĐHYD";
                case "3": return "Tái khám theo hẹn trên đơn thuốc BHYT của BV ĐHYD";
                default: return "Không phải 3 trường hợp trên";
            }
        }

        [HttpGet]
        public ActionResult ChonThanhToan(string slotId, string dvId, string ngay)
        {
            if (string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(dvId) || string.IsNullOrWhiteSpace(ngay))
                return RedirectToAction("ChonChuyenKhoa");

            if (!DateTime.TryParseExact(ngay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngayKham))
                return RedirectToAction("ChonChuyenKhoa");

            var vm = new ChonThanhToanVM
            {
                SlotId = slotId,
                DV_MaDichVu = dvId,
                NgayKham = ngayKham.Date,
                SelectedMethod = PaymentMethod.PayAtHospital // mặc định
            };

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var con = new OracleConnection(cs))
            {
                con.Open();

                // chỉ cần tên DV + giá
                using (var cmd = new OracleCommand(@"
                    SELECT DV_TenDichVu, DV_GiaTien
                    FROM DICHVU
                    WHERE DV_MaDichVu = :dvId", con))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add(":dvId", OracleDbType.Char).Value = dvId;

                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            TempData["Err"] = "Không tìm thấy dịch vụ.";
                            return RedirectToAction("ChonChuyenKhoa");
                        }

                        vm.TenDichVu = rd.IsDBNull(0) ? "" : rd.GetString(0);
                        vm.TienKham = rd.IsDBNull(1) ? 0 : Convert.ToDecimal(rd.GetValue(1));

                    }
                }
                VietQrVm vietQrModel = new VietQrVm
                {
                    AccountNo = "977783867979",
                    AccountName = "TO TRUONG TRUONG THANH",
                    Amount = Convert.ToInt64(vm.TienKham),
                    AddInfo = "Thanh toán khám bệnh"
                };
                ViewBag.VietQrModel = vietQrModel;
            }
            
            return View(vm); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChonThanhToan(ChonThanhToanVM vm)
        {
            if (vm == null) return RedirectToAction("ChonChuyenKhoa");

            if (vm.SelectedMethod == PaymentMethod.QR)
            {
                VietQrVm vietQrModel = new VietQrVm
                {
                    AccountNo = "977783867979",
                    AccountName = "TO TRUONG TRUONG THANH",
                    Amount = Convert.ToInt64(vm.TienKham),
                    AddInfo = "Thanh toán khám bệnh"
                };
                ViewBag.VietQrModel = vietQrModel;
                return RedirectToAction("VietQr", "DangKyKham", new { amount = vm.TienKham });
            }

            var userId = Session["ND_IdNguoiDung"]?.ToString();
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

            var bnId = Session["BN_MaBenhNhan"]?.ToString();
            if (string.IsNullOrEmpty(bnId))
            {
                TempData["Err"] = "Bạn chưa chọn hồ sơ bệnh nhân.";
                return RedirectToAction("Index");
            }

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            using (var con = new OracleConnection(cs))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        // 1) Lock slot + kiểm tra còn chỗ + lấy PC_Id để dùng tiếp
                        string pcId = null;
                        int gioiHan = 0, soDa = 0;

                        using (var cmd = new OracleCommand(@"
                    SELECT PC_Id, NVL(SLOT_GioiHan,0), NVL(SLOT_SoDaDK,0)
                    FROM SLOTKHAM
                    WHERE SLOT_Id = :slotId
                    FOR UPDATE", con))
                        {
                            cmd.Transaction = tx;
                            cmd.BindByName = true;
                            cmd.Parameters.Add(":slotId", OracleDbType.Char).Value = vm.SlotId;

                            using (var rd = await cmd.ExecuteReaderAsync())
                            {
                                if (!await rd.ReadAsync())
                                    throw new Exception("Slot không tồn tại.");

                                pcId = rd.GetString(0);
                                gioiHan = Convert.ToInt32(rd.GetDecimal(1));
                                soDa = Convert.ToInt32(rd.GetDecimal(2));
                            }
                        }

                        if (gioiHan > 0 && soDa >= gioiHan)
                        {
                            tx.Rollback();
                            TempData["Err"] = "Slot đã đầy, vui lòng chọn khung giờ khác.";
                            return RedirectToAction("ChonKhungGio", new { dvId = vm.DV_MaDichVu, ngay = vm.NgayKham.ToString("yyyy-MM-dd") });
                        }

                        // 2) Lấy BS_MaBacSi + K_MaKhoa (để tạo PHIEUDANGKY/PHIEUDICHVU)
                        string bsId = null;
                        string maKhoa = null;

                        using (var cmd = new OracleCommand(@"
                    SELECT pc.BS_MaBacSi, dv.K_MaKhoa
                    FROM PHANCONG pc
                    JOIN DICHVU dv ON dv.DV_MaDichVu = :dvId
                    WHERE pc.PC_Id = :pcId", con))
                        {
                            cmd.Transaction = tx;
                            cmd.BindByName = true;
                            cmd.Parameters.Add(":dvId", OracleDbType.Char).Value = vm.DV_MaDichVu;
                            cmd.Parameters.Add(":pcId", OracleDbType.Char).Value = pcId;

                            using (var rd = await cmd.ExecuteReaderAsync())
                            {
                                if (!await rd.ReadAsync())
                                    throw new Exception("Không lấy được bác sĩ/khoa từ phân công.");
                                bsId = rd.GetString(0);
                                maKhoa = rd.GetString(1);
                            }
                        }

                        // 3) UPDATE SLOTKHAM: tăng đã đăng ký
                        using (var cmd = new OracleCommand(@"
                    UPDATE SLOTKHAM
                    SET SLOT_SoDaDK = NVL(SLOT_SoDaDK,0) + 1
                    WHERE SLOT_Id = :slotId", con))
                        {
                            cmd.Transaction = tx;
                            cmd.BindByName = true;
                            cmd.Parameters.Add(":slotId", OracleDbType.Char).Value = vm.SlotId;

                            var affected = await cmd.ExecuteNonQueryAsync();
                            if (affected != 1) throw new Exception("Không thể cập nhật SLOT_SoDaDK.");
                        }

                        // 4) INSERT PHIEUDANGKY (để có DK_MaPhieuKham cho PHIEUDICHVU)
                        var dkId = NextId("DK", 10);
                        using (var cmd = new OracleCommand(@"
                    INSERT INTO PHIEUDANGKY (DK_MaPhieuKham, DK_NgayKham, DK_TrieuTrung, DK_ChuanDoan, BN_MaBenhNhan, K_MaKhoa)
                    VALUES (:dk, :ngay, :trieuChung, :chuanDoan, :bn, :khoa)", con))
                        {
                            cmd.Transaction = tx;
                            cmd.BindByName = true;
                            cmd.Parameters.Add(":dk", OracleDbType.Char).Value = dkId;
                            cmd.Parameters.Add(":ngay", OracleDbType.Date).Value = vm.NgayKham.Date;
                            cmd.Parameters.Add(":trieuChung", OracleDbType.Varchar2).Value = DBNull.Value;
                            cmd.Parameters.Add(":chuanDoan", OracleDbType.Varchar2).Value = DBNull.Value;
                            cmd.Parameters.Add(":bn", OracleDbType.Char).Value = bnId;
                            cmd.Parameters.Add(":khoa", OracleDbType.Char).Value = maKhoa;

                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 5) INSERT PHIEUDICHVU (tạo mã phiếu + stt)
                        var pdvId = NextId("PDV", 10);

                        using (var cmd = new OracleCommand(@"
                    INSERT INTO PHIEUDICHVU (PDV_MaPhieu, PDV_NgayChiDinh, PDV_KetQua, BN_MaBenhNhan, DV_MaDichVu, BS_MaBacSi, DK_MaPhieuKham)
                    VALUES (:pdv, SYSDATE, :kq, :bn, :dv, :bs, :dk)", con))
                        {
                            cmd.Transaction = tx;
                            cmd.BindByName = true;
                            cmd.Parameters.Add(":pdv", OracleDbType.Char).Value = pdvId;
                            cmd.Parameters.Add(":kq", OracleDbType.NVarchar2).Value = DBNull.Value;
                            cmd.Parameters.Add(":bn", OracleDbType.Char).Value = bnId;
                            cmd.Parameters.Add(":dv", OracleDbType.Char).Value = vm.DV_MaDichVu;
                            cmd.Parameters.Add(":bs", OracleDbType.Char).Value = bsId;
                            cmd.Parameters.Add(":dk", OracleDbType.Char).Value = dkId;

                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Lưu phương thức thanh toán (tuỳ bạn)
                        Session["DK_PaymentMethod"] = vm.SelectedMethod;

                        tx.Commit();

                        // qua trang phiếu khám (có STT)
                        return RedirectToAction("PhieuKhamBenh", new { id = pdvId });
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch { }
                        TempData["Err"] = "Lỗi tạo phiếu: " + ex.Message;
                        return RedirectToAction("ChonThanhToan", new { slotId = vm.SlotId, dvId = vm.DV_MaDichVu, ngay = vm.NgayKham.ToString("yyyy-MM-dd") });
                    }
                }
            }
        }

        private static string NextId(string prefix, int totalLen = 10)
        {
            // prefix + digits, total length = totalLen
            int digits = totalLen - prefix.Length;
            var rng = new Random();
            var num = rng.Next(0, (int)Math.Pow(10, digits)); // 0 -> 999...
            return prefix + num.ToString(new string('0', digits));
        }

        [HttpGet]
        public async Task<ActionResult> PhieuKhamBenh(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return RedirectToAction("Index");

            var stt = 0;
            var tail2 = id.Trim().Substring(id.Trim().Length - 2);
            int.TryParse(tail2, out stt);

            var vm = new DangKyKhamBenh.Models.ViewModels.PhieuKhamBenhVM
            {
                PdvId = id.Trim(),
                STT = stt,
                MaGiaoDich = (Session["DK_PaymentMethod"]?.ToString() == "MoMo") ? "MOMO_" + NextId("", 10) : null
            };

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var con = new OracleConnection(cs))
            {
                await con.OpenAsync();

                // Lấy đủ thông tin từ PDV -> DK -> BN/ND + slot/pc/pk + dv
                using (var cmd = new OracleCommand(@"
            SELECT
                dv.DV_TenDichVu,
                dv.DV_GiaTien,
                pk.PK_TenPhong,
                pk.PK_ViTri,
                sl.SLOT_GioBD,
                sl.SLOT_GioKT,
                pc.PC_CaTruc,
                dk.DK_NgayKham,
                nd.ND_HoTen,
                nd.ND_NgaySinh,
                nd.ND_GioiTinh,
                nd.ND_TinhThanh,
                pdv.BN_MaBenhNhan
            FROM PHIEUDICHVU pdv
            JOIN PHIEUDANGKY dk ON dk.DK_MaPhieuKham = pdv.DK_MaPhieuKham
            JOIN BENHNHAN bn     ON bn.BN_MaBenhNhan = pdv.BN_MaBenhNhan
            JOIN NGUOIDUNG nd    ON nd.ND_IdNguoiDung = bn.ND_IdNguoiDung
            JOIN DICHVU dv       ON dv.DV_MaDichVu = pdv.DV_MaDichVu
            JOIN PHANCONG pc     ON pc.BS_MaBacSi = pdv.BS_MaBacSi AND TRUNC(pc.PC_Ngay)=TRUNC(dk.DK_NgayKham)
            JOIN SLOTKHAM sl     ON sl.PC_Id = pc.PC_Id
            JOIN PHONGKHAM pk    ON pk.PK_MaPK = pc.PK_MaPK
            WHERE pdv.PDV_MaPhieu = :pdv
            FETCH FIRST 1 ROWS ONLY", con))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add(":pdv", OracleDbType.Char).Value = id.Trim();

                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                        {
                            TempData["Err"] = "Không tìm thấy phiếu khám.";
                            return RedirectToAction("Index");
                        }

                        vm.TenDichVu = rd.IsDBNull(0) ? "" : rd.GetString(0);
                        vm.TienKham = rd.IsDBNull(1) ? 0 : Convert.ToDecimal(rd.GetValue(1));
                        vm.TenPhong = rd.IsDBNull(2) ? "" : rd.GetString(2);
                        vm.ViTri = rd.IsDBNull(3) ? "" : rd.GetString(3);
                        vm.GioBD = rd.IsDBNull(4) ? "" : rd.GetString(4);
                        vm.GioKT = rd.IsDBNull(5) ? "" : rd.GetString(5);
                        vm.Buoi = rd.IsDBNull(6) ? "" : ("Buổi " + rd.GetString(6));
                        vm.NgayKham = rd.GetDateTime(7);

                        vm.HoTen = rd.IsDBNull(8) ? "" : rd.GetString(8);
                        vm.NgaySinh = rd.IsDBNull(9) ? (DateTime?)null : rd.GetDateTime(9);
                        vm.GioiTinh = rd.IsDBNull(10) ? "" : rd.GetString(10);
                        vm.TinhTP = rd.IsDBNull(11) ? "" : rd.GetString(11);
                        vm.SoHoSo = rd.IsDBNull(12) ? "" : rd.GetString(12);
                    }
                }
            }

            vm.DoiTuong = "Thu phí";

            return View(vm); 
        }

        [HttpGet]
        public ActionResult VietQr(decimal amount)
        {
            var vm = new VietQrVm
            {
                AccountNo = "977783867979",
                AccountName = "TO TRUONG TRUONG THANH",
                Amount = Convert.ToInt64(amount),
                AddInfo = "Thanh toán khám bệnh",
                QrUrl = BuildVietQrUrl(amount)
            };

            return View(vm); 
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VietQr(VietQrVm vm)
        {
            decimal amount = vm.Amount.GetValueOrDefault(); 
            vm.QrUrl = BuildVietQrUrl(amount);

            return View(vm);
        }


        private string BuildVietQrUrl(decimal amount)
        {
            var baseUrl = $"https://img.vietqr.io/image/mbbank-977783867979-compact2.png";

            var qs = new List<string>();

            if (amount > 0)
                qs.Add($"amount={amount}");

            if (!string.IsNullOrWhiteSpace("Thanh toán khám bệnh"))
                qs.Add($"addInfo={Uri.EscapeDataString("Thanh toán khám bệnh")}");

            if (!string.IsNullOrWhiteSpace("TO TRUONG TRUONG THANH"))
                qs.Add($"accountName={Uri.EscapeDataString("TO TRUONG TRUONG THANH")}");

            return qs.Count == 0 ? baseUrl : $"{baseUrl}?{string.Join("&", qs)}";
        }

        private string GenerateQrUrl(decimal amount)
        {
            string qrData = $"bank_id=mbbank&account=977783867979&account_name=TO TRUONG TRUONG THANH&amount={amount}&add_info=Thanh toán khám bệnh";

            // Sử dụng Google Chart API hoặc bất kỳ thư viện nào bạn muốn để tạo mã QR
            string qrUrl = $"https://chart.googleapis.com/chart?chs=300x300&cht=qr&chl={HttpUtility.UrlEncode(qrData)}";

            return qrUrl;
        }


    }
}