using DangKyKhamBenh.Models.ViewModels;
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
                return View(new BenhNhan());
            }

            var model = new BenhNhan { ND_IdNguoiDung = userId };
            return View(model);
        }


        [HttpPost]
        public ActionResult HoSo(BenhNhan model)
        {
            if (model == null || model == null || string.IsNullOrEmpty(model.ND_IdNguoiDung))
            {
                ViewBag.ErrorMessage = "Dữ liệu không hợp lệ hoặc thiếu ID người dùng.";
                return View(new BenhNhan());
            }


            try
            {
                model.ND_TinhThanh = _caesarCipher.Encrypt(model.ND_TinhThanh, 15);
                model.ND_QuanHuyen = _caesarCipher.Encrypt(model.ND_QuanHuyen, 15);
                model.ND_PhuongXa = _caesarCipher.Encrypt(model.ND_PhuongXa, 15);

                model.ND_SoDienThoai = _rsaService.Encrypt(model.ND_SoDienThoai);
                model.ND_DiaChiThuongChu = _rsaService.Encrypt(model.ND_DiaChiThuongChu);
                model.BN_TieuSuBenhAn = _rsaService.Encrypt( model.BN_TieuSuBenhAn);

                model.ND_Email = _hybridService.Encrypt(model.ND_Email, model.BN_MaBenhNhan);
                model.ND_CCCD = _hybridService.Encrypt( model.ND_CCCD, model.BN_MaBenhNhan);
                model.BN_SoBaoHiemYT = _hybridService.Encrypt( model.BN_SoBaoHiemYT, model.BN_MaBenhNhan);


                using (var conn = new OracleConnection(ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString))
                {
                    conn.Open();

                    // Kiểm tra tồn tại NGUOIDUNG
                    bool nguoiDungTonTai;
                    using (var check = new OracleCommand("SELECT COUNT(*) FROM NGUOIDUNG WHERE ND_IdNguoiDung = :id", conn))
                    {
                        check.Parameters.Add(":id", OracleDbType.Varchar2).Value = model.ND_IdNguoiDung;
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
                            //cmd.Parameters.Add(":cccd", encCCCD);
                            cmd.Parameters.Add(":cccd", model.ND_CCCD);
                            cmd.Parameters.Add(":gt", model.ND_GioiTinh);
                            cmd.Parameters.Add(":qg", model.ND_QuocGia);
                            cmd.Parameters.Add(":dt", model.ND_DanToc);
                            cmd.Parameters.Add(":nn", model.ND_NgheNghiep);
                            cmd.Parameters.Add(":tt", model.ND_TinhThanh);
                            cmd.Parameters.Add(":qh", model.ND_QuanHuyen);
                            cmd.Parameters.Add(":px", model.ND_PhuongXa);
                            cmd.Parameters.Add(":dc", model.ND_DiaChiThuongChu);
                            cmd.Parameters.Add(":email", model.ND_Email);
                            cmd.Parameters.Add(":sdt", model.ND_SoDienThoai);
                            cmd.Parameters.Add(":id", model.ND_IdNguoiDung);
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
                            cmd.Parameters.Add(":id", model.ND_IdNguoiDung);
                            cmd.Parameters.Add(":ht", model.ND_HoTen);
                            cmd.Parameters.Add(":sdt", model.ND_SoDienThoai);
                            cmd.Parameters.Add(":email", model.ND_Email);
                            //cmd.Parameters.Add(":cccd", encCCCD);
                            cmd.Parameters.Add(":cccd", model.ND_CCCD);
                            cmd.Parameters.Add(":dob", model.ND_NgaySinh);
                            cmd.Parameters.Add(":gt", model.ND_GioiTinh);
                            cmd.Parameters.Add(":qg", model.ND_QuocGia);
                            cmd.Parameters.Add(":dt", model.ND_DanToc);
                            cmd.Parameters.Add(":nn", model.ND_NgheNghiep);
                            cmd.Parameters.Add(":tt", model.ND_TinhThanh);
                            cmd.Parameters.Add(":qh", model.ND_QuanHuyen);
                            cmd.Parameters.Add(":px", model.ND_PhuongXa);
                            cmd.Parameters.Add(":dc", model.ND_DiaChiThuongChu);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Kiểm tra tồn tại BENHNHAN
                    bool benhNhanTonTai;
                    using (var check = new OracleCommand("SELECT COUNT(*) FROM BENHNHAN WHERE BN_MaBenhNhan = :ma", conn))
                    {
                        check.Parameters.Add(":ma", OracleDbType.Varchar2).Value =  model.BN_MaBenhNhan;
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
                            cmd.Parameters.Add(":sbh", model.BN_SoBaoHiemYT);
                            cmd.Parameters.Add(":tsba", model.BN_TieuSuBenhAn);
                            cmd.Parameters.Add(":ma", model.BN_MaBenhNhan);
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
                            cmd.Parameters.Add(":ma", model.BN_MaBenhNhan);
                            cmd.Parameters.Add(":sbh", model.BN_SoBaoHiemYT);
                            cmd.Parameters.Add(":tsba", model.BN_TieuSuBenhAn);
                            cmd.Parameters.Add(":id", model.ND_IdNguoiDung);
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

            return RedirectToAction("HoSoCaNhan", "HoSo", new { maBenhNhan = model.BN_MaBenhNhan });
        }

        public ActionResult HoSoCaNhan()
        {
            if (Session["User"] == null)
                return RedirectToAction("Login", "Account");

            var maBenhNhan = Session["MaBenhNhan"]?.ToString();
            if (string.IsNullOrEmpty(maBenhNhan))
            {
                TempData["Err"] = "Bạn chưa có hồ sơ. Vui lòng tạo hồ sơ trước.";
                return RedirectToAction("HoSo", "HoSo");
            }



            var model = new BenhNhan(); // ViewModel bạn đang dùng

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                var sql = @"
                    SELECT bn.*, nd.*
                    FROM BENHNHAN bn
                    JOIN NGUOIDUNG nd ON bn.ND_IdNguoiDung = nd.ND_IdNguoiDung
                    WHERE bn.BN_MaBenhNhan = :ma";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.Parameters.Add("ma", maBenhNhan);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Gán dữ liệu vào model
                            model.BN_MaBenhNhan = reader["BN_MaBenhNhan"].ToString();
                            model.ND_IdNguoiDung = reader["ND_IdNguoiDung"].ToString();
                            model.ND_HoTen = reader["ND_HoTen"].ToString();
                            model.ND_SoDienThoai = reader["ND_SoDienThoai"].ToString();
                            model.ND_Email = reader["ND_Email"].ToString();
                            model.ND_CCCD = reader["ND_CCCD"].ToString();
                            model.ND_NgaySinh = reader.GetDateTime(reader.GetOrdinal("ND_NgaySinh"));
                            model.ND_GioiTinh = reader["ND_GioiTinh"].ToString();
                            model.ND_QuocGia = reader["ND_QuocGia"].ToString();
                            model.ND_DanToc = reader["ND_DanToc"].ToString();
                            model.ND_NgheNghiep = reader["ND_NgheNghiep"].ToString();
                            model.ND_TinhThanh = reader["ND_TinhThanh"].ToString();
                            model.ND_QuanHuyen = reader["ND_QuanHuyen"].ToString();
                            model.ND_PhuongXa = reader["ND_PhuongXa"].ToString();
                            model.ND_DiaChiThuongChu = reader["ND_DiaChiThuongChu"].ToString();
                            model.BN_SoBaoHiemYT = reader["BN_SoBaoHiemYT"].ToString();
                            model.BN_NhomMau = reader["BN_NhomMau"].ToString();
                            model.BN_TieuSuBenhAn = reader["BN_TieuSuBenhAn"].ToString();
                            if (string.IsNullOrEmpty(model.BN_SoBaoHiemYT) ||
                                string.IsNullOrEmpty(model.BN_TieuSuBenhAn) ||
                                string.IsNullOrEmpty(model.BN_NhomMau) ||
                                string.IsNullOrEmpty(model.ND_CCCD) ||
                                string.IsNullOrEmpty(model.ND_GioiTinh) ||
                                string.IsNullOrEmpty(model.ND_QuocGia) ||
                                string.IsNullOrEmpty(model.ND_DanToc) ||
                                string.IsNullOrEmpty(model.ND_NgheNghiep) ||
                                string.IsNullOrEmpty(model.ND_TinhThanh) ||
                                string.IsNullOrEmpty(model.ND_QuanHuyen) ||
                                string.IsNullOrEmpty(model.ND_PhuongXa))
                            {
                                TempData["Err"] = "Bạn Chưa Có Hồ Sơ. Vui Lòng Lập Hồ Sơ";
                                return RedirectToAction("HoSo", "HoSo");
                            }

                        }
                        else
                        {
                            TempData["Err"] = "Không tìm thấy hồ sơ.";
                            return RedirectToAction("Index");
                        }
                    }
                }
            }

           
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
    }
}


