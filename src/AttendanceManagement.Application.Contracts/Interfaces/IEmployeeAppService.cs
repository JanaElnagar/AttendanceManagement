using AttendanceManagement.Dtos.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace AttendanceManagement.Interfaces
{
    public interface IEmployeeAppService :
        ICrudAppService<EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>
    {
        Task<EmployeeWithDetailsDto> GetWithDetailsAsync(Guid id);
        Task<List<EmployeeDto>> GetActiveEmployeesAsync();
        Task<EmployeeDto> GetByUserIdAsync(Guid userId);
        Task ActivateAsync(Guid id);
        Task DeactivateAsync(Guid id);
        Task AssignManagerAsync(AssignManagerDto input);
        Task<List<ManagerAssignmentDto>> GetManagersAsync(Guid employeeId);
        Task<byte[]> ExportToExcelAsync();
    }
}
