using Microsoft.EntityFrameworkCore;

namespace CredWise_Trail.Models
{
    public class BankLoanManagementDbContext : DbContext
    {
        public BankLoanManagementDbContext(DbContextOptions<BankLoanManagementDbContext> options)
            : base(options)
        { }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<LoanProduct> LoanProducts { get; set; }
        public DbSet<LoanApplication> LoanApplications { get; set; }
        public DbSet<Repayment> Repayments { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<KycApproval> KycApprovals { get; set; }
        public DbSet<LoanPayment> LoanPayments { get; set; }    

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<LoanApplication>()
     .HasOne(la => la.LoanProduct)           // A LoanApplication has one LoanProduct
     .WithMany()                             // A LoanProduct can have many LoanApplications (no navigation collection needed on LoanProduct if not used)
     .HasForeignKey(la => la.LoanProductId)  // The foreign key property in LoanApplication
     .IsRequired(false)                      // Make the foreign key optional (allows NULL)
     .OnDelete(DeleteBehavior.SetNull);      // Set the foreign key to NULL on deletion of the principal (LoanProduct)
            modelBuilder.Entity<LoanApplication>(entity =>
            {
                entity.ToTable(tb => tb.HasCheckConstraint("CK_LoanApplication_ApprovalStatus", "approvalStatus IN ('PENDING', 'APPROVED', 'REJECTED')"));
            });

            modelBuilder.Entity<Repayment>(entity =>
            {
                entity.ToTable(tb => tb.HasCheckConstraint("CK_Repayment_PaymentStatus", "paymentStatus IN ('PENDING', 'COMPLETED')"));
            });



            modelBuilder.Entity<Customer>().HasIndex(c => c.Email).IsUnique();
            modelBuilder.Entity<Admin>().HasIndex(a => a.Email).IsUnique();
        }
    }
}
