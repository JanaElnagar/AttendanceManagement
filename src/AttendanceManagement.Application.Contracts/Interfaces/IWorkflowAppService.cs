using AttendanceManagement.Dtos.Workflows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace AttendanceManagement.Interfaces
{
    public interface IWorkflowAppService :
        ICrudAppService<WorkflowDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateWorkflowDto>
    {
        Task<List<WorkflowDto>> GetActiveWorkflowsAsync();
        Task ActivateAsync(Guid id);
        Task DeactivateAsync(Guid id);
        Task AssignWorkflowToEmployeeAsync(Guid employeeId, Guid workflowId);
        Task<byte[]> ExportToExcelAsync();
    }
}
