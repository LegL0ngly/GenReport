using GenReport.DB.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GenReport.DB.Domain.EntityConfigurations
{
    public class RoutineObjectConfiguration : IEntityTypeConfiguration<RoutineObject>
    {
        public void Configure(EntityTypeBuilder<RoutineObject> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(x => x.Type)
                .HasMaxLength(10)
                .IsRequired();

            builder.HasOne(x => x.Database)
                .WithMany()
                .HasForeignKey(x => x.DatabaseId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
