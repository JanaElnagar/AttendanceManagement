using Volo.Abp.Modularity;

namespace AttendanceManagement;

public abstract class AttendanceManagementApplicationTestBase<TStartupModule> : AttendanceManagementTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
