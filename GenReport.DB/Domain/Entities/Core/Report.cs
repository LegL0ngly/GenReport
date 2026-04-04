using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
using GenReport.Domain.Entities.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenReport.DB.Domain.Entities.Core
{
    /// <summary>
    /// The Report Entity
    /// </summary>
    /// <seealso cref="CoreDdd.Domain.Entity<System.Int64>" />
    /// <seealso cref="CoreDdd.Domain.Entity<System.Int64>" />
    /// <seealso cref="CoreDdd.Domain.IAggregateRoot" />
    [Table("reports")]
    public class Report : BaseEntity
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        [Column("name")]
        public required string Name { get; set; }
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>
        /// The description.
        /// </value>
        [Column("description")]
        public string? Description { get; set; }
        /// <summary>
        /// Gets or sets the query identifier.
        /// </summary>
        /// <value>
        /// The query identifier.
        /// </value>
        [Column("query_id")]
        public long QueryId { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        /// <summary>
        /// Gets or sets the query.
        /// </summary>
        /// <value>
        /// The query.
        /// </value>
        public Query Query { get; set; }

        /// <summary>
        /// Gets or sets the media file identifier.
        /// </summary>
        /// <value>
        /// The media file identifier.
        /// </value>
        [Column("mediafile_id")]
        public long MediaFileId  { get; set; }
        /// <summary>
        /// Gets or sets the media file.
        /// </summary>
        /// <value>
        /// The media file.
        /// </value>
        public MediaFile MediaFile { get; set; }
        /// <summary>
        /// Gets or sets the created at.
        /// </summary>
        /// <value>
        /// The created at.
        /// </value>
        /// Gets or sets the updated at.
        /// </summary>
        /// <value>
        /// The updated at.
        /// </value>
        /// Gets or sets the no of rows.
        /// </summary>
        /// <value>
        /// The no of rows.
        /// </value>
        [Column("no_of_rows")]
        public int NoOfRows { get; set; }
        /// <summary>
        /// Gets or sets the no of columns.
        /// </summary>
        /// <value>
        /// The no of columns.
        /// </value>
        [Column("no_of_columns")]
        public int NoOfColumns { get; set; }
        /// <summary>
        /// Gets or sets the time in seconds.
        /// </summary>
        /// <value>
        /// The time taken in seconds.
        /// </value>
        [Column("time_in_seconds")]
        public int TimeInSeconds { get; set; }
    }
}
