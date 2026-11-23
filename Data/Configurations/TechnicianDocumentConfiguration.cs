using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairConnect.Models;

namespace RepairConnect.Data.Configurations;

public class TechnicianDocumentConfiguration : IEntityTypeConfiguration<TechnicianDocument>
{
    public void Configure(EntityTypeBuilder<TechnicianDocument> builder)
    {
        builder.ToTable("TechnicianDocuments");

        builder.HasKey(td => td.Id);

        builder.Property(td => td.Document)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(td => td.UploadedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(td => td.Technician)
            .WithMany(tp => tp.Documents)
            .HasForeignKey(td => td.TechnicianUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
