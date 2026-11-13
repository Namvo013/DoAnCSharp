using LiMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Api.Data;

public sealed class LiMSDbContext(DbContextOptions<LiMSDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Reader> Readers => Set<Reader>();
    public DbSet<Librarian> Librarians => Set<Librarian>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookCopy> BookCopies => Set<BookCopy>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Fine> Fines => Set<Fine>();
    public DbSet<PurchaseProposal> PurchaseProposals => Set<PurchaseProposal>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("ACCOUNT", table => table.HasCheckConstraint(
                "CK_ACCOUNT_Role", "[Role] IN ('Borrower', 'Librarian', 'Admin')"));
            entity.HasKey(x => x.AccountId);
            entity.Property(x => x.Username).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(20);
            entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Reader>(entity =>
        {
            entity.ToTable("READER");
            entity.HasKey(x => x.ReaderId);
            entity.Property(x => x.CardNo).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CardStatus).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.AccountId).IsUnique();
            entity.HasIndex(x => x.CardNo).IsUnique();
            entity.HasOne(x => x.Account).WithOne(x => x.Reader)
                .HasForeignKey<Reader>(x => x.AccountId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Librarian>(entity =>
        {
            entity.ToTable("LIBRARIAN");
            entity.HasKey(x => x.LibrarianId);
            entity.Property(x => x.StaffCode).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.AccountId).IsUnique();
            entity.HasIndex(x => x.StaffCode).IsUnique();
            entity.HasOne(x => x.Account).WithOne(x => x.Librarian)
                .HasForeignKey<Librarian>(x => x.AccountId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("ACTIVITY_LOG");
            entity.HasKey(x => x.LogId);
            entity.Property(x => x.Action).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TargetTable).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(2000);
            entity.HasIndex(x => new { x.CreatedAt, x.AccountId });
            entity.HasOne(x => x.Account).WithMany(x => x.ActivityLogs)
                .HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.ToTable("SYSTEM_CONFIG");
            entity.HasKey(x => x.ConfigKey);
            entity.Property(x => x.ConfigKey).HasMaxLength(50);
            entity.Property(x => x.ConfigValue).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("CATEGORY");
            entity.HasKey(x => x.CategoryId);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.ToTable("BOOK");
            entity.HasKey(x => x.BookId);
            entity.Property(x => x.Title).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Author).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Isbn).HasMaxLength(20);
            entity.Property(x => x.Publisher).HasMaxLength(100);
            entity.Property(x => x.ShelfLocation).HasMaxLength(50);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.HasIndex(x => x.Isbn).IsUnique().HasFilter("[Isbn] IS NOT NULL");
            entity.HasIndex(x => new { x.Title, x.Author });
            entity.HasOne(x => x.Category).WithMany(x => x.Books)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<BookCopy>(entity =>
        {
            entity.ToTable("BOOK_COPY");
            entity.HasKey(x => x.CopyId);
            entity.Property(x => x.Barcode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Barcode).IsUnique();
            entity.HasIndex(x => new { x.BookId, x.Status });
            entity.HasOne(x => x.Book).WithMany(x => x.Copies)
                .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("RESERVATION");
            entity.HasKey(x => x.ReservationId);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.ReaderId, x.BookId, x.Status });
            entity.HasOne(x => x.Reader).WithMany(x => x.Reservations)
                .HasForeignKey(x => x.ReaderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Book).WithMany(x => x.Reservations)
                .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Loan>(entity =>
        {
            entity.ToTable("LOAN");
            entity.HasKey(x => x.LoanId);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.CopyId).IsUnique().HasFilter("[ReturnDate] IS NULL");
            entity.HasIndex(x => new { x.ReaderId, x.BorrowDate });
            entity.HasIndex(x => x.ReservationId).IsUnique()
                .HasFilter("[ReservationId] IS NOT NULL");
            entity.HasOne(x => x.Reader).WithMany(x => x.Loans)
                .HasForeignKey(x => x.ReaderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Copy).WithMany(x => x.Loans)
                .HasForeignKey(x => x.CopyId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Librarian).WithMany(x => x.Loans)
                .HasForeignKey(x => x.LibrarianId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Reservation).WithOne(x => x.Loan)
                .HasForeignKey<Loan>(x => x.ReservationId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Fine>(entity =>
        {
            entity.ToTable("FINE", table => table.HasCheckConstraint(
                "CK_FINE_Amount", "[Amount] >= 0"));
            entity.HasKey(x => x.FineId);
            entity.Property(x => x.Amount).HasPrecision(10, 2);
            entity.Property(x => x.Reason).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasOne(x => x.Loan).WithMany(x => x.Fines)
                .HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Collector).WithMany(x => x.CollectedFines)
                .HasForeignKey(x => x.CollectedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PurchaseProposal>(entity =>
        {
            entity.ToTable("PURCHASE_PROPOSAL");
            entity.HasKey(x => x.ProposalId);
            entity.Property(x => x.Title).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Author).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ReviewNote).HasMaxLength(1000);
            entity.HasOne(x => x.Reader).WithMany(x => x.PurchaseProposals)
                .HasForeignKey(x => x.ReaderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Reviewer).WithMany(x => x.ReviewedProposals)
                .HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.ToTable("EMAIL_LOG");
            entity.HasKey(x => x.EmailId);
            entity.Property(x => x.EmailType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SendStatus).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.ReaderId, x.SentAt });
            entity.HasOne(x => x.Reader).WithMany(x => x.EmailLogs)
                .HasForeignKey(x => x.ReaderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Loan).WithMany(x => x.EmailLogs)
                .HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Reservation).WithMany(x => x.EmailLogs)
                .HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
