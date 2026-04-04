using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
using GenReport.Domain.Entities.Media;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenReport.DB.Domain.Entities.Core
{
    [Table("message_attachments")]
    public class MessageAttachment : BaseEntity
    {
        [Column("message_id")]
        public long MessageId { get; set; }

#pragma warning disable CS8618
        public ChatMessage Message { get; set; }
#pragma warning restore CS8618

        [Column("media_file_id")]
        public long MediaFileId { get; set; }

#pragma warning disable CS8618
        public MediaFile MediaFile { get; set; }
#pragma warning restore CS8618

    }
}
