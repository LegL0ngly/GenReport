using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenReport.Domain.Entities.Media
{
    /// <summary>
    /// This class represents a media file stored in the system.
    /// </summary>
    [Table("mediafiles")] // Table name mapping
    public class  MediaFile(
        string? storageUrl, 
        string fileName, 
        string mimeType, long size) : BaseEntity
    {
      

        /// <summary>
        /// The URL where the media file is stored.
        /// </summary>
        [Column("storage_url")] // Column name mapping
        public string? StorageUrl { get; set; } = storageUrl;

        /// <summary>
        /// The original filename of the media file.
        /// </summary>
        [Column("file_name")] // Column name mapping
        [Required] // Enforces non-null value
        public string FileName { get; set; } = fileName;

        /// <summary>
        /// The MIME type of the media file, indicating its content type.
        /// </summary>
        [Column("mime_type")] // Column name mapping
        [Required] // Enforces non-null value
        public string MimeType { get; set; } = mimeType;

        /// <summary>
        /// The size of the media file in bytes.
        /// </summary>
        [Column("size")] // Column name mapping
        public long Size { get; set; } = size;

    }
}