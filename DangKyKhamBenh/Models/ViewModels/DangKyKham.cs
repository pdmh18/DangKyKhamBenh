using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class DangKyKham
    {
        // đăng ký khám
        public string DK_MaPhieuKham { get; set; }      
        public DateTime? DK_NgayKham { get; set; }     
        public string DK_TrieuTrung { get; set; }       
        public string DK_ChuanDoan { get; set; }       
        public string BN_MaBenhNhan { get; set; }       
        public string K_MaKhoa { get; set; }


        //khoa
        public string K_TenKhoa { get; set; }  
        public string K_SoDienThoai { get; set; }
        public string K_Email { get; set; }


        // dịch vụ 
        public string DV_MaDichVu { get; set; }    
        public string DV_TenDichVu { get; set; }     
        public decimal? DV_GiaTien { get; set; }
    }
}