using GenReport.DB.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GenReport.DB.Domain.EntityConfigurations
{
    public class AiModelEndpointConfiguration : IEntityTypeConfiguration<AiModelEndpoint>
    {
        public void Configure(EntityTypeBuilder<AiModelEndpoint> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.AiConnection)
                .WithMany(c => c.ModelEndpoints)
                .HasForeignKey(x => x.AiConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
