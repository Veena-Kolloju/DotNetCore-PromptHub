using CleanArchitectureDemo.Domain.Entities;
using CleanArchitectureDemo.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchitectureDemo.Infrastructure.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.OwnsOne(e => e.Email, email =>
        {
            email.Property(e => e.Value)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("Email");
        });
        
        builder.OwnsOne(e => e.Phone, phone =>
        {
            phone.Property(p => p.Value)
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnName("Phone");
        });
        
        builder.Property(e => e.Type)
            .HasConversion<int>()
            .IsRequired();
            
        builder.Property(e => e.Status)
            .HasConversion<int>()
            .IsRequired();
            
        builder.Property(e => e.CreatedAt)
            .IsRequired();
            
        builder.Property(e => e.CreatedBy)
            .HasMaxLength(100);
            
        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(100);
            
        builder.HasIndex(e => e.Email.Value)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
            
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}