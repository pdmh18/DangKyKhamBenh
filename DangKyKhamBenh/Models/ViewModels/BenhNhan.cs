using System;
using System.ComponentModel.DataAnnotations;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class BenhNhan
    {
        [Required]
        public string BN_MaBenhNhan { get; set; }

        // NGUOIDUNG
        public string ND_IdNguoiDung { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Họ tên")]
        public string ND_HoTen { get; set; }    

        [Phone, Display(Name = "Số điện thoại")]
        public string ND_SoDienThoai { get; set; }  

        [EmailAddress, Display(Name = "Email")]
        public string ND_Email { get; set; }    
        
        public string ND_CCCD { get; set; }



        [Display(Name = "Ngày sinh")]
        public DateTime? ND_NgaySinh { get; set; }    

        [Display(Name = "Giới tính")]
        public string ND_GioiTinh { get; set; }

        public string ND_QuocGia { get; set; }
        public string ND_DanToc { get; set; }
        public string ND_NgheNghiep { get; set; }
        public string ND_TinhThanh { get; set; }
        public string ND_QuanHuyen { get; set; }
        public string ND_PhuongXa { get; set; }

        [Display(Name = "Địa chỉ")]
        public string ND_DiaChiThuongChu { get; set; }     

        // TAIKHOAN
        [Display(Name = "Username")]
        public string UserName { get; set; }   

        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }       

        // Thông tin phụ BN (tuỳ có/không trong DB)
        public string BN_SoBaoHiemYT { get; set; }   
        public string BN_NhomMau { get; set; }   
        public string BN_TieuSuBenhAn { get; set; }     
        


    }
}
