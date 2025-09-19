using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class BacSi
    {
        // Khóa/định danh
        [Required]
        public string MaBacSi { get; set; }         

        // ----------- Thông tin NGUOIDUNG ------------
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

        // -------------- Thông tin BACSI --------------
        [Required, Display(Name = "Chuyên khoa")]
        public string ChuyenKhoa { get; set; }      

        [Display(Name = "Chức danh")]
        public string ChucDanh { get; set; }   

        [Range(0, 80), Display(Name = "Năm kinh nghiệm")]
        public int? NamKinhNghiem { get; set; }       

        // -------------- Thông tin TAIKHOAN -----------
        [Required, Display(Name = "Tài khoản")]
        public string UserName { get; set; }      

        [Display(Name = "Mật khẩu (để trống nếu không đổi)")]
        public string NewPassword { get; set; }     

        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }
    }
}