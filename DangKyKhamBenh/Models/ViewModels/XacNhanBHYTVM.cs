using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class XacNhanBHYTVM
    {
        public string SlotId { get; set; }
        public string DV_MaDichVu { get; set; }
        public DateTime NgayKham { get; set; }

        // Info hiển thị như ảnh
        public string TenDichVu { get; set; }
        public decimal TienKham { get; set; }
        public string GioBD { get; set; }
        public string GioKT { get; set; }
        public string TenPhong { get; set; }
        public string ViTri { get; set; }
        public string TenBacSi { get; set; }

        // Form lựa chọn
        public string BhytCase { get; set; }          // "1".."4"
        public bool? BaoLanhVienPhi { get; set; }     // true/false
    }
}