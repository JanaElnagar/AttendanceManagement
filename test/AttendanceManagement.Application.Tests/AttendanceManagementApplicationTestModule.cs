using Volo.Abp.Modularity;

namespace AttendanceManagement;

[DependsOn(
    typeof(AttendanceManagementApplicationModule),
    typeof(AttendanceManagementDomainTestModule)
)]
public class AttendanceManagementApplicationTestModule : AbpModule
{

}
