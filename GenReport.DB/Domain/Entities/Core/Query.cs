using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
using GenReport.Domain.Entities.Onboarding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenReport.DB.Domain.Entities.Core
{
    [Table("queries")]
    public class Query : BaseEntity
    {
        /// <summary>
        /// The raw text of the query.
        /// </summary>
        [Column("rawtext")]
        public required string Rawtext { get; set; }

        /// <summary>
        /// The ID of the database associated with the query.
        /// </summary>
        [Column("database_id")]
        public long DatabaseId { get; set; }

        /// <summary>
        /// The database associated with the query.
        /// </summary>
        #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Database Database { get; set; }


        /// <summary>
        /// The ID of the user who created the query.
        /// </summary>
        [Column("created_by_id")]
        public long CreatedById { get; set; }

        /// <summary>
        /// The user who created the query.
        /// </summary>
        public User CreatedBy { get; set; }

        /// <summary>
        /// The ID of the collection associated with the query (optional).
        /// </summary>
        [Column("collection_id")]
        public long? CollectionId { get; set; }

        /// <summary>
        /// The selected columns for the query.
        /// </summary>
        [Column("involved_columns")]
        public string[] InvolvedColumns { get; set; } = [];

        /// <summary>
        /// The selected tables for the query.
        /// </summary>
        [Column("involved_tables")]
        public string[] InvolvedTables { get; set; } = [];

        /// <summary>
        /// The comments associated with the query.
        /// </summary>
        [Column("comments")]
        public string[] Comments { get; set; } = [];

        /// <summary>
        /// Gets or sets the created at.
        /// </summary>
        /// <value>
        /// The created at.
        /// </value>
    }
}