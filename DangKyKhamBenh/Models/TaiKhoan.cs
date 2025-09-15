using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models
{
    public class TaiKhoan
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }  // thêm để khớp View
        public string Email { get; set; }            // thêm
        public string PhoneNumber { get; set; }      // thêm
        public DateTime? DateOfBirth { get; set; }   // thêm
        public string Address { get; set; }          // thêm
        public string Role { get; set; }             // giữ nguyên
        public string StaffType { get; set; }        // giữ nguyên
    }


}