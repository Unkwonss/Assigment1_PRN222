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
        private readonly IConfiguration _configuration;

        public UserService(
            IGenericRepository<User> userRepository,
            IGenericRepository<ChatSession> chatSessionRepository,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _chatSessionRepository = chatSessionRepository;
            _configuration = configuration;
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
                // (ChatHistories sẽ bị xóa theo cascade từ DB)
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
