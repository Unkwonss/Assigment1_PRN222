using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BusinessLayer.Interfaces;
using BusinessLayer.DTOs;

namespace PresentationLayer.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IUserService _userService;

        public AdminController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> ManageStudents(string? searchTerm)
        {
            var allUsers = await _userService.GetAllUsersAsync();
            var students = allUsers.Where(u => u.Role == "Student");

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                students = students.Where(u =>
                    (u.FullName != null && u.FullName.ToLower().Contains(term)) ||
                    (u.Email != null && u.Email.ToLower().Contains(term)) ||
                    (u.Username != null && u.Username.ToLower().Contains(term))
                );
            }

            ViewBag.SearchTerm = searchTerm;
            return View(students);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudent(string fullName, string email)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Vui lòng điền đầy đủ họ tên và email.";
                return RedirectToAction("ManageStudents");
            }

            if (!email.Contains("@"))
            {
                TempData["Error"] = "Email không hợp lệ.";
                return RedirectToAction("ManageStudents");
            }

            try
            {
                // Check email exists in all users
                var existing = await _userService.GetUserByEmailAsync(email);
                if (existing != null)
                {
                    TempData["Error"] = $"Email '{email}' đã tồn tại trong hệ thống với vai trò '{existing.Role}'.";
                    return RedirectToAction("ManageStudents");
                }

                var student = await _userService.CreateStudentAccountAsync(fullName, email);
                if (student != null)
                {
                    TempData["Success"] = $"Đã tạo tài khoản cho sinh viên '{fullName}'. Username: '{student.Username}'. Thông tin tài khoản đã được gửi qua email.";
                }
                else
                {
                    TempData["Error"] = "Tạo tài khoản thất bại.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction("ManageStudents");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCsv(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn một file CSV hợp lệ.";
                return RedirectToAction("ManageStudents");
            }

            string ext = Path.GetExtension(csvFile.FileName).ToLower();
            if (ext != ".csv" && ext != ".txt")
            {
                TempData["Error"] = "Chỉ chấp nhận file định dạng .csv hoặc .txt chứa dữ liệu CSV.";
                return RedirectToAction("ManageStudents");
            }

            try
            {
                using (var stream = csvFile.OpenReadStream())
                {
                    int successCount = await _userService.ImportStudentsFromCsvAsync(stream);
                    if (successCount > 0)
                    {
                        TempData["Success"] = $"Import thành công {successCount} sinh viên và gửi email tài khoản mặc định!";
                    }
                    else
                    {
                        TempData["Error"] = "Không import được sinh viên nào. Vui lòng kiểm tra lại cấu trúc file CSV (Dòng đầu là header, các dòng sau dạng: Họ tên,Email).";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi import file CSV: {ex.Message}";
            }

            return RedirectToAction("ManageStudents");
        }
    }
}
