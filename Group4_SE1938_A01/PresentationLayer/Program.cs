using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Domain.Models;
using BusinessLayer.Interfaces;
using BusinessLayer.Services;
using BusinessLayer.Services.Embedding;
using Microsoft.Extensions.DependencyInjection;
using BusinessLayer.Models;

namespace PresentationLayer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddDbContext<Prn222AssigmentContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Bind GeminiSettings từ config
            builder.Services.Configure<GeminiSettings>(
                builder.Configuration.GetSection("GeminiSettings"));

            // Đăng ký HttpClient cho Gemini
            builder.Services.AddHttpClient("GeminiClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Đăng ký HttpClient cho HuggingFace Inference API
            builder.Services.AddHttpClient("HuggingFaceClient", client =>
            {
                client.BaseAddress = new Uri("https://api-inference.huggingface.co");
                client.Timeout = TimeSpan.FromSeconds(60); // HF cold-start có thể chậm
            });

            // Đăng ký HttpClient cho OpenAI
            builder.Services.AddHttpClient("OpenAIClient", client =>
            {
                client.BaseAddress = new Uri("https://api.openai.com");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Register Repositories
            builder.Services.AddScoped(typeof(DataAccessLayer.Repository.IGenericRepository<>), typeof(DataAccessLayer.Repository.GenericRepository<>));

            // Register Services
            builder.Services.AddSingleton<SimulatedAIEngine>();
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IGeminiService, GeminiService>();
            builder.Services.AddScoped<IGeminiEmbeddingService, GeminiEmbeddingService>();
            builder.Services.AddScoped<IBenchmarkService, BenchmarkService>();
            // Factory chọn đúng embedding provider theo model name
            builder.Services.AddScoped<EmbeddingProviderFactory>();

            // Configure Authentication
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                });

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<Prn222AssigmentContext>();
                var canConnect = await db.Database.CanConnectAsync();
                if (!canConnect)
                    Console.WriteLine("[DB ERROR] Cannot connect to database!");
                else
                {
                    Console.WriteLine("[DB OK] Database connected successfully.");

                    // Ensure admin user exists in DB for FK constraints
                    var adminEmail = builder.Configuration["AdminAccount:Email"] ?? "admin@fpt.edu.vn";
                    var adminPassword = builder.Configuration["AdminAccount:Password"] ?? "123456789";
                    var existingAdmin = db.Users.FirstOrDefault(u => u.Email == adminEmail);
                    if (existingAdmin == null)
                    {
                        using var sha256 = System.Security.Cryptography.SHA256.Create();
                        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(adminPassword));
                        var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                        var adminUser = new User
                        {
                            Username = "admin",
                            PasswordHash = hashString,
                            FullName = "System Administrator",
                            Email = adminEmail,
                            Role = "Admin"
                        };
                        db.Users.Add(adminUser);
                        await db.SaveChangesAsync();
                        Console.WriteLine($"[SEED] Admin user created with UserId={adminUser.UserId}");
                    }
                    else
                    {
                        Console.WriteLine($"[SEED] Admin user already exists with UserId={existingAdmin.UserId}");
                    }

                    // Seed TestSet 50 questions if empty or very few exist
                    if (db.TestSets.Count() < 50)
                    {
                        var subject = db.Subjects.FirstOrDefault(s => s.SubjectCode == "PRN222") 
                                   ?? db.Subjects.FirstOrDefault();
                        
                        if (subject != null)
                        {
                            var jsonPath = Path.Combine(builder.Environment.ContentRootPath, "Data", "TestSets_SWP391.json");
                            if (File.Exists(jsonPath))
                            {
                                var jsonString = File.ReadAllText(jsonPath);
                                var testSetsData = System.Text.Json.JsonSerializer.Deserialize<List<TestSet>>(jsonString);
                                if (testSetsData != null)
                                {
                                    foreach (var ts in testSetsData)
                                    {
                                        ts.SubjectId = subject.SubjectId;
                                        ts.CreatedAt = DateTime.UtcNow;
                                        db.TestSets.Add(ts);
                                    }
                                    await db.SaveChangesAsync();
                                    Console.WriteLine($"[SEED] Added {testSetsData.Count} Test Sets for subject {subject.SubjectCode}.");
                                }
                            }
                        }
                    }

                    // Seed PRN212 (OOP with .NET) — 50 câu hỏi benchmark
                    var prn212Subject = db.Subjects.FirstOrDefault(s => s.SubjectCode == "PRN212");
                    if (prn212Subject != null)
                    {
                        var prn212Count = db.TestSets.Count(t => t.SubjectId == prn212Subject.SubjectId);
                        if (prn212Count < 50)
                        {
                            var prn212JsonPath = Path.Combine(builder.Environment.ContentRootPath, "Data", "TestSets_PRN212.json");
                            if (File.Exists(prn212JsonPath))
                            {
                                var jsonString = File.ReadAllText(prn212JsonPath);
                                // Deserialize as anonymous type (Question + GroundTruth only)
                                var rawItems = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(jsonString);
                                if (rawItems != null)
                                {
                                    int added = 0;
                                    foreach (var item in rawItems)
                                    {
                                        var question = item.GetProperty("Question").GetString() ?? "";
                                        var groundTruth = item.GetProperty("GroundTruth").GetString() ?? "";
                                        if (!string.IsNullOrWhiteSpace(question))
                                        {
                                            db.TestSets.Add(new TestSet
                                            {
                                                SubjectId  = prn212Subject.SubjectId,
                                                Question   = question,
                                                GroundTruth = groundTruth,
                                                CreatedAt  = DateTime.UtcNow
                                            });
                                            added++;
                                        }
                                    }
                                    await db.SaveChangesAsync();
                                    Console.WriteLine($"[SEED] Added {added} PRN212 Test Questions (OOP with .NET).");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[SEED] PRN212 JSON not found at: {prn212JsonPath}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[SEED] PRN212 already has {prn212Count} test questions — skip.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[SEED] PRN212 subject not found in DB — skipping PRN212 test set seed.");
                    }
                }
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
