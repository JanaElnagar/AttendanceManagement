using AttendanceManagement.Dtos.ExceptionRequests;
using AttendanceManagement.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace AttendanceManagement.Interfaces
{
    public interface IExceptionRequestAppService :
        IApplicationService
    {
        Task<ExceptionRequestDto> GetAsync(Guid id);
        Task<PagedResultDto<ExceptionRequestDto>> GetListAsync(PagedAndSortedResultRequestDto input);
        Task<ExceptionRequestDto> CreateAsync(CreateExceptionRequestDto input);
        Task<ExceptionRequestDto> ApproveOrRejectAsync(ApproveRejectExceptionRequestDto input);
        Task<List<ExceptionRequestDto>> GetMyRequestsAsync();
        Task<List<ExceptionRequestDto>> GetPendingApprovalsAsync();
        Task CancelAsync(Guid id);
        Task<Guid> UploadAttachmentAsync(Guid exceptionRequestId, IRemoteStreamContent file, AttachmentType attachmentType);
        Task<byte[]> DownloadAttachmentAsync(Guid attachmentId);
    }
}
