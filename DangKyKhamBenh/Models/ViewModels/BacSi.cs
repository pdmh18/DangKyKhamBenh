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
        public string BS_MABACSI { get; set; }         

        // ----------- Thông tin NGUOIDUNG ------------
        [Required, StringLength(100)]
        [Display(Name = "Họ tên")]
        public string ND_HOTEN { get; set; }           

        [Phone, Display(Name = "Số điện thoại")]
        public string ND_SODIENTHOAI { get; set; }   

        [EmailAddress, Display(Name = "Email")]
        public string ND_EMAIL { get; set; }     

        [Display(Name = "Ngày sinh")]
        public DateTime? ND_NGAYSINH { get; set; }    

        [Display(Name = "Địa chỉ")]
        public string ND_DIACHITHUONGCHU { get; set; }         

        // -------------- Thông tin BACSI --------------
        [Required, Display(Name = "Chuyên khoa")]
        public string BS_CHUYENKHOA { get; set; }      

        [Display(Name = "Chức danh")]
        public string BS_CHUCDANH { get; set; }   

        [Range(0, 80), Display(Name = "Năm kinh nghiệm")]
        public int? BS_NAMKINHNGHIEM { get; set; }       

        // -------------- Thông tin TAIKHOAN -----------
        [Required, Display(Name = "Tài khoản")]
        public string UserName { get; set; }      

        [Display(Name = "Mật khẩu (để trống nếu không đổi)")]
        public string NewPassword { get; set; }     

        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }
    }
}