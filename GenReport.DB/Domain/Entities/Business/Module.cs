using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenReport.DB.Domain.Entities.Business
{
    /// <summary>
    /// This class represents a module within the system.
    /// </summary>
    [Table("modules")] // Table name mapping
    public class Module : BaseEntity
    {
        /// <summary>
        /// The name of the module.
        /// </summary>
        [Column("name")] // Column name mapping
        [Required]  // Enforces non-null value
        public required string Name { get; set; }

        /// <summary>
        /// A description of the module's functionality.
        /// </summary>
        [Column("description")] // Column name mapping
        [Required]  // Enforces non-null value
        public required string Description { get; set; }

        /// <summary>
        /// The CSS class used to display an icon for the module. (Optional, defaults to "Default Icon Class")
        /// </summary>
        [Column("icon_class")] // Column name mapping
        public string IconClass { get; set; } = "Default Icon Class";

        /// <summary>
        /// The base URL path for the module.
        /// </summary>
        [Column("url_prefix")]
        public string UrlPrefix { get; set; } = string.Empty;

    }
}