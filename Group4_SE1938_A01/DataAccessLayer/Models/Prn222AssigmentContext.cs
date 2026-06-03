using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Domain.Models;

public partial class Prn222AssigmentContext : DbContext
{
    public Prn222AssigmentContext()
    {
    }

    public Prn222AssigmentContext(DbContextOptions<Prn222AssigmentContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Aimodel> Aimodels { get; set; }

    public virtual DbSet<BenchmarkResult> BenchmarkResults { get; set; }

    public virtual DbSet<Chapter> Chapters { get; set; }

    public virtual DbSet<ChatCitation> ChatCitations { get; set; }

    public virtual DbSet<ChatHistory> ChatHistories { get; set; }

    public virtual DbSet<ChatSession> ChatSessions { get; set; }

    public virtual DbSet<ChunkingStrategy> ChunkingStrategies { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<DocumentChunk> DocumentChunks { get; set; }

    public virtual DbSet<DocumentIndex> DocumentIndexes { get; set; }

    public virtual DbSet<EmbeddingModel> EmbeddingModels { get; set; }

    public virtual DbSet<Experiment> Experiments { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }

    public virtual DbSet<SubjectTeacher> SubjectTeachers { get; set; }

    public virtual DbSet<TestSet> TestSets { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Connection string is configured via DI in Program.cs from appsettings.json
        // Do NOT hardcode connection strings here
        if (!optionsBuilder.IsConfigured)
        {
            throw new InvalidOperationException(
                "DbContext must be configured via Dependency Injection. " +
                "Ensure AddDbContext is called in Program.cs with the connection string from appsettings.json.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Aimodel>(entity =>
        {
            entity.HasKey(e => e.ModelId).HasName("PK__AIModels__E8D7A12CCEA820F1");

            entity.ToTable("AIModels");

            entity.HasIndex(e => e.ModelName, "UQ__AIModels__67DC63B557FFC277").IsUnique();

            entity.Property(e => e.ModelName).HasMaxLength(100);
            entity.Property(e => e.ModelType).HasMaxLength(30);
        });

        modelBuilder.Entity<BenchmarkResult>(entity =>
        {
            entity.HasKey(e => e.ResultId).HasName("PK__Benchmar__976902086927374A");

            entity.HasIndex(e => e.ExperimentId, "IX_BenchmarkResults_ExperimentId");

            entity.HasIndex(e => e.QuestionId, "IX_BenchmarkResults_QuestionId");

            entity.Property(e => e.TestedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Experiment).WithMany(p => p.BenchmarkResults)
                .HasForeignKey(d => d.ExperimentId)
                .HasConstraintName("FK__Benchmark__Exper__7C4F7684");

            entity.HasOne(d => d.Question).WithMany(p => p.BenchmarkResults)
                .HasForeignKey(d => d.QuestionId)
                .HasConstraintName("FK__Benchmark__Quest__7B5B524B");
        });

        modelBuilder.Entity<Chapter>(entity =>
        {
            entity.HasKey(e => e.ChapterId).HasName("PK__Chapters__0893A36A66468F12");

            entity.Property(e => e.ChapterName).HasMaxLength(150);

            entity.HasOne(d => d.Subject).WithMany(p => p.Chapters)
                .HasForeignKey(d => d.SubjectId)
                .HasConstraintName("FK__Chapters__Subjec__403A8C7D");
        });

        modelBuilder.Entity<ChatCitation>(entity =>
        {
            entity.HasKey(e => e.CitationId).HasName("PK__ChatCita__EAD2ADFB28D54548");

            entity.HasIndex(e => e.HistoryId, "IX_ChatCitations_HistoryId");

            entity.Property(e => e.Snippet).HasMaxLength(500);

            entity.HasOne(d => d.Chunk).WithMany(p => p.ChatCitations)
                .HasForeignKey(d => d.ChunkId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChatCitat__Chunk__6E01572D");

            entity.HasOne(d => d.History).WithMany(p => p.ChatCitations)
                .HasForeignKey(d => d.HistoryId)
                .HasConstraintName("FK__ChatCitat__Histo__6D0D32F4");
        });

        modelBuilder.Entity<ChatHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__ChatHist__4D7B4ABD3F92A37D");

            entity.HasIndex(e => e.SessionId, "IX_ChatHistories_SessionId");

            entity.Property(e => e.Timestamp).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Session).WithMany(p => p.ChatHistories)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK__ChatHisto__Sessi__6A30C649");
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PK__ChatSess__C9F49290606518BF");

            entity.HasIndex(e => new { e.UserId, e.LastUpdatedAt }, "IX_ChatSessions_UserId_LastUpdated").IsDescending(false, true);

            entity.Property(e => e.SessionId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasDefaultValue("Cuộc trò chuyện mới");

            entity.HasOne(d => d.Subject).WithMany(p => p.ChatSessions)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChatSessi__Subje__66603565");

            entity.HasOne(d => d.User).WithMany(p => p.ChatSessions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChatSessi__UserI__656C112C");
        });

        modelBuilder.Entity<ChunkingStrategy>(entity =>
        {
            entity.HasKey(e => e.StrategyId).HasName("PK__Chunking__459B986C5A88EF21");

            entity.HasIndex(e => e.StrategyName, "UQ__Chunking__E7BA90D15C882147").IsUnique();

            entity.Property(e => e.StrategyName).HasMaxLength(100);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("PK__Document__1ABEEF0F53005A57");

            entity.HasIndex(e => e.ChapterId, "IX_Documents_ChapterId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.FileType).HasMaxLength(10);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Chapter).WithMany(p => p.Documents)
                .HasForeignKey(d => d.ChapterId)
                .HasConstraintName("FK__Documents__Chapt__4F7CD00D");

            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.Documents)
                .HasForeignKey(d => d.UploadedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Documents__Uploa__5070F446");
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(e => e.ChunkId).HasName("PK__Document__FBFF9D0012F19D2C");

            entity.HasIndex(e => e.IndexId, "IX_DocumentChunks_IndexId");

            entity.HasIndex(e => new { e.IndexId, e.ChunkOrder }, "UC_Index_ChunkOrder").IsUnique();

            entity.Property(e => e.HasEmbedding).HasDefaultValue(false);
            entity.Property(e => e.EmbeddingVector).HasColumnType("nvarchar(max)");
            entity.Property(e => e.VectorStoreKey).HasMaxLength(150);

            entity.HasOne(d => d.Index).WithMany(p => p.DocumentChunks)
                .HasForeignKey(d => d.IndexId)
                .HasConstraintName("FK__DocumentC__Index__5EBF139D");
        });

        modelBuilder.Entity<DocumentIndex>(entity =>
        {
            entity.HasKey(e => e.IndexId).HasName("PK__Document__40BC8A4147AD88D9");

            entity.HasIndex(e => e.DocumentId, "IX_DocumentIndexes_DocumentId");

            entity.HasIndex(e => new { e.DocumentId, e.ModelId, e.StrategyId, e.ChunkSize, e.ChunkOverlap }, "UC_Doc_Model_Strat_Size_Overlap").IsUnique();

            entity.Property(e => e.ChunkOverlap).HasDefaultValue(100);
            entity.Property(e => e.ChunkSize).HasDefaultValue(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Document).WithMany(p => p.DocumentIndices)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("FK__DocumentI__Docum__5812160E");

            entity.HasOne(d => d.Model).WithMany(p => p.DocumentIndices)
                .HasForeignKey(d => d.ModelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DocumentI__Model__59063A47");

            entity.HasOne(d => d.Strategy).WithMany(p => p.DocumentIndices)
                .HasForeignKey(d => d.StrategyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DocumentI__Strat__59FA5E80");
        });

        modelBuilder.Entity<EmbeddingModel>(entity =>
        {
            entity.HasKey(e => e.ModelId).HasName("PK__Embeddin__E8D7A12C85EB7DA4");

            entity.HasIndex(e => e.ModelName, "UQ__Embeddin__67DC63B57E076D19").IsUnique();

            entity.Property(e => e.ModelName).HasMaxLength(100);
            entity.Property(e => e.Provider).HasMaxLength(50);
        });

        modelBuilder.Entity<Experiment>(entity =>
        {
            entity.HasKey(e => e.ExperimentId).HasName("PK__Experime__7372232CA8244F24");

            entity.HasIndex(e => e.ExperimentName, "UQ__Experime__C12C9438FD53F0D9").IsUnique();

            entity.Property(e => e.AimodelId).HasColumnName("AIModelId");
            entity.Property(e => e.ExperimentDescription).HasMaxLength(500);
            entity.Property(e => e.ExperimentName).HasMaxLength(150);

            entity.HasOne(d => d.Aimodel).WithMany(p => p.Experiments)
                .HasForeignKey(d => d.AimodelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Experimen__AIMod__75A278F5");

            entity.HasOne(d => d.EmbeddingModel).WithMany(p => p.Experiments)
                .HasForeignKey(d => d.EmbeddingModelId)
                .HasConstraintName("FK__Experimen__Embed__76969D2E");

            entity.HasOne(d => d.Strategy).WithMany(p => p.Experiments)
                .HasForeignKey(d => d.StrategyId)
                .HasConstraintName("FK__Experimen__Strat__778AC167");
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.SubjectId).HasName("PK__Subjects__AC1BA3A8AF32ACA7");

            entity.HasIndex(e => e.SubjectCode, "UQ__Subjects__9F7CE1A9B0A9231D").IsUnique();

            entity.Property(e => e.SubjectCode).HasMaxLength(20);
            entity.Property(e => e.SubjectName).HasMaxLength(150);
        });

        modelBuilder.Entity<SubjectTeacher>(entity =>
        {
            entity.HasKey(e => new { e.SubjectId, e.UserId }).HasName("PK_SubjectTeachers");

            entity.ToTable("SubjectTeachers");

            entity.HasOne(d => d.Subject)
                .WithMany(p => p.SubjectTeachers)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SubjectTeachers_Subjects");

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SubjectTeachers_Users");
        });

        modelBuilder.Entity<TestSet>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("PK__TestSets__0DC06FAC4229D7D8");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Subject).WithMany(p => p.TestSets)
                .HasForeignKey(d => d.SubjectId)
                .HasConstraintName("FK__TestSets__Subjec__71D1E811");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CDB9FB20B");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E4E3098676").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Users__A9D105349D8CA7DF").IsUnique();

            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValue("Student");
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
