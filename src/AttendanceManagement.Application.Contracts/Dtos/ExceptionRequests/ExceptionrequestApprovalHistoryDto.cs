using AttendanceManagement.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace AttendanceManagement.Dtos.ExceptionRequests
{
    public class ExceptionRequestApprovalHistoryDto : EntityDto<Guid>
    {
        public Guid ExceptionRequestId { get; set; }
        public Guid WorkflowStepId { get; set; }
        public Guid? ApproverEmployeeId { get; set; } // Nullable to support external doctors
        public string ApproverEmployeeName { get; set; }
        public int StepOrder { get; set; }
        public ApprovalAction Action { get; set; }
        public string Notes { get; set; }
        public DateTime ActionDateTime { get; set; }
    }
}
