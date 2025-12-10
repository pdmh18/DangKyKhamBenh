using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models
{
    public class Role
    {
        public int RoleID { get; set; }
        public string RoleName { get; set; }
        public List<Permission> Permissions { get; set; }
    }

    public class Permission
    {
        public int PermissionID { get; set; }
        public string PermissionName { get; set; }
    }

    public class UserRole
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public Role Role { get; set; } 
    }

}