using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.Schedules;
using AttendanceManagement.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace AttendanceManagement.Repositories
{
    public class ScheduleRepository(IDbContextProvider<AttendanceManagementDbContext> dbContextProvider) : EfCoreRepository<AttendanceManagementDbContext, ScheduleAssignment, Guid>(dbContextProvider), IScheduleRepository
    {
        public async Task<ScheduleAssignment> GetScheduleAssignmentByEmployeeId(Guid id)
        {
            var query = await GetQueryableAsync();
            return query
                .Where(sa => sa.EmployeeId == id && sa.EffectiveFrom <= DateTime.Now && (sa.EffectiveTo == null || sa.EffectiveTo >= DateTime.Now))
                .FirstOrDefault();
        }

    }
}
