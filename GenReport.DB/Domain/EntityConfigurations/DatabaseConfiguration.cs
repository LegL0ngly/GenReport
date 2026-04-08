using GenReport.DB.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenReport.DB.Domain.EntityConfigurations
{
    public class DatabaseConfiguration : IEntityTypeConfiguration<Database>
    {
        /// <summary>Configures the entity of type <span class="Database">TEntity</span>.</summary>
        /// <param name="builder">The builder to be used to configure the entity type.</param>
        public void Configure(EntityTypeBuilder<Database> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.MaxRowsToReturn).HasDefaultValue(20);
        }
    }
}
