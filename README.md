# VietRAG — RAG Chatbot for Vietnamese Education

> **PRN222 Assignment — Group 4 | SE1938**

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF_Core-9.0-orange)](https://docs.microsoft.com/ef/core/)
[![SQL Server](https://img.shields.io/badge/SQL_Server-2019+-CC2927?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![Gemini](https://img.shields.io/badge/Google_Gemini-2.5_Flash-4285F4?logo=google)](https://ai.google.dev/)

---

## 📖 Mô tả dự án

**VietRAG** là hệ thống chatbot hỏi đáp tài liệu môn học sử dụng **RAG (Retrieval-Augmented Generation)**, cho phép sinh viên đặt câu hỏi và nhận câu trả lời **chính xác, có trích dẫn nguồn** dựa trên tài liệu được giảng viên upload. Đồng thời, hệ thống cung cấp **bộ công cụ nghiên cứu và benchmark** để so sánh hiệu quả giữa các embedding model trong bối cảnh tiếng Việt.

### Vấn đề giải quyết
- Sinh viên khó tìm kiếm thông tin chính xác trong slide bài giảng
- Không có công cụ đo lường định lượng chất lượng RAG cho tiếng Việt
- Thiếu so sánh khách quan giữa các embedding model cho nội dung học thuật Việt Nam

---

## ✨ Tính năng chính

| Tính năng | Mô tả |
|-----------|-------|
| 📄 **Document Upload** | Upload PDF, DOCX, PPTX — tự động trích xuất text, chunk và embed |
| 💬 **RAG Chat** | Hỏi đáp AI với trích dẫn nguồn tài liệu, giới hạn trong phạm vi môn học |
| 🔬 **Embedding Benchmark** | So sánh Gemini / multilingual-e5-base / bge-m3 / PhoBERT qua Precision@3, Recall@3, MRR |
| 📊 **Dashboard** | Biểu đồ kết quả benchmark theo thời gian thực |
| 🗂️ **Lịch sử hội thoại** | Lưu session, rename, xóa hội thoại |
| 🔐 **Phân quyền** | Admin (quản lý tất cả) / Teacher / Student |
| ⚙️ **RAG Sandbox** | Tùy chỉnh model, chunking strategy, chunk size, overlap theo từng truy vấn |

---

## 🛠 Tech Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| **Frontend** | ASP.NET Core MVC, Bootstrap 5, jQuery, SweetAlert2 | Bootstrap 5.3 |
| **Backend** | C# ASP.NET Core MVC | .NET 9 |
| **Architecture** | N-Tier (3 Layers) + Repository Pattern + DI | — |
| **Database** | SQL Server + Entity Framework Core | EF Core 9 |
| **AI / LLM** | Google Gemini 2.5 Flash | v1beta |
| **Embedding** | Gemini Embedding 001, HuggingFace Inference API | — |
| **HF Models** | multilingual-e5-base (768d), bge-m3 (1024d), PhoBERT-base (768d) | — |
| **PDF Parsing** | PdfPig | 0.1.9 |
| **Auth** | Cookie Authentication + SHA-256 password hashing | — |

---

## 📐 Kiến trúc 3-Layer

```
┌─────────────────────────────────────────────────────┐
│               PresentationLayer (MVC)                │
│  Controllers │ Views (Razor) │ ViewModels │ wwwroot  │
└──────────────────────┬──────────────────────────────┘
                       │ injects IService interfaces
┌──────────────────────▼──────────────────────────────┐
│              BusinessLayer (Services)                │
│  ChatService │ DocumentService │ GeminiService       │
│  BenchmarkService │ UserService │ EmbeddingFactory   │
└──────────────────────┬──────────────────────────────┘
                       │ injects IGenericRepository<T>
┌──────────────────────▼──────────────────────────────┐
│           DataAccessLayer (EF Core + Repos)          │
│  GenericRepository<T> │ DbContext │ EF Migrations    │
└─────────────────────────────────────────────────────┘
```


### Nguyên tắc Decoupling nghiêm ngặt (Strict Separation)
Để đảm bảo tính độc lập giữa các tầng theo chuẩn Enterprise Architecture:
*   **PresentationLayer** hoàn toàn **không tham chiếu** trực tiếp tới **DataAccessLayer** hay các Database Entities (`Domain.Models` / `DataAccessLayer.Models`).
*   Toàn bộ dữ liệu trao đổi được ánh xạ qua các đối tượng **DTOs (Data Transfer Objects)** định nghĩa độc lập tại `BusinessLayer/DTOs/`.
*   Tất cả logic nghiệp vụ, xử lý lỗi và mapping đều được đóng gói hoàn toàn bên trong tầng **BusinessLayer**.

---

## 💡 Khái niệm Chunk & Index trong RAG

Để chatbot hoạt động chính xác dựa trên tài liệu học tập của bạn, hệ thống thực hiện hai bước xử lý cốt lõi:

1.  **Chunking (Chia nhỏ tài liệu):**
    *   Tài liệu thô (PDF, DOCX, PPTX) được đọc và chia nhỏ thành các phân đoạn ngắn (từ 200 - 1000 ký tự) dựa trên các chiến lược khác nhau (Fixed Size, Sentence, Recursive).
    *   Việc này giúp LLM (Gemini) dễ dàng trích xuất thông tin liên quan mà không bị vượt quá giới hạn từ ngữ (Context Window) và giúp việc tìm kiếm chính xác hơn.
2.  **Indexing (Chỉ mục hóa):**
    *   Mỗi **Chunk** văn bản được gửi đến mô hình Embedding để chuyển đổi thành các **Vector Embedding** (dạng mảng số biểu thị ngữ nghĩa).
    *   Hệ thống lưu các Vector này vào cơ sở dữ liệu cùng với nội dung văn bản.
    *   Khi người dùng đặt câu hỏi, hệ thống sẽ chuyển câu hỏi thành Vector và dùng thuật toán **Cosine Similarity** để tìm ra các Chunk phù hợp nhất trong nháy mắt.

---

## ⚙️ Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [SQL Server 2019+](https://www.microsoft.com/sql-server) (hoặc SQL Server Express)
- Visual Studio 2022 / VS Code / Cursor
- Google Gemini API Key — **miễn phí** tại [ai.google.dev](https://ai.google.dev)
- HuggingFace Token — **miễn phí** (tùy chọn, cho HF models) tại [huggingface.co/settings/tokens](https://huggingface.co/settings/tokens)

---

## 🚀 Cài đặt và chạy

### Bước 1 — Clone project

```bash
git clone https://github.com/YOUR_USERNAME/VietRAG.git
cd VietRAG/Group4_SE1938_A01
```

### Bước 2 — Tạo database

Mở SQL Server Management Studio, chạy lệnh tạo DB:

```sql
CREATE DATABASE Prn222_assigment;
```

Sau đó apply migrations:

```bash
cd PresentationLayer
dotnet ef database update --project ../DataAccessLayer
```

### Bước 3 — Cấu hình API Keys

Mở `PresentationLayer/appsettings.json` và điền:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=Prn222_assigment;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True"
  },
  "GeminiSettings": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-2.5-flash"
  },
  "HuggingFaceToken": "hf_YOUR_TOKEN"
}
```

### Bước 4 — Chạy project

```bash
cd PresentationLayer
dotnet run
```

Mở trình duyệt: **http://localhost:5007**

### Bước 5 — Tài khoản mặc định

| Role | Email | Mật khẩu |
|------|-------|---------|
| **Admin** | `admin@fpt.edu.vn` | `123456789` |

> Admin được tự động tạo khi app khởi động lần đầu.

---

## 📁 Cấu trúc thư mục

```
Group4_SE1938_A01/
├── PresentationLayer/              # Tầng hiển thị (MVC)
│   ├── Controllers/
│   │   ├── AccountController.cs   # Login, Register, User CRUD
│   │   ├── ChatController.cs      # RAG Chat sessions
│   │   ├── DocumentController.cs  # Upload, Index, Re-Index
│   │   └── BenchmarkController.cs # Benchmark Runner & Dashboard
│   ├── Views/
│   │   ├── Chat/                  # Chat UI
│   │   ├── Document/              # Document management UI
│   │   └── Benchmark/             # Dashboard & metrics
│   ├── Models/                    # ViewModels (LoginVM, RegisterVM...)
│   └── wwwroot/                   # CSS, JS, static files
│
├── BusinessLayer/                 # Tầng nghiệp vụ
│   ├── Interfaces/                # Service contracts (IUserService, IChatService...)
│   ├── Services/
│   │   ├── ChatService.cs         # RAG retrieval pipeline
│   │   ├── DocumentService.cs     # Chunking + Embedding indexing
│   │   ├── GeminiService.cs       # LLM generation
│   │   ├── BenchmarkService.cs    # RAGAS metrics computation
│   │   ├── Embedding/
│   │   │   ├── EmbeddingProviderFactory.cs
│   │   │   ├── HuggingFaceEmbeddingProvider.cs
│   │   │   ├── GeminiEmbeddingAdapter.cs
│   │   │   └── OpenAIEmbeddingProvider.cs
│   │   └── Chunking/
│   │       ├── FixedSizeChunker.cs
│   │       ├── SentenceChunker.cs
│   │       └── RecursiveChunker.cs
│   └── Helpers/
│       └── VectorHelper.cs        # Cosine similarity
│
└── DataAccessLayer/               # Tầng dữ liệu
    ├── Models/                    # EF Core entities + DbContext
    ├── Repository/
    │   ├── IGenericRepository.cs
    │   └── GenericRepository.cs
    ├── Seeds/
    │   └── TestSet50Questions.json # 50 câu hỏi PRN212 benchmark
    └── Migrations/                # EF Core migrations
```

---

## 🔬 Chạy Benchmark

1. **Login** với tài khoản Admin
2. Vào tab **Benchmarks** trên navbar
3. Chọn **môn học** và **embedding model** cần đánh giá
4. Bấm **"Run Benchmark"** — hệ thống chạy pipeline RAG trên 50 câu hỏi mẫu
5. Xem **Dashboard** với biểu đồ Precision@3, Recall@3, MRR, Latency

### Metrics được tính

| Metric | Ý nghĩa |
|--------|---------|
| **Precision@3** | Trong top-3 chunks trả về, bao nhiêu % thực sự liên quan |
| **Recall@3** | Tìm được bao nhiêu % ground truth chunks |
| **MRR** | Mean Reciprocal Rank — chunk đúng xuất hiện ở vị trí nào |
| **Latency (ms)** | Thời gian embedding + retrieval |

---

## 📊 Kết quả Benchmark (PRN212 — OOP với .NET)

| Model | Dim | Precision@3 | Recall@3 | MRR | Latency |
|-------|-----|------------|----------|-----|---------|
| gemini-embedding-001 | 768 | — | — | — | — |
| multilingual-e5-base | 768 | — | — | — | — |
| bge-m3 | 1024 | — | — | — | — |
| PhoBERT-base | 768 | — | — | — | — |

> *Cập nhật sau khi chạy benchmark thực tế. Lưu ý: bge-m3 (1024d) không tương thích vector với Gemini (768d) — cần Re-Index riêng.*

---

## 📌 Ghi chú kỹ thuật quan trọng

### Dimension Mismatch
Mỗi embedding model tạo vector với số chiều khác nhau. Khi chat, query vector phải cùng chiều với chunk vectors trong DB. Nếu chọn model khác với model đã index → hệ thống tự động báo **"Vui lòng Re-Index với model đang chọn"**.

### RAG Pipeline Flow
```
User Question
    → Embed query (EmbeddingProviderFactory)
    → Cosine Similarity với chunks trong DB
    → Top-K retrieval (threshold 0.4)
    → Gemini 2.5 Flash (context + question)
    → Response với Citations
```

### HuggingFace Network
HuggingFace Inference API (`api-inference.huggingface.co`) cần kết nối internet trực tiếp. Nếu mạng trường/công ty block → cần VPN hoặc hotspot.

---

## 👥 Thành viên nhóm

| Họ tên | MSSV | Role |
|--------|------|------|
| Phạm Trí Tính | SE1938_xxx | Team Lead / Developer |

---

## 📄 License

MIT License — Free for educational use.
