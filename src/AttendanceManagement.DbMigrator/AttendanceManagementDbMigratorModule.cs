using AttendanceManagement.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace AttendanceManagement.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AttendanceManagementEntityFrameworkCoreModule),
    typeof(AttendanceManagementApplicationContractsModule)
    )]
public class AttendanceManagementDbMigratorModule : AbpModule
{
}
