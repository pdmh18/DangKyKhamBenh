using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models
{
    public class ForgotPasswordOtpViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tài khoản.")]
        [Display(Name = "Tài khoản")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email đã đăng ký.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Mật khẩu mới")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Display(Name = "Xác nhận mật khẩu mới")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Mã OTP")]
        public string Otp { get; set; }

        // Cờ để view biết có nên hiển thị ô OTP hay chưa
        public bool OtpSent { get; set; }
    }
}