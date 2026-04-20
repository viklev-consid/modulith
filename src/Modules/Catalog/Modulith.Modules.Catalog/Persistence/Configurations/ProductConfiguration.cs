using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Catalog.Domain;

namespace Modulith.Modules.Catalog.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, v => new ProductId(v));

        builder.Property(p => p.Sku)
            .HasConversion(s => s.Value, v => Sku.Create(v).Value)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(p => p.Sku).IsUnique();

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired();

        builder.OwnsOne(p => p.Price, price =>
        {
            price.Property(m => m.Amount)
                .HasColumnName("price_amount")
                .HasPrecision(18, 4)
                .IsRequired();

            price.Property(m => m.Currency)
                .HasColumnName("price_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(100);
        builder.Property(p => p.UpdatedAt);
        builder.Property(p => p.UpdatedBy).HasMaxLength(100);
    }
}
