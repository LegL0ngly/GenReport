using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
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
    public class RoleModuleMapping : BaseEntity
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

        #endregion
    }
}