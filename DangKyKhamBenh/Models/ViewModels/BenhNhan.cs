using System;
using System.ComponentModel.DataAnnotations;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class BenhNhan
    {
        [Required]
        public string MaBenhNhan { get; set; }   

        // NGUOIDUNG
        [Required, StringLength(100)]
        [Display(Name = "Họ tên")]
        public string HoTen { get; set; }    

        [Phone, Display(Name = "Số điện thoại")]
        public string SoDienThoai { get; set; }  

        [EmailAddress, Display(Name = "Email")]
        public string Email { get; set; }     

        [Display(Name = "Ngày sinh")]
        public DateTime? NgaySinh { get; set; }    

        [Display(Name = "Địa chỉ")]
        public string DiaChi { get; set; }     

        // TAIKHOAN
        [Display(Name = "Username")]
        public string UserName { get; set; }   

        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }       

        // Thông tin phụ BN (tuỳ có/không trong DB)
        public string SoBaoHiemYT { get; set; }   
        public string NhomMau { get; set; }   
        public string TieuSuBenhAn { get; set; }      
    }
}
