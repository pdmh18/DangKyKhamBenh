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
        public string BS_MaBacSi { get; set; }

        // ----------- Thông tin NGUOIDUNG ------------
        public string ND_IdNguoiDung { get; set; }
        [Required, StringLength(100)]
        [Display(Name = "Họ tên")]
        public string ND_HoTen { get; set; }           

        [Phone, Display(Name = "Số điện thoại")]
        public string ND_SoDienThoai { get; set; }   

        [EmailAddress, Display(Name = "Email")]
        public string ND_Email { get; set; }     

        [Display(Name = "Ngày sinh")]
        public DateTime? ND_NgaySinh { get; set; }
        public string ND_CCCD { get; set; }
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

        // -------------- Thông tin BACSI --------------
        //[Required, Display(Name = "Chuyên khoa")]
        public string BS_ChuyenKhoa { get; set; }      

        [Display(Name = "Chức danh")]
        public string BS_ChucDanh { get; set; }   

        [Range(0, 80), Display(Name = "Năm kinh nghiệm")]
        public int? BS_NamKinhNghiem { get; set; }       

        public string K_MaKhoa { get; set; }

        // -------------- Thông tin TAIKHOAN -----------
        [Required, Display(Name = "Tài khoản")]
        public string TK_UserName { get; set; }          

        [Display(Name = "Trạng thái")]
        public string TK_TrangThai { get; set; }
    }
}