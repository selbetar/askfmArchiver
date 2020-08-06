using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace askfmArchiver.Models
{
    public class MyDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Answer> Answers { get; set; }
        public DbSet<PdfGen> PdfGen { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var con = new SqliteConnectionStringBuilder()
            {
                DataSource = @"data.db",
                RecursiveTriggers = true,
                ForeignKeys = true
            }.ToString();
            
            options.UseSqlite(con);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Answer>(entity =>
            {
                entity.HasKey(u => u.AnswerId)
                    .HasName("AnswerID");

                entity.Property(u => u.UserId)
                    .HasColumnName("UserID")
                    .IsRequired();
                
                entity.Property(u => u.AnswerText)
                    .HasColumnName("AnswerText");

                entity.Property(u => u.QuestionText)
                    .HasColumnName("QuestionText")
                    .IsRequired();
                
                entity.Property(u => u.AuthorId)
                    .HasColumnName("AuthorID");
                
                entity.Property(u => u.AuthorName)
                    .HasColumnName("AuthorName");
                
                entity.Property(u => u.Date)
                    .HasColumnName("Date")
                    .IsRequired();
                
                entity.Property(u => u.Likes)
                    .HasColumnName("Likes")
                    .IsRequired();
                
                entity.Property(u => u.VisualId)
                    .HasColumnName("VisualID");
                
                entity.Property(u => u.VisualType)
                    .HasColumnName("VisualType");
                
                entity.Property(u => u.VisualUrl)
                    .HasColumnName("VisualUrl");
                
                entity.Property(u => u.VisualExt)
                    .HasColumnName("VisualExt");
                
                entity.Property(u => u.VisualHash)
                    .HasColumnName("VisualHash");
                
                entity.Property(u => u.ThreadId)
                    .HasColumnName("ThreadID")
                    .IsRequired();
                
                entity.Property(u => u.PageId)
                    .HasColumnName("PageID");
                
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(u => u.UserId)
                    .HasConstraintName("UserID")
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.UserId)
                    .HasName("UserID");

                entity.Property(u => u.UserName)
                    .HasColumnName("UserName");
                
                entity.Property(u => u.LastQuestion)
                    .HasColumnName("LastQuestion");

                entity.Property(u => u.FirstQuestion)
                    .HasColumnName("FirstQuestion");
            });
            
            modelBuilder.Entity<PdfGen>(entity =>
            {
                entity.HasKey(u => u.UserId)
                    .HasName("UserID");

                entity.HasOne<User>()
                    .WithOne()
                    .HasForeignKey<PdfGen>(u => u.UserId)
                    .HasConstraintName("UserID");
                
                entity.Property(u => u.StopAt)
                    .HasColumnName("StopAt");
                
                entity.Property(u => u.AnswerId)
                    .HasColumnName("AnswerID");
            });
        }

    }
}