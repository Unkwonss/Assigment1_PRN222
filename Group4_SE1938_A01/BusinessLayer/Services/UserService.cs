using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Domain.Models;
using DataAccessLayer.Repository;
using BusinessLayer.Interfaces;
using BusinessLayer.DTOs;

namespace BusinessLayer.Services
{
    public class UserService : IUserService
    {
        private readonly IGenericRepository<User> _userRepository;
        private readonly IGenericRepository<ChatSession> _chatSessionRepository;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;
        private string DefaultPassword => _configuration["DefaultStudentPassword"] ?? "fpt12345";

        public UserService(
            IGenericRepository<User> userRepository,
            IGenericRepository<ChatSession> chatSessionRepository,
            EmailService emailService,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _chatSessionRepository = chatSessionRepository;
            _emailService = emailService;
            _configuration = configuration;
        }

        private string GenerateUsername(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "student_" + Guid.NewGuid().ToString().Substring(0, 5);
            
            // Remove Vietnamese accents and spaces
            string normalized = fullName.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            foreach (char c in normalized)
            {
                System.Globalization.UnicodeCategory uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        sb.Append(char.ToLower(c));
                    }
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        public async Task<UserDto?> CreateStudentAccountAsync(string fullName, string email)
        {
            // Validate if user already exists
            var existing = await _userRepository.GetAllAsync(u => u.Email == email);
            if (existing.Any()) return MapToDto(existing.First());

            string username = GenerateUsername(fullName);
            // Check if username unique
            var existingUsername = await _userRepository.GetAllAsync(u => u.Username == username);
            if (existingUsername.Any())
            {
                username += new Random().Next(10, 99);
            }

            var user = new User
            {
                Username = username,
                PasswordHash = HashPassword(DefaultPassword),
                FullName = fullName,
                Email = email,
                Role = "Student"
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveAsync();

            // Send notification email
            string subject = "Tài khoản học tập RAG LMS FPT của bạn";
            string body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; border: 1px solid #e2e8f0; padding: 20px; border-radius: 8px; background-color: #ffffff;'>
                    <h2 style='color: #4f46e5; border-bottom: 2px solid #f1f5f9; padding-bottom: 10px;'>Chào mừng {fullName} đến với hệ thống RAG LMS!</h2>
                    <p style='color: #334155; font-size: 16px;'>Tài khoản sinh viên của bạn đã được khởi tạo thành công:</p>
                    <div style='background-color: #f8fafc; padding: 15px; border-radius: 6px; margin: 20px 0;'>
                        <p style='margin: 8px 0; color: #475569;'><b>Tên tài khoản (Username):</b> {username}</p>
                        <p style='margin: 8px 0; color: #475569;'><b>Email đăng nhập:</b> {email}</p>
                        <p style='margin: 8px 0; color: #e11d48;'><b>Mật khẩu mặc định:</b> {DefaultPassword}</p>
                    </div>
                    <p style='color: #ef4444; font-weight: bold;'>* Lưu ý quan trọng: Bạn bắt buộc phải đổi mật khẩu mặc định ngay ở lần đăng nhập đầu tiên tại trang cá nhân (Profile) để kích hoạt toàn bộ tính năng.</p>
                    <p style='color: #94a3b8; font-size: 12px; margin-top: 30px; border-top: 1px solid #f1f5f9; padding-top: 15px;'>Thư này được gửi tự động bởi hệ thống Quản lý học liệu PRN222 FPT.</p>
                </div>";

            await _emailService.SendEmailAsync(email, subject, body);

            return MapToDto(user);
        }

        public async Task<int> ImportStudentsFromCsvAsync(System.IO.Stream fileStream)
        {
            int successCount = 0;
            using (var reader = new System.IO.StreamReader(fileStream, Encoding.UTF8))
            {
                string? line;
                // Read header row
                await reader.ReadLineAsync();

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length >= 2)
                    {
                        string fullName = parts[0].Trim();
                        string email = parts[1].Trim();

                        if (!string.IsNullOrEmpty(fullName) && !string.IsNullOrEmpty(email) && email.Contains("@"))
                        {
                            var created = await CreateStudentAccountAsync(fullName, email);
                            if (created != null)
                            {
                                successCount++;
                            }
                        }
                    }
                }
            }
            return successCount;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            if (userId == 0) return false; // Admin config is read-only from appsettings.json
            
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            if (user.PasswordHash != HashPassword(oldPassword))
            {
                return false; // Old password mismatch
            }

            user.PasswordHash = HashPassword(newPassword);
            _userRepository.Update(user);
            await _userRepository.SaveAsync();
            return true;
        }

        public async Task<bool> IsDefaultPasswordAsync(int userId)
        {
            if (userId == 0) return false;
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            return user.PasswordHash == HashPassword(DefaultPassword);
        }

        private UserDto? MapToDto(User? user)
        {
            if (user == null) return null;
            return new UserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                PasswordHash = user.PasswordHash,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role
            };
        }

