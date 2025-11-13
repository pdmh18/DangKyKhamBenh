//using DangKyKhamBenh.Models;
//using DangKyKhamBenh.Services;
//using Oracle.ManagedDataAccess.Client;
//using System;
//using System.Configuration;
//using System.Data;
//using System.Web.Mvc;

//namespace DangKyKhamBenh.Controllers
//{
//    public class HoSoController : Controller
//    {
//        private readonly CaesarCipher _caesarCipher;
//        private readonly RsaService _rsaService;
//        private readonly HybridService _hybridService;

//        public HoSoController()
//        {
//            _caesarCipher = new CaesarCipher();
//            _rsaService = new RsaService();
//            _hybridService = new HybridService();
//        }

//        public ActionResult HoSo()
//        {
//            NguoiDung nguoiDung = new NguoiDung();  // Hoặc lấy từ database
//            return View(nguoiDung);
//        }

//        // Hàm mã hóa Hồ Sơ và lưu vào cơ sở dữ liệu Oracle
//        [HttpPost]
//        public ActionResult HoSo(NguoiDung nguoiDung, Patient benhNhan)
//        {
//            // Kiểm tra xem đối tượng có null không
//            if (nguoiDung == null || benhNhan == null)
//            {
//                ViewBag.ErrorMessage = "Dữ liệu không hợp lệ!";
//                return View();
//            }

//            // Nếu ND_IdNguoiDung đã có từ khi đăng ký tài khoản, không cần phải tạo lại
//            if (string.IsNullOrEmpty(nguoiDung.ND_IdNguoiDung))
//            {
//                ViewBag.ErrorMessage = "ID người dùng không hợp lệ!";
//                return View();
//            }

//            // Mã hóa số điện thoại và email nếu chưa mã hóa
//            if (!nguoiDung.ND_SoDienThoai.Contains("enc:"))
//            {
//                nguoiDung.ND_SoDienThoai = _rsaService.Encrypt(nguoiDung.ND_SoDienThoai);
//            }

//            if (!nguoiDung.ND_Email.Contains("enc:"))
//            {
//                var (encryptedEmail, _) = _hybridService.Encrypt(nguoiDung.ND_Email, 1);
//                nguoiDung.ND_Email = encryptedEmail;
//            }

//            // Mã hóa các trường đối xứng (Caesar Cipher)
//            string encryptedHoTen = _caesarCipher.Encrypt(nguoiDung.ND_HoTen, 15);
//            string encryptedTinhThanh = _caesarCipher.Encrypt(nguoiDung.ND_TinhThanh, 15);
//            string encryptedQuanHuyen = _caesarCipher.Encrypt(nguoiDung.ND_QuanHuyen, 15);
//            string encryptedPhuongXa = _caesarCipher.Encrypt(nguoiDung.ND_PhuongXa, 15);

//            // Mã hóa các trường RSA
//            string encryptedDiaChiThuongChu = _rsaService.Encrypt(nguoiDung.ND_DiaChiThuongChu);
//            string encryptedTieuSuBenhAn = _rsaService.Encrypt(benhNhan.BN_TieuSuBenhAn);

//            // Mã hóa lai (Hybrid Encryption) cho những trường khác
//            var (encryptedCCCD, _) = _hybridService.Encrypt(nguoiDung.ND_CCCD, 1);
//            var (encryptedSoBaoHiemYT, _) = _hybridService.Encrypt(benhNhan.BN_SoBaoHiemYT, 1);

//            // Lưu vào cơ sở dữ liệu Oracle bằng ODP.NET
//            string connectionString = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
//            try
//            {
//                using (OracleConnection conn = new OracleConnection(connectionString))
//                {
//                    conn.Open();

