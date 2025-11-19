using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Để sử dụng [NotMapped]

namespace DangKyKhamBenh.Models
{
    public class TaiKhoan
    {
        public int Id { get; set; }

        // Tên đăng nhập (chỉ cho phép chữ cái a-z và số 0-9)
        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc.")]
        [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Tên đăng nhập chỉ được phép chứa chữ cái và số.")]
        [StringLength(50, ErrorMessage = "Tên đăng nhập không được vượt quá 50 ký tự.")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
        public string Password { get; set; }

        [NotMapped]
        [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
        [Compare("Password", ErrorMessage = "Mật khẩu và xác nhận mật khẩu không khớp.")]
        public string ConfirmPassword { get; set; }

        // Email
        [Required(ErrorMessage = "Email là bắt buộc.")]
        [RegularExpression(
             @"^[a-zA-Z0-9._%+-]+@[a-zA-Z.-]+\.[a-zA-Z]{2,}$",
             ErrorMessage = "Địa chỉ email không hợp lệ.")]
        public string Email { get; set; }

        // Số điện thoại
        [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string PhoneNumber { get; set; }

        // Ngày sinh
        [Required(ErrorMessage = "Ngày sinh là bắt buộc.")]
        public DateTime? DateOfBirth { get; set; }

        // Địa chỉ
        [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
        public string Address { get; set; }

        // Vai trò
        public string Role { get; set; }

        // Loại nhân viên
        public string StaffType { get; set; }
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DateOfBirth.HasValue)
            {
                var today = DateTime.Today;
                var dob = DateOfBirth.Value.Date;

                var age = today.Year - dob.Year;
                if (dob > today.AddYears(-age)) age--;   // nếu sinh nhật năm nay chưa tới thì trừ thêm 1

                if (age < 15)
                {
                    yield return new ValidationResult(
                        "Bạn phải từ 15 tuổi trở lên.",
                        new[] { nameof(DateOfBirth) }
                    );
                }
            }
        }
    }
}
