using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models
{
    public class Khoa
    {
        public string K_MaKhoa { get; set; }
        public string K_TenKhoa { get; set; }
        public string K_SoDienThoai { get; set; }
        public string K_Email { get; set; }
        public string K_TruongKhoa { get; set; }
        public int? K_SoLuongNhanVien { get; set; }
    }
}