//                    // INSERT vào bảng NGUOIDUNG (không cần tự sinh ND_IdNguoiDung nữa vì đã có sẵn)
//                    using (OracleCommand cmd = new OracleCommand(@"
//                INSERT INTO NGUOIDUNG 
//                (ND_IdNguoiDung, ND_HoTen, ND_SoDienThoai, ND_Email, ND_CCCD, ND_NgaySinh, ND_GioiTinh, ND_QuocGia, 
//                ND_DanToc, ND_NgheNghiep, ND_TinhThanh, ND_QuanHuyen, ND_PhuongXa, ND_DiaChiThuongChu) 
//                VALUES 
//                (:ND_IdNguoiDung, :ND_HoTen, :ND_SoDienThoai, :ND_Email, :ND_CCCD, :ND_NgaySinh, :ND_GioiTinh, :ND_QuocGia, 
//                :ND_DanToc, :ND_NgheNghiep, :ND_TinhThanh, :ND_QuanHuyen, :ND_PhuongXa, :ND_DiaChiThuongChu)", conn))
//                    {
//                        cmd.Parameters.Add(":ND_IdNguoiDung", OracleDbType.Varchar2).Value = nguoiDung.ND_IdNguoiDung;
//                        cmd.Parameters.Add(":ND_HoTen", OracleDbType.Varchar2).Value = encryptedHoTen;
//                        cmd.Parameters.Add(":ND_SoDienThoai", OracleDbType.Varchar2).Value = nguoiDung.ND_SoDienThoai;
//                        cmd.Parameters.Add(":ND_Email", OracleDbType.Varchar2).Value = nguoiDung.ND_Email;
//                        cmd.Parameters.Add(":ND_CCCD", OracleDbType.Varchar2).Value = encryptedCCCD;
//                        cmd.Parameters.Add(":ND_NgaySinh", OracleDbType.Date).Value = nguoiDung.ND_NgaySinh;
//                        cmd.Parameters.Add(":ND_GioiTinh", OracleDbType.Varchar2).Value = nguoiDung.ND_GioiTinh;
//                        cmd.Parameters.Add(":ND_QuocGia", OracleDbType.Varchar2).Value = nguoiDung.ND_QuocGia;
//                        cmd.Parameters.Add(":ND_DanToc", OracleDbType.Varchar2).Value = nguoiDung.ND_DanToc;
//                        cmd.Parameters.Add(":ND_NgheNghiep", OracleDbType.Varchar2).Value = nguoiDung.ND_NgheNghiep;
//                        cmd.Parameters.Add(":ND_TinhThanh", OracleDbType.Varchar2).Value = encryptedTinhThanh;
//                        cmd.Parameters.Add(":ND_QuanHuyen", OracleDbType.Varchar2).Value = encryptedQuanHuyen;
//                        cmd.Parameters.Add(":ND_PhuongXa", OracleDbType.Varchar2).Value = encryptedPhuongXa;
//                        cmd.Parameters.Add(":ND_DiaChiThuongChu", OracleDbType.Varchar2).Value = encryptedDiaChiThuongChu;

//                        cmd.ExecuteNonQuery();
//                    }

//                    // Gán lại ID vào Patient để liên kết
//                    benhNhan.ND_IdNguoiDung = nguoiDung.ND_IdNguoiDung;

//                    // INSERT vào bảng BENHNHAN
//                    using (OracleCommand cmd = new OracleCommand(@"
//                INSERT INTO BenhNhan (BN_MaBenhNhan, BN_SoBaoHiemYT, BN_TieuSuBenhAn) 
//                VALUES (:BN_MaBenhNhan, :BN_SoBaoHiemYT, :BN_TieuSuBenhAn)", conn))
//                    {
//                        cmd.Parameters.Add(":BN_MaBenhNhan", OracleDbType.Varchar2).Value = benhNhan.BN_MaBenhNhan;
//                        cmd.Parameters.Add(":BN_SoBaoHiemYT", OracleDbType.Varchar2).Value = encryptedSoBaoHiemYT;
//                        cmd.Parameters.Add(":BN_TieuSuBenhAn", OracleDbType.Varchar2).Value = encryptedTieuSuBenhAn;

//                        cmd.ExecuteNonQuery();
//                    }


//                    conn.Close();
//                }

//                ViewBag.SuccessMessage = "Hồ sơ đã được tạo thành công!";
//            }
//            catch (Exception ex)
//            {
//                ViewBag.ErrorMessage = "Đã xảy ra lỗi khi lưu hồ sơ: " + ex.Message;
//            }

//            ViewBag.EncryptedData = new { NguoiDung = nguoiDung, Patient = benhNhan };

//            return View("HoSo");
//        }

//    }
//}
using DangKyKhamBenh.Models;
using DangKyKhamBenh.Services;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Configuration;
using System.Data;
using System.Web.Mvc;

namespace DangKyKhamBenh.Controllers
{
    public class HoSoController : Controller
    {
        private readonly CaesarCipher _caesarCipher;
        private readonly RsaService _rsaService;
        private readonly HybridService _hybridService;

        public HoSoController()
        {
            _caesarCipher = new CaesarCipher();
            _rsaService = new RsaService();
            _hybridService = new HybridService();
        }



