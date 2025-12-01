using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.Workflows;
using AttendanceManagement.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;

namespace AttendanceManagement.Data.ExceptionRequests
{
    public class ExceptionRequestApprovalHistory : Entity<Guid>
    {
        public Guid ExceptionRequestId { get; set; }
        public Guid WorkflowStepId { get; set; }
        public Guid? ApproverEmployeeId { get; set; } // Nullable to support external doctors
        public int StepOrder { get; set; }
        public ApprovalAction Action { get; set; }
        public string Notes { get; set; }
        public DateTime ActionDateTime { get; set; }

        // Navigation properties
        public virtual ExceptionRequest ExceptionRequest { get; set; }
        public virtual WorkflowStep WorkflowStep { get; set; }
        public virtual Employee ApproverEmployee { get; set; }

        protected ExceptionRequestApprovalHistory() { }

        public ExceptionRequestApprovalHistory(
            Guid id,
            Guid exceptionRequestId,
            Guid workflowStepId,
            Guid? approverEmployeeId, // Nullable to support external doctors
            int stepOrder,
            ApprovalAction action,
            string notes) : base(id)
        {
            ExceptionRequestId = exceptionRequestId;
            WorkflowStepId = workflowStepId;
            ApproverEmployeeId = approverEmployeeId;
            StepOrder = stepOrder;
            Action = action;
            Notes = notes;
            ActionDateTime = DateTime.UtcNow;
        }
    }
}
