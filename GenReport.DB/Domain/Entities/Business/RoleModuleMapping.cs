using CoreDdd.Domain;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenReport.DB.Domain.Entities.Business
{
    /// <summary>
    /// This class represents the mapping between a Role and a Module in the system.
    /// </summary>
    [Table("rolemodulemappings")] // Table name mapping
    public class RoleModuleMapping : Entity<long>, IAggregateRoot
    {
        #region Columns
        /// <summary>
        /// The unique identifier of the Role.
        /// </summary>
        [Column("role_id")] // Column name mapping
        public long RoleId { get; set; }

        /// <summary>
        /// The unique identifier of the Module.
        /// </summary>
        [Column("module_id")] // Column name mapping
        public long ModuleId { get; set; }

        /// <summary>
        /// The associated Module entity. (This might need to be a reference or a value object depending on your domain)
        /// </summary>
        public Module? Module { get; set; }

        /// <summary>
        /// Flag indicating if the mapping was created (consider using a DateTime instead).
        /// </summary>
        [Column("created_at")] // Column name mapping
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Flag indicating if the mapping was updated (consider using a DateTime instead).
        /// </summary>
        [Column("updated_at")] // Column name mapping
        public DateTime UpdatedAt { get; set; }
        #endregion
    }
}