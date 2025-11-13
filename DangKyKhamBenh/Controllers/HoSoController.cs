using DangKyKhamBenh.Models;
using DangKyKhamBenh.Models.ViewModels;
using DangKyKhamBenh.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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
        public ActionResult HoSo()
        {
            return View("HoSo");
        }

        // Hàm mã hóa Hồ Sơ
        [HttpPost]
        public ActionResult HoSo(NguoiDung nguoiDung, Patient benhNhan)
        {
            if (nguoiDung == null || benhNhan == null)
            {
                ViewBag.ErrorMessage = "Dữ liệu không hợp lệ!";
                return View();
            }

            // Kiểm tra nếu các giá trị trong model hợp lệ
            if (string.IsNullOrEmpty(nguoiDung.ND_HoTen) || string.IsNullOrEmpty(nguoiDung.ND_SoDienThoai))
            {
                ViewBag.ErrorMessage = "Một số thông tin bắt buộc chưa được điền!";
                return View();
            }

            // Mã hóa các trường theo yêu cầu
            string encryptedHoTen = _caesarCipher.Encrypt(nguoiDung.ND_HoTen, 3); // Giả sử key = 3
            string encryptedTinhThanh = _caesarCipher.Encrypt(nguoiDung.ND_TinhThanh, 3);
            string encryptedQuanHuyen = _caesarCipher.Encrypt(nguoiDung.ND_QuanHuyen, 3);
            string encryptedPhuongXa = _caesarCipher.Encrypt(nguoiDung.ND_PhuongXa, 3);

            string encryptedSoDienThoai = _rsaService.Encrypt(nguoiDung.ND_SoDienThoai);
            string encryptedDiaChiThuongChu = _rsaService.Encrypt(nguoiDung.ND_DiaChiThuongChu);
            string encryptedTieuSuBenhAn = _rsaService.Encrypt(benhNhan.BN_TieuSuBenhAn);

            var (encryptedEmail, encryptedKeyEmail) = _hybridService.Encrypt(nguoiDung.ND_Email, 1);
            var (encryptedCCCD, encryptedKeyCCCD) = _hybridService.Encrypt(nguoiDung.ND_CCCD, 1);
            var (encryptedSoBaoHiemYT, encryptedKeySoBaoHiemYT) = _hybridService.Encrypt(benhNhan.BN_SoBaoHiemYT, 1);

            // Lưu thông tin vào cơ sở dữ liệu
            nguoiDung.ND_HoTen = encryptedHoTen;
            nguoiDung.ND_TinhThanh = encryptedTinhThanh;
            nguoiDung.ND_QuanHuyen = encryptedQuanHuyen;
            nguoiDung.ND_PhuongXa = encryptedPhuongXa;
            nguoiDung.ND_SoDienThoai = encryptedSoDienThoai;
            nguoiDung.ND_DiaChiThuongChu = encryptedDiaChiThuongChu;
            benhNhan.BN_TieuSuBenhAn = encryptedTieuSuBenhAn;
            nguoiDung.ND_Email = encryptedEmail;
            nguoiDung.ND_CCCD = encryptedCCCD;
            benhNhan.BN_SoBaoHiemYT = encryptedSoBaoHiemYT;
            

            // Kiểm tra kết quả mã hóa
            ViewBag.EncryptedData = new { NguoiDung = nguoiDung, BenhNhan = benhNhan };

            // Thông báo thành công
            ViewBag.SuccessMessage = "Hồ sơ đã được tạo thành công!";
            return View();
        }


       
    }
}