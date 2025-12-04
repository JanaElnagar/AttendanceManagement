using AttendanceManagement.Dtos.Schedules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace AttendanceManagement.Interfaces
{
    public interface IScheduleAppService :
        ICrudAppService<ScheduleDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateScheduleDto>
    {
        Task<List<ScheduleDto>> GetActiveSchedulesAsync();
        Task ActivateAsync(Guid id);
        Task DeactivateAsync(Guid id);
        Task AssignScheduleAsync(AssignScheduleDto input);
        Task AssignScheduleToGroupMembersAsync(AssignScheduleToGroupMembersDto input);
        Task<ScheduleAssignmentDto> GetEmployeeCurrentScheduleAsync(Guid employeeId);
        Task<List<ScheduleAssignmentDto>> GetEmployeeScheduleAssignmentsAsync(Guid employeeId);
        Task UpdateScheduleAssignmentAsync(Guid assignmentId, AssignScheduleDto input);
        Task EndScheduleAssignmentAsync(Guid assignmentId, DateTime endDate);
        Task<byte[]> ExportEmployeeScheduleAsync(Guid employeeId);
        Task<byte[]> ExportToExcelAsync();
    }
}
