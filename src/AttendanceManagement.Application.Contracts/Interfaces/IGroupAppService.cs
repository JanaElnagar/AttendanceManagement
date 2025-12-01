using AttendanceManagement.Dtos.Group;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace AttendanceManagement.Interfaces
{
    public interface IGroupAppService :
        ICrudAppService<GroupDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateGroupDto>
    {
        Task<GroupWithEmployeesDto> GetWithEmployeesAsync(Guid id);
        Task<List<GroupDto>> GetActiveGroupsAsync();
        Task ActivateAsync(Guid id);
        Task DeactivateAsync(Guid id);
        Task<byte[]> ExportToExcelAsync();
    }
}