        private User? MapToEntity(UserDto? dto)
        {
            if (dto == null) return null;
            return new User
            {
                UserId = dto.UserId,
                Username = dto.Username,
                PasswordHash = dto.PasswordHash,
                FullName = dto.FullName,
                Email = dto.Email,
                Role = dto.Role
            };
        }

        public async Task<UserDto?> AuthenticateAsync(string email, string password)
        {
            // Check Admin credentials from appsettings.json
            var adminEmail = _configuration["AdminAccount:Email"] ?? "admin@fpt.edu.vn";
            var adminPassword = _configuration["AdminAccount:Password"] ?? "123456789";

            if (email.Equals(adminEmail, StringComparison.OrdinalIgnoreCase) && HashPassword(password) == HashPassword(adminPassword))
            {
                // Try to get the real admin user from DB (seeded at startup)
                var dbAdmin = (await _userRepository.GetAllAsync(u => u.Email == adminEmail)).FirstOrDefault();
                if (dbAdmin != null)
                {
                    return MapToDto(dbAdmin);
                }

                // Fallback: virtual admin if not in DB
                return new UserDto
                {
                    UserId = 0,
                    Username = "admin",
                    PasswordHash = HashPassword(adminPassword),
                    FullName = "System Administrator",
                    Email = adminEmail,
                    Role = "Admin"
                };
            }

            // Normal authentication from database
            var hashedPassword = HashPassword(password);
            var users = await _userRepository.GetAllAsync(u => u.Email == email && u.PasswordHash == hashedPassword);
            return MapToDto(users.FirstOrDefault());
        }

        public async Task<UserDto?> RegisterAsync(string username, string password, string fullName, string email, string role = "Student")
        {
            var existingUsers = await _userRepository.GetAllAsync(u => u.Username == username || u.Email == email);
            if (existingUsers.Any()) return null;

            var user = new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                FullName = fullName,
                Email = email,
                Role = role
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveAsync();
            return MapToDto(user);
        }

        public async Task<UserDto?> GetUserByIdAsync(int userId)
        {
            if (userId == 0)
            {
                var adminEmail = _configuration["AdminAccount:Email"] ?? "admin@fpt.edu.vn";
                return new UserDto
                {
                    UserId = 0,
                    Username = "admin",
                    PasswordHash = "@@abc123@@",
                    FullName = "System Administrator",
                    Email = adminEmail,
                    Role = "Admin"
                };
            }
            var user = await _userRepository.GetByIdAsync(userId);
            return MapToDto(user);
        }

        public async Task<UserDto?> GetUserByEmailAsync(string email)
        {
            var adminEmail = _configuration["AdminAccount:Email"] ?? "admin@fpt.edu.vn";
            if (email.Equals(adminEmail, StringComparison.OrdinalIgnoreCase))
            {
                return new UserDto
                {
                    UserId = 0,
                    Username = "admin",
                    PasswordHash = "@@abc123@@",
                    FullName = "System Administrator",
                    Email = adminEmail,
                    Role = "Admin"
                };
            }

            var users = await _userRepository.GetAllAsync(u => u.Email == email);
            return MapToDto(users.FirstOrDefault());
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllAsync();
            return users.Select(u => MapToDto(u)!).ToList();
        }

        public async Task<UserDto> CreateUserAsync(UserDto userDto)
        {
            var user = MapToEntity(userDto)!;
            user.PasswordHash = HashPassword(user.PasswordHash);
            await _userRepository.AddAsync(user);
            await _userRepository.SaveAsync();
            return MapToDto(user)!;
        }

        public async Task UpdateUserAsync(UserDto userDto)
        {
            var existingUser = await _userRepository.GetByIdAsync(userDto.UserId);
            if (existingUser != null)
            {
                existingUser.FullName = userDto.FullName;
                existingUser.Email = userDto.Email;
                existingUser.Role = userDto.Role;
                existingUser.Username = userDto.Username;

                if (!string.IsNullOrEmpty(userDto.PasswordHash) && userDto.PasswordHash != existingUser.PasswordHash)
                {
                    if (userDto.PasswordHash.Length != 64)
                    {
                        existingUser.PasswordHash = HashPassword(userDto.PasswordHash);
                    }
                    else
                    {
                        existingUser.PasswordHash = userDto.PasswordHash;
                    }
                }
                
                _userRepository.Update(existingUser);
                await _userRepository.SaveAsync();
            }
        }

        public async Task DeleteUserAsync(int userId)
        {
            if (userId != 0)
            {
                // Xóa các ChatSession liên quan trước để tránh FK constraint violation
                var sessions = await _chatSessionRepository.GetAllAsync(s => s.UserId == userId);
                foreach (var session in sessions)
                {
                    _chatSessionRepository.Delete(session);
                }
                await _chatSessionRepository.SaveAsync();

                // Sau đó xóa User
                await _userRepository.DeleteByIdAsync(userId);
                await _userRepository.SaveAsync();
            }
        }

        private string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
