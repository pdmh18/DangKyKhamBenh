using DangKyKhamBenh.Models.ViewModels;
using DangKyKhamBenh.Services;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DangKyKhamBenh.Controllers
{
    public class HoSoBacSiController : Controller
    {
        private readonly CaesarCipher _caesarCipher;
        private readonly RsaService _rsaService;
        private readonly HybridService _hybridService;

        public HoSoBacSiController()
        {
            _caesarCipher = new CaesarCipher();
            _rsaService = new RsaService();
            _hybridService = new HybridService();
        }

        [HttpGet]
        public ActionResult CreateHoSoBacSi()
        {
            var userId = Session["ND_IdNguoiDung"]?.ToString();
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Err"] = "Không xác định được người dùng. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Account");
            }

            var maBacSi = Session["MaBacSi"] as string;
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            BacSi model = new BacSi
            {
                ND_IdNguoiDung = userId,
                BS_MaBacSi = maBacSi
            };

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();

                // Nếu chưa có MaBacSi trong session thì tra theo ND_IdNguoiDung
                if (string.IsNullOrEmpty(maBacSi))
                {
                    using (var cmd = new OracleCommand(@"
                SELECT BS_MaBacSi 
                FROM   BACSI 
                WHERE  ND_IdNguoiDung = :id", conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("id", userId);
                        var o = cmd.ExecuteScalar();
                        if (o != null && o != DBNull.Value)
                        {
                            maBacSi = o.ToString();
                            Session["MaBacSi"] = maBacSi;
                            model.BS_MaBacSi = maBacSi;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(maBacSi))
                {
                    using (var cmd = new OracleCommand(@"
                SELECT 
                    bs.BS_MaBacSi,
                    bs.BS_ChuyenKhoa,
                    bs.BS_ChucDanh,
                    bs.BS_NamKinhNghiem,
                    bs.K_MaKhoa,
                    nd.ND_IdNguoiDung,
                    nd.ND_HoTen,
                    nd.ND_SoDienThoai,
                    nd.ND_Email,
                    nd.ND_CCCD,
                    nd.ND_NgaySinh,
                    nd.ND_GioiTinh,
                    nd.ND_QuocGia,
                    nd.ND_DanToc,
                    nd.ND_NgheNghiep,
                    nd.ND_TinhThanh,
                    nd.ND_QuanHuyen,
                    nd.ND_PhuongXa,
                    nd.ND_DiaChiThuongChu
                FROM BACSI bs
                JOIN NGUOIDUNG nd ON nd.ND_IdNguoiDung = bs.ND_IdNguoiDung
                WHERE bs.BS_MaBacSi = :ma", conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("ma", maBacSi);

                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                model.BS_MaBacSi = r["BS_MaBacSi"]?.ToString();
                                model.BS_ChuyenKhoa = r["BS_ChuyenKhoa"]?.ToString();
                                model.BS_ChucDanh = r["BS_ChucDanh"]?.ToString();
                                model.BS_NamKinhNghiem = r.IsDBNull(r.GetOrdinal("BS_NamKinhNghiem"))
                                    ? (int?)null
                                    : Convert.ToInt32(r.GetDecimal(r.GetOrdinal("BS_NamKinhNghiem")));
                                model.K_MaKhoa = r["K_MaKhoa"]?.ToString();

                                model.ND_IdNguoiDung = r["ND_IdNguoiDung"]?.ToString();
                                model.ND_HoTen = r["ND_HoTen"]?.ToString();
                                model.ND_SoDienThoai = r["ND_SoDienThoai"]?.ToString();
                                model.ND_Email = r["ND_Email"]?.ToString();
                                model.ND_CCCD = r["ND_CCCD"]?.ToString();
                                model.ND_NgaySinh = r.IsDBNull(r.GetOrdinal("ND_NgaySinh"))
                                    ? (DateTime?)null
                                    : r.GetDateTime(r.GetOrdinal("ND_NgaySinh"));
                                model.ND_GioiTinh = r["ND_GioiTinh"]?.ToString();
                                model.ND_QuocGia = r["ND_QuocGia"]?.ToString();
                                model.ND_DanToc = r["ND_DanToc"]?.ToString();
                                model.ND_NgheNghiep = r["ND_NgheNghiep"]?.ToString();
                                model.ND_TinhThanh = r["ND_TinhThanh"]?.ToString();
                                model.ND_QuanHuyen = r["ND_QuanHuyen"]?.ToString();
                                model.ND_PhuongXa = r["ND_PhuongXa"]?.ToString();
                                model.ND_DiaChiThuongChu = r["ND_DiaChiThuongChu"]?.ToString();

                                // Giải mã
                                try
                                {
                                    model.ND_TinhThanh = _caesarCipher.Decrypt(model.ND_TinhThanh, 15);
                                    model.ND_QuanHuyen = _caesarCipher.Decrypt(model.ND_QuanHuyen, 15);
                                    model.ND_PhuongXa = _caesarCipher.Decrypt(model.ND_PhuongXa, 15);

                                    model.ND_SoDienThoai = _rsaService.Decrypt(model.ND_SoDienThoai);
                                    model.ND_DiaChiThuongChu = _rsaService.Decrypt(model.ND_DiaChiThuongChu);
                                    //model.BS_ChucDanh = _rsaService.Decrypt(model.BS_ChucDanh);

                                    if (!string.IsNullOrEmpty(model.BS_MaBacSi))
                                    {
                                        model.ND_Email = _hybridService.Decrypt(model.ND_Email, model.BS_MaBacSi);
                                        model.ND_CCCD = _hybridService.Decrypt(model.ND_CCCD, model.BS_MaBacSi);
                                       // model.BS_ChuyenKhoa = _hybridService.Decrypt(model.BS_ChuyenKhoa, model.BS_MaBacSi);
                                    }

                                }
                                catch (Exception ex)
                                {
                                    TempData["Err"] = "Lỗi giải mã hồ sơ bác sĩ: " + ex.Message;
                                }
                            }
                        }
                    }
                }
            }

            // Luôn load dropdown khoa (nếu model.K_MaKhoa có rồi thì chọn sẵn)
            LoadKhoaDropDown(model.K_MaKhoa);

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateHoSoBacSi(BacSi model)
        {
            if (model == null || string.IsNullOrEmpty(model.ND_IdNguoiDung))
            {
                TempData["Err"] = "Dữ liệu không hợp lệ hoặc thiếu ID người dùng.";
                return RedirectToAction("CreateHoSoBacSi");
            }
            if (string.IsNullOrEmpty(model.BS_MaBacSi))
            {
                TempData["Err"] = "Không tìm thấy mã bác sĩ. Vui lòng thử lại.";
                return RedirectToAction("CreateHoSoBacSi");
            }

            try
            {
                // MÃ HÓA
                model.ND_TinhThanh = _caesarCipher.Encrypt(model.ND_TinhThanh, 15);
                model.ND_QuanHuyen = _caesarCipher.Encrypt(model.ND_QuanHuyen, 15);
                model.ND_PhuongXa = _caesarCipher.Encrypt(model.ND_PhuongXa, 15);

                model.ND_SoDienThoai = _rsaService.Encrypt(model.ND_SoDienThoai);
                model.ND_DiaChiThuongChu = _rsaService.Encrypt(model.ND_DiaChiThuongChu);
                //model.BS_ChucDanh = _rsaService.Encrypt(model.BS_ChucDanh);

                model.ND_Email = _hybridService.Encrypt(model.ND_Email, model.BS_MaBacSi);
                model.ND_CCCD = _hybridService.Encrypt(model.ND_CCCD, model.BS_MaBacSi);
                //model.BS_ChuyenKhoa = _hybridService.Encrypt(model.BS_ChuyenKhoa, model.BS_MaBacSi);
                // K_MaKhoa: GIỮ NGUYÊN, KHÔNG MÃ HÓA

                var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();

                    // 1) NGUOIDUNG
                    bool nguoiDungTonTai;
                    using (var check = new OracleCommand(
                        "SELECT COUNT(*) FROM NGUOIDUNG WHERE ND_IdNguoiDung = :id", conn))
                    {
                        check.BindByName = true;
                        check.Parameters.Add("id", OracleDbType.Varchar2).Value = model.ND_IdNguoiDung;
                        nguoiDungTonTai = Convert.ToInt32(check.ExecuteScalar()) > 0;
                    }

                    if (nguoiDungTonTai)
                    {
                        using (var cmd = new OracleCommand(@"
                    UPDATE NGUOIDUNG SET
                        ND_HoTen           = :ht,
                        ND_CCCD            = :cccd,
                        ND_GioiTinh        = :gt,
                        ND_QuocGia         = :qg,
                        ND_DanToc          = :dt,
                        ND_NgheNghiep      = :nn,
                        ND_TinhThanh       = :tt,
                        ND_QuanHuyen       = :qh,
                        ND_PhuongXa        = :px,
                        ND_DiaChiThuongChu = :dc,
                        ND_Email           = :email,
                        ND_SoDienThoai     = :sdt,
                        ND_NgaySinh = : ns
                    WHERE ND_IdNguoiDung = :id", conn))
                        {
                            cmd.BindByName = true;
                            cmd.Parameters.Add("ht", model.ND_HoTen);
                            cmd.Parameters.Add("cccd", model.ND_CCCD);
                            cmd.Parameters.Add("gt", model.ND_GioiTinh);
                            cmd.Parameters.Add("qg", model.ND_QuocGia);
                            cmd.Parameters.Add("dt", model.ND_DanToc);
                            cmd.Parameters.Add("nn", model.ND_NgheNghiep);
                            cmd.Parameters.Add("tt", model.ND_TinhThanh);
                            cmd.Parameters.Add("qh", model.ND_QuanHuyen);
                            cmd.Parameters.Add("px", model.ND_PhuongXa);
                            cmd.Parameters.Add("dc", model.ND_DiaChiThuongChu);
                            cmd.Parameters.Add("email", model.ND_Email);
                            cmd.Parameters.Add("sdt", model.ND_SoDienThoai);
                            cmd.Parameters.Add("ns", model.ND_NgaySinh);
                            cmd.Parameters.Add("id", model.ND_IdNguoiDung);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        using (var cmd = new OracleCommand(@"
                    INSERT INTO NGUOIDUNG
                    (ND_IdNguoiDung, ND_HoTen, ND_SoDienThoai, ND_Email, ND_CCCD, ND_NgaySinh, ND_GioiTinh,
                     ND_QuocGia, ND_DanToc, ND_NgheNghiep, ND_TinhThanh, ND_QuanHuyen, ND_PhuongXa, ND_DiaChiThuongChu)
                    VALUES
                    (:id, :ht, :sdt, :email, :cccd, :dob, :gt, :qg, :dt, :nn, :tt, :qh, :px, :dc)", conn))
                        {
                            cmd.BindByName = true;
                            cmd.Parameters.Add("id", model.ND_IdNguoiDung);
                            cmd.Parameters.Add("ht", model.ND_HoTen);
                            cmd.Parameters.Add("sdt", model.ND_SoDienThoai);
                            cmd.Parameters.Add("email", model.ND_Email);
                            cmd.Parameters.Add("cccd", model.ND_CCCD);
                            cmd.Parameters.Add("dob", model.ND_NgaySinh);
                            cmd.Parameters.Add("gt", model.ND_GioiTinh);
                            cmd.Parameters.Add("qg", model.ND_QuocGia);
                            cmd.Parameters.Add("dt", model.ND_DanToc);
                            cmd.Parameters.Add("nn", model.ND_NgheNghiep);
                            cmd.Parameters.Add("tt", model.ND_TinhThanh);
                            cmd.Parameters.Add("qh", model.ND_QuanHuyen);
                            cmd.Parameters.Add("px", model.ND_PhuongXa);
                            cmd.Parameters.Add("dc", model.ND_DiaChiThuongChu);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // 2) BACSI
                    bool bacSiTonTai;
                    using (var check = new OracleCommand(
                        "SELECT COUNT(*) FROM BACSI WHERE BS_MaBacSi = :ma", conn))
                    {
                        check.BindByName = true;
                        check.Parameters.Add("ma", OracleDbType.Varchar2).Value = model.BS_MaBacSi;
                        bacSiTonTai = Convert.ToInt32(check.ExecuteScalar()) > 0;
                    }

                    if (bacSiTonTai)
                    {
                        using (var cmd = new OracleCommand(@"
                    UPDATE BACSI
                    SET BS_ChuyenKhoa    = :ck,
                        BS_ChucDanh      = :cd,
                        BS_NamKinhNghiem = :nam,
                        K_MaKhoa         = :khoa
                    WHERE BS_MaBacSi     = :ma", conn))
                        {
                            cmd.BindByName = true;
                            cmd.Parameters.Add("ck", (object)model.BS_ChuyenKhoa ?? DBNull.Value);
                            cmd.Parameters.Add("cd", (object)model.BS_ChucDanh ?? DBNull.Value);
                            cmd.Parameters.Add("nam", (object)model.BS_NamKinhNghiem ?? DBNull.Value);
                            cmd.Parameters.Add("khoa", (object)model.K_MaKhoa ?? DBNull.Value);
                            cmd.Parameters.Add("ma", model.BS_MaBacSi);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        using (var cmd = new OracleCommand(@"
                    INSERT INTO BACSI
                    (BS_MaBacSi, BS_ChuyenKhoa, BS_ChucDanh, BS_NamKinhNghiem, ND_IdNguoiDung, K_MaKhoa)
                    VALUES
                    (:ma, :ck, :cd, :nam, :nd, :khoa)", conn))
                        {
                            cmd.BindByName = true;
                            cmd.Parameters.Add("ma", model.BS_MaBacSi);
                            cmd.Parameters.Add("ck", (object)model.BS_ChuyenKhoa ?? DBNull.Value);
                            cmd.Parameters.Add("cd", (object)model.BS_ChucDanh ?? DBNull.Value);
                            cmd.Parameters.Add("nam", (object)model.BS_NamKinhNghiem ?? DBNull.Value);
                            cmd.Parameters.Add("nd", model.ND_IdNguoiDung);
                            cmd.Parameters.Add("khoa", (object)model.K_MaKhoa ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                TempData["Msg"] = "Hồ sơ bác sĩ đã được lưu thành công.";
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Lỗi khi lưu hồ sơ bác sĩ: " + ex.Message;
            }

            return RedirectToAction("CreateHoSoBacSi");
        }




        private void LoadKhoaDropDown(string selected = null)
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            var list = new List<SelectListItem>();

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                using (var cmd = new OracleCommand(
                    "SELECT K_MaKhoa, K_TenKhoa FROM KHOA ORDER BY K_TenKhoa", conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var ma = r["K_MaKhoa"]?.ToString();
                        var ten = r["K_TenKhoa"]?.ToString();

                        list.Add(new SelectListItem
                        {
                            Value = ma,
                            Text = ten,
                            Selected = (selected != null && selected == ma)
                        });
                    }
                }
            }

            ViewBag.KhoaList = list;
        }


    }
}