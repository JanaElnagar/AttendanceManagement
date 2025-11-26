using Volo.Abp.Modularity;

namespace AttendanceManagement;

[DependsOn(
    typeof(AttendanceManagementDomainModule),
    typeof(AttendanceManagementTestBaseModule)
)]
public class AttendanceManagementDomainTestModule : AbpModule
{

}
