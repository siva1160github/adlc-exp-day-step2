using System.Threading;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Infrastructure.Cosmos;

public interface IAuditRepository
{
    Task AddAsync(AuditRecord record, CancellationToken cancellationToken);
    Task<AuditRecord?> GetByIdAsync(string id, CancellationToken cancellationToken);
}
