using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models
{
    public class NguoiDung
    {
        public string ND_IdNguoiDung { get; set; }
        public string ND_HoTen { get; set; }
        public string ND_SoDienThoai { get; set; }
        public string ND_Email { get; set; }
        public string ND_CCCD { get; set; }
        public DateTime ND_NgaySinh { get; set; }
        public string ND_GioiTinh { get; set; }
        public string ND_QuocGia { get; set; }
        public string ND_DanToc { get; set; }
        public string ND_NgheNghiep { get; set; }
        public string ND_TinhThanh { get; set; }
        public string ND_QuanHuyen { get; set; }
        public string ND_PhuongXa { get; set; }
        public string ND_DiaChiThuongChu { get; set; }
    }
}