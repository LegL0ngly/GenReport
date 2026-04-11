

using GenReport.DB.Domain.Common;
using GenReport.DB.Domain.Interfaces;
using GenReport.Domain.Entities.Media;
using GenReport.Domain.Entities.Onboarding;
using Microsoft.EntityFrameworkCore;
using GenReport.Domain.EntityConfigurations;
using GenReport.DB.Domain.EntityConfigurations;
using GenReport.DB.Domain.Entities.Core;
using GenReport.Infrastructure.Static.Externsions;
using GenReport.DB.Domain.Entities.Business;

namespace GenReport.Domain.DBContext
{
    /// <summary>
    /// the database that runs on the client server 
    /// </summary>
    /// <seealso cref="Microsoft.EntityFrameworkCore.DbContext" />
    /// <seealso cref="GenReport.DB.Domain.Interfaces.IApplicationDbContext" />
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options), IApplicationDbContext
    {
        #region Users
        /// <summary>
        /// Gets or sets the users.
        /// </summary>
        /// <value>
        /// The users.
        /// </value>
        public DbSet<User> Users { get; set; }
        #endregion Users

        #region Business
        #endregion Business

        #region Media
        /// <summary>
        /// Gets or sets the media files.
        /// </summary>
        /// <value>
        /// The media files.
        /// </value>
        public DbSet<MediaFile> MediaFiles { get; set; }
        #endregion Media
        #region Core
        /// <summary>
        /// Gets or sets the databases.
        /// </summary>
        /// <value>
        /// The databases.
        /// </value>
        public DbSet<Database> Databases { get; set; }
        /// <summary>Gets or sets the queries.</summary>
        public DbSet<Query> Queries { get; set; }
        /// <summary>The Reports table represents all the reports generated.</summary>
        public DbSet<Report> Reports { get; set; }
        /// <summary>Module access control table.</summary>
        public DbSet<Module> Modules { get; set; }
        /// <summary>Role-to-module mapping table.</summary>
        public DbSet<RoleModuleMapping> RoleModules { get; set; }
        /// <summary>AI/LLM provider connection configurations.</summary>
        public DbSet<AiConnection> AiConnections { get; set; }
        /// <summary>Chat sessions.</summary>
        public DbSet<ChatSession> ChatSessions { get; set; }
        /// <summary>Chat messages within sessions.</summary>
        public DbSet<ChatMessage> ChatMessages { get; set; }
        /// <summary>Mappings between a chat message and a generated report.</summary>
        public DbSet<MessageReport> MessageReports { get; set; }
        /// <summary>Mappings between a chat message and a media file.</summary>
        public DbSet<MessageAttachment> MessageAttachments { get; set; }
        /// <summary>Database schema tables and views along with embeddings.</summary>
        public DbSet<SchemaObject> SchemaObjects { get; set; }
        /// <summary>Database routines like SPs and functions along with embeddings.</summary>
        public DbSet<RoutineObject> RoutineObjects { get; set; }
        /// <summary>Versioned AI configurations (system prompts, instructions, etc.).</summary>
        public DbSet<AiConfig> AiConfigs { get; set; }
        #endregion

        /// <summary>
        /// Override this method to further configure the model that was discovered by convention from the entity types
        /// exposed in <see cref="T:Microsoft.EntityFrameworkCore.DbSet`1" /> properties on your derived context. The resulting model may be cached
        /// and re-used for subsequent instances of your derived context.
        /// </summary>
        /// <param name="modelBuilder">The builder being used to construct the model for this context. Databases (and other extensions) typically
        /// define extension methods on this object that allow you to configure aspects of the model that are specific
        /// to a given database.</param>
        /// <remarks>
        /// <para>
        /// If a model is explicitly set on the options for this context (via <see cref="M:Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.UseModel(Microsoft.EntityFrameworkCore.Metadata.IModel)" />)
        /// then this method will not be run. However, it will still run when creating a compiled model.
        /// </para>
        /// <para>
        /// See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see> for more information and
        /// examples.
        /// </para>
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var providerName = Database.ProviderName ?? string.Empty;
            var isNpgsql = providerName.Contains("Npgsql");

            if (isNpgsql)
            {
                modelBuilder.HasPostgresExtension("vector");
            }

            modelBuilder.ApplyAllConfigurations();

            // When not running on Npgsql (e.g. in-memory provider used in tests) the
            // Pgvector Vector type has no parameterless constructor and cannot be bound
            // by EF conventions.  Ignore SchemaObject and RoutineObject *after* the
            // configurations have been applied so the Ignore wins and removes them from
            // the model entirely.
            if (!isNpgsql)
            {
                modelBuilder.Ignore<SchemaObject>();
                modelBuilder.Ignore<RoutineObject>();
            }

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    modelBuilder.Entity(entityType.ClrType)
                        .Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");
                }
            }
            base.OnModelCreating(modelBuilder);
        }
    }
}
