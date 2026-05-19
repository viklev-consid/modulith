using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Persistence.Configurations;

internal sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.ToTable("legal_documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new LegalDocumentId(value));

        builder.Property(d => d.DocumentType)
            .HasConversion(t => t.ToString(), value => Enum.Parse<LegalDocumentType>(value))
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.Version)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.MarkdownContent).IsRequired();

        builder.Property(d => d.ContentHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(d => d.EffectiveAt).IsRequired();
        builder.Property(d => d.PublishedAt).IsRequired();
        builder.Property(d => d.SupersededAt);
        builder.Property(d => d.IsRequiredForOnboarding).IsRequired();
        builder.Property(d => d.IsRequiredForContinuedUse).IsRequired();
        builder.Property(d => d.ContinuedUseRequiredAt);
        builder.Property(d => d.BlockingLevel)
            .HasConversion(l => l.ToString(), value => Enum.Parse<LegalDocumentBlockingLevel>(value))
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(d => new { d.DocumentType, d.Version }).IsUnique();
        builder.HasIndex(d => new { d.DocumentType, d.IsRequiredForOnboarding, d.SupersededAt });
        builder.HasIndex(d => new { d.IsRequiredForContinuedUse, d.ContinuedUseRequiredAt, d.SupersededAt });
    }
}
