using System.ComponentModel.DataAnnotations;

namespace PresentationLayer.Models
{
    /// <summary>
    /// ViewModel for Login form with full DataAnnotations validation
    /// </summary>
    public class LoginViewModel
    {
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
    }
}
