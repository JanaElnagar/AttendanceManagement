using Volo.Abp.Modularity;

namespace AttendanceManagement;

/* Inherit from this class for your domain layer tests. */
public abstract class AttendanceManagementDomainTestBase<TStartupModule> : AttendanceManagementTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
