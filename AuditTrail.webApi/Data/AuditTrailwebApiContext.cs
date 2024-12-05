using AuditTrail.webApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Security.Claims;

namespace AuditTrail.webApi.Data
{
    public class AuditTrailwebApiContext : DbContext
    {
        //public AuditTrailwebApiContext(DbContextOptions<AuditTrailwebApiContext> options)
        //    : base(options)
        //{
        //}
        private readonly IHttpContextAccessor _httpContextAccessor;
        public AuditTrailwebApiContext(DbContextOptions<AuditTrailwebApiContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Product> Products { get; set; } = default!;
        public DbSet<AuditLog> AuditLogs { get; set; } = default!;
        /*
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // second way to save audit log
            var modifiedEntities = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added
                || e.State == EntityState.Modified
                || e.State == EntityState.Deleted)
                .ToList();

            foreach (var modifiedEntity in modifiedEntities)
            {
                var auditLog = new AuditLog
                {
                    EntityName = modifiedEntity.Entity.GetType().Name,
                    Action = modifiedEntity.State.ToString(),
                    Timestamp = DateTime.UtcNow,
                    Changes = GetChanges(modifiedEntity)
                };

                AuditLogs.Add(auditLog);
            }

            return base.SaveChangesAsync(cancellationToken);
        } 
        private static string GetChanges(EntityEntry entity)
        {
            var changes = new StringBuilder();

            foreach (var property in entity.OriginalValues.Properties)
            {
                var originalValue = entity.OriginalValues[property];
                var currentValue = entity.CurrentValues[property];

                if (!Equals(originalValue, currentValue))
                {
                    changes.AppendLine($"{property.Name}: From '{originalValue}' to '{currentValue}'");
                }
            }

            return changes.ToString();
        }
        */
        public override int SaveChanges()
        {
            var auditEntries = OnBeforeSaveChanges();
            var result = base.SaveChanges();
            OnAfterSaveChanges(auditEntries);
            return result;
        }
        private List<AuditEntry> OnBeforeSaveChanges()
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                {
                    continue;
                }
                var auditEntry = new AuditEntry(entry)
                {
                    TableName = entry.Entity.GetType().Name,
                    Action = entry.State.ToString(),
                    UserId = "1234"
                };
                auditEntries.Add(auditEntry);
                foreach (var property in entry.Properties)
                {
                    string propertyName = property.Metadata.Name;
                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }
                    if (entry.State == EntityState.Added)
                    {
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                    }
                    else if (entry.State == EntityState.Deleted)
                    {
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                    }
                    else if (entry.State == EntityState.Modified && property.IsModified)
                    {
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                    }
                }
            }
            foreach (var auditEntry in auditEntries.Where(e => !e.HasTemporaryProperties))
            {
                AuditLogs.Add(auditEntry.ToAuditLog());
            }
            return auditEntries.Where(e => e.HasTemporaryProperties).ToList();
        }
        private void OnAfterSaveChanges(List<AuditEntry> auditEntries)
        {
            if (auditEntries == null || auditEntries.Count == 0)
            {
                return;
            }
            foreach (var auditEntry in auditEntries)
            {
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }
                AuditLogs.Add(auditEntry.ToAuditLog());
            }
            SaveChanges();
        }
    }
}
