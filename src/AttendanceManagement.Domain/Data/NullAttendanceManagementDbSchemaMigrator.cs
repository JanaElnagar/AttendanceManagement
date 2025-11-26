using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace AttendanceManagement.Data;

/* This is used if database provider does't define
 * IAttendanceManagementDbSchemaMigrator implementation.
 */
public class NullAttendanceManagementDbSchemaMigrator : IAttendanceManagementDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
