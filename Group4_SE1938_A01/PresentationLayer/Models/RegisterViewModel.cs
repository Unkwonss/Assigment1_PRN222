using System.ComponentModel.DataAnnotations;

namespace PresentationLayer.Models
{
    /// <summary>
    /// ViewModel for Register form with full DataAnnotations validation
    /// </summary>
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Họ và tên là bắt buộc")]
        [Display(Name = "Họ và tên")]
        [MinLength(2, ErrorMessage = "Họ và tên phải có ít nhất 2 ký tự")]
        [MaxLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
        [Display(Name = "Tên đăng nhập")]
        [MinLength(3, ErrorMessage = "Tên đăng nhập phải có ít nhất 3 ký tự")]
        [MaxLength(50, ErrorMessage = "Tên đăng nhập không được vượt quá 50 ký tự")]
        [RegularExpression(@"^[a-zA-Z0-9_\.]+$", ErrorMessage = "Tên đăng nhập chỉ được chứa chữ cái, số, dấu _ và dấu .")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Địa chỉ Email không hợp lệ")]
        [Display(Name = "Địa chỉ Email")]
        [MaxLength(100, ErrorMessage = "Email không được vượt quá 100 ký tự")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        [MaxLength(100, ErrorMessage = "Mật khẩu không được vượt quá 100 ký tự")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc")]
        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
