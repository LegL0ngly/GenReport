using GenReport.DB.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GenReport.DB.Domain.EntityConfigurations
{
    public class AiConnectionConfiguration : IEntityTypeConfiguration<AiConnection>
    {
        public void Configure(EntityTypeBuilder<AiConnection> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ApiKey)
                .IsRequired()
                .HasColumnType("text");

            builder.Property(x => x.CostPer1kInputTokens)
                .HasColumnType("decimal(18,8)");

            builder.Property(x => x.CostPer1kOutputTokens)
                .HasColumnType("decimal(18,8)");
        }
    }
}
