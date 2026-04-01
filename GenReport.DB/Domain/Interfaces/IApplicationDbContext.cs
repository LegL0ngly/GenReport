using GenReport.DB.Domain.Entities.Core;
using GenReport.Domain.Entities.Media;
using GenReport.Domain.Entities.Onboarding;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenReport.DB.Domain.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<User> Users { get; set; }
        DbSet<MediaFile> MediaFiles { get; set; }
        DbSet<Database> Databases { get; set; }
        DbSet<Query> Queries { get; set; }
        DbSet<Report> Reports { get; set; }
        DbSet<AiConnection> AiConnections { get; set; }
        DbSet<ChatSession> ChatSessions { get; set; }
        DbSet<ChatMessage> ChatMessages { get; set; }
        DbSet<MessageReport> MessageReports { get; set; }
        DbSet<MessageAttachment> MessageAttachments { get; set; }
        DbSet<SchemaObject> SchemaObjects { get; set; }
        DbSet<RoutineObject> RoutineObjects { get; set; }
        DbSet<AiConfig> AiConfigs { get; set; }
    }
}
