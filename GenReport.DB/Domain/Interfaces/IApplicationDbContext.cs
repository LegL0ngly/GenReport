using GenReport.DB.Domain.Entities.Core;
using GenReport.DB.Domain.Entities.Business;
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
        public DbSet<User> Users { get; set; }
        public DbSet<MediaFile> MediaFiles { get; set; }
        public DbSet<Database> Databases { get; set; }
        public DbSet<Query> Queries { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<RoleModuleMapping> RoleModules { get; set; }
        public DbSet<AiConnection> AiConnections { get; set; }
        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<MessageReport> MessageReports { get; set; }
        public DbSet<MessageAttachment> MessageAttachments { get; set; }
        public DbSet<SchemaObject> SchemaObjects { get; set; }
        public DbSet<RoutineObject> RoutineObjects { get; set; }
        public DbSet<AiConfig> AiConfigs { get; set; }
    }
}
