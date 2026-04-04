using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenReport.DB.Domain.Entities.Core
{
    [Table("message_reports")]
    public class MessageReport : BaseEntity
    {
        [Column("message_id")]
        public long MessageId { get; set; }

#pragma warning disable CS8618
        public ChatMessage Message { get; set; }
#pragma warning restore CS8618

        [Column("report_id")]
        public long ReportId { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
        public Report Report { get; set; }
#pragma warning restore CS8618

    }
}
