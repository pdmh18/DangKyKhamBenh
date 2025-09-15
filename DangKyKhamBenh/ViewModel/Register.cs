using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.ViewModel
{
    public class Register
    {
        [Required(ErrorMessage = "Username cannot be blank.")]
        public string Username { get; set; }
        [Required(ErrorMessage = "Password cannot be blank.")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Confirm Password cannot be blank.")]
        [Compare("Password", ErrorMessage ="Password and Confirm Password")]
        public string ConfirmPassword { get; set; }
        [Required(ErrorMessage = "Email cannot be blank.")]
        [EmailAddress(ErrorMessage ="Invalid Email")]
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Address { get; set; }
        public string StaffType { get; set; }


    }
}