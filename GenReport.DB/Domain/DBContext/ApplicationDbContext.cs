

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
        /// <summary>
        /// Gets or sets the queries.
        /// </summary>
        /// <value>
        /// The queries.
        /// </value>
        public DbSet<Query> Queries { get; set; }
        /// <summary>
        /// The Reports table represents all the reports generated 
        /// </summary>
        public DbSet<Report> Reports { get; set; }
        /// <summary>
        /// The Table for modules represents various modules available in this software
        /// </summary>
        public DbSet<Module> Modules { get; set; }
        /// <summary>
        /// The Table for rolemodules mapping represents various modules mappings available in this software
        /// </summary>
        public DbSet<RoleModuleMapping> RoleModules { get; set; }   
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
            modelBuilder.ApplyAllConfigurations(); 
            base.OnModelCreating(modelBuilder);
        }
    }
}