        // Hàm mã hóa Hồ Sơ và lưu vào cơ sở dữ liệu Oracle
        [HttpGet]
        public ActionResult HoSo()
        {
            var userId = Session["ND_IdNguoiDung"]?.ToString();
            if (string.IsNullOrEmpty(userId))
            {
                ViewBag.ErrorMessage = "Không xác định được người dùng.";
                return View(new NguoiDung());
            }

            var model = new NguoiDung { ND_IdNguoiDung = userId };
            return View(model);
        }


        [HttpPost]
        public ActionResult HoSo(NguoiDung nguoiDung, Patient benhNhan)
        {
            if (nguoiDung == null || benhNhan == null || string.IsNullOrEmpty(nguoiDung.ND_IdNguoiDung))
            {
                ViewBag.ErrorMessage = "Dữ liệu không hợp lệ hoặc thiếu ID người dùng.";
                return View(new NguoiDung());
            }


            try
            {
                // Mã hóa dữ liệu
                nguoiDung.ND_HoTen = _caesarCipher.Encrypt(nguoiDung.ND_HoTen, 15);
                nguoiDung.ND_TinhThanh = _caesarCipher.Encrypt(nguoiDung.ND_TinhThanh, 15);
                nguoiDung.ND_QuanHuyen = _caesarCipher.Encrypt(nguoiDung.ND_QuanHuyen, 15);
                nguoiDung.ND_PhuongXa = _caesarCipher.Encrypt(nguoiDung.ND_PhuongXa, 15);

                nguoiDung.ND_SoDienThoai = _rsaService.Encrypt(nguoiDung.ND_SoDienThoai);
                nguoiDung.ND_DiaChiThuongChu = _rsaService.Encrypt(nguoiDung.ND_DiaChiThuongChu);
                benhNhan.BN_TieuSuBenhAn = _rsaService.Encrypt(benhNhan.BN_TieuSuBenhAn);

                var (encEmail, _) = _hybridService.Encrypt(nguoiDung.ND_Email, 15);
                var (encCCCD, _) = _hybridService.Encrypt(nguoiDung.ND_CCCD, 15);
                var (encBaoHiem, _) = _hybridService.Encrypt(benhNhan.BN_SoBaoHiemYT, 15);

                nguoiDung.ND_Email = encEmail;
                nguoiDung.ND_CCCD = encCCCD;
                benhNhan.BN_SoBaoHiemYT = encBaoHiem;

                using (var conn = new OracleConnection(ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString))
                {
                    conn.Open();

                    // Kiểm tra tồn tại NGUOIDUNG
                    bool nguoiDungTonTai;
                    using (var check = new OracleCommand("SELECT COUNT(*) FROM NGUOIDUNG WHERE ND_IdNguoiDung = :id", conn))
                    {
                        check.Parameters.Add(":id", OracleDbType.Varchar2).Value = nguoiDung.ND_IdNguoiDung;
                        nguoiDungTonTai = Convert.ToInt32(check.ExecuteScalar()) > 0;
                    }

                    if (nguoiDungTonTai)
                    {
                        // UPDATE NGUOIDUNG
                        using (var cmd = new OracleCommand(@"
                    UPDATE NGUOIDUNG SET
                        ND_CCCD = :cccd,
                        ND_GioiTinh = :gt,
                        ND_QuocGia = :qg,
                        ND_DanToc = :dt,
                        ND_NgheNghiep = :nn,
                        ND_TinhThanh = :tt,
                        ND_QuanHuyen = :qh,
                        ND_PhuongXa = :px,
                        ND_DiaChiThuongChu = :dc,
                        ND_Email = :email,
                        ND_SoDienThoai = :sdt
                    WHERE ND_IdNguoiDung = :id", conn))
                        {
                            cmd.Parameters.Add(":cccd", encCCCD);
                            cmd.Parameters.Add(":gt", nguoiDung.ND_GioiTinh);
                            cmd.Parameters.Add(":qg", nguoiDung.ND_QuocGia);
                            cmd.Parameters.Add(":dt", nguoiDung.ND_DanToc);
                            cmd.Parameters.Add(":nn", nguoiDung.ND_NgheNghiep);
                            cmd.Parameters.Add(":tt", nguoiDung.ND_TinhThanh);
                            cmd.Parameters.Add(":qh", nguoiDung.ND_QuanHuyen);
                            cmd.Parameters.Add(":px", nguoiDung.ND_PhuongXa);
                            cmd.Parameters.Add(":dc", nguoiDung.ND_DiaChiThuongChu);
                            cmd.Parameters.Add(":email", nguoiDung.ND_Email);
                            cmd.Parameters.Add(":sdt", nguoiDung.ND_SoDienThoai);
                            cmd.Parameters.Add(":id", nguoiDung.ND_IdNguoiDung);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // INSERT NGUOIDUNG
                        using (var cmd = new OracleCommand(@"
                    INSERT INTO NGUOIDUNG
                    (ND_IdNguoiDung, ND_HoTen, ND_SoDienThoai, ND_Email, ND_CCCD, ND_NgaySinh, ND_GioiTinh,
                     ND_QuocGia, ND_DanToc, ND_NgheNghiep, ND_TinhThanh, ND_QuanHuyen, ND_PhuongXa, ND_DiaChiThuongChu)
                    VALUES
                    (:id, :ht, :sdt, :email, :cccd, :dob, :gt, :qg, :dt, :nn, :tt, :qh, :px, :dc)", conn))
                        {
                            cmd.Parameters.Add(":id", nguoiDung.ND_IdNguoiDung);
                            cmd.Parameters.Add(":ht", nguoiDung.ND_HoTen);
                            cmd.Parameters.Add(":sdt", nguoiDung.ND_SoDienThoai);
                            cmd.Parameters.Add(":email", nguoiDung.ND_Email);
                            cmd.Parameters.Add(":cccd", encCCCD);
                            cmd.Parameters.Add(":dob", nguoiDung.ND_NgaySinh);
                            cmd.Parameters.Add(":gt", nguoiDung.ND_GioiTinh);
                            cmd.Parameters.Add(":qg", nguoiDung.ND_QuocGia);
                            cmd.Parameters.Add(":dt", nguoiDung.ND_DanToc);
                            cmd.Parameters.Add(":nn", nguoiDung.ND_NgheNghiep);
                            cmd.Parameters.Add(":tt", nguoiDung.ND_TinhThanh);
                            cmd.Parameters.Add(":qh", nguoiDung.ND_QuanHuyen);
                            cmd.Parameters.Add(":px", nguoiDung.ND_PhuongXa);
                            cmd.Parameters.Add(":dc", nguoiDung.ND_DiaChiThuongChu);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Kiểm tra tồn tại BENHNHAN
                    bool benhNhanTonTai;
                    using (var check = new OracleCommand("SELECT COUNT(*) FROM BENHNHAN WHERE BN_MaBenhNhan = :ma", conn))
                    {
                        check.Parameters.Add(":ma", OracleDbType.Varchar2).Value = benhNhan.BN_MaBenhNhan;
                        benhNhanTonTai = Convert.ToInt32(check.ExecuteScalar()) > 0;
                    }

                    if (benhNhanTonTai)
                    {
                        // UPDATE BENHNHAN
                        using (var cmd = new OracleCommand(@"
                    UPDATE BENHNHAN SET
                        BN_SoBaoHiemYT = :sbh,
                        BN_TieuSuBenhAn = :tsba
                    WHERE BN_MaBenhNhan = :ma", conn))
                        {
                            cmd.Parameters.Add(":sbh", benhNhan.BN_SoBaoHiemYT);
                            cmd.Parameters.Add(":tsba", benhNhan.BN_TieuSuBenhAn);
                            cmd.Parameters.Add(":ma", benhNhan.BN_MaBenhNhan);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // INSERT BENHNHAN
                        using (var cmd = new OracleCommand(@"
                    INSERT INTO BENHNHAN
                    (BN_MaBenhNhan, BN_SoBaoHiemYT, BN_TieuSuBenhAn, ND_IdNguoiDung)
                    VALUES
                    (:ma, :sbh, :tsba, :id)", conn))
                        {
                            cmd.Parameters.Add(":ma", benhNhan.BN_MaBenhNhan);
                            cmd.Parameters.Add(":sbh", benhNhan.BN_SoBaoHiemYT);
                            cmd.Parameters.Add(":tsba", benhNhan.BN_TieuSuBenhAn);
                            cmd.Parameters.Add(":id", nguoiDung.ND_IdNguoiDung);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    conn.Close();
                }

                ViewBag.SuccessMessage = "Hồ sơ đã được lưu thành công!";
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Lỗi khi lưu hồ sơ: " + ex.Message;
            }

            return View(nguoiDung);
        }
    }
}

