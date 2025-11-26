using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AttendanceManagement.Data;
using Volo.Abp.DependencyInjection;

namespace AttendanceManagement.EntityFrameworkCore;

public class EntityFrameworkCoreAttendanceManagementDbSchemaMigrator
    : IAttendanceManagementDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreAttendanceManagementDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolve the AttendanceManagementDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<AttendanceManagementDbContext>()
            .Database
            .MigrateAsync();
    }
}
