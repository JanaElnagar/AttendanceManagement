using AttendanceManagement.Data.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace AttendanceManagement.Data.Schedules
{
    public interface IScheduleRepository : IRepository<ScheduleAssignment, Guid>
    {
        public Task<ScheduleAssignment> GetScheduleAssignmentByEmployeeId(Guid id);
    }
}
