using System.Threading.Tasks;

namespace AttendanceManagement.Data;

public interface IAttendanceManagementDbSchemaMigrator
{
    Task MigrateAsync();
}
