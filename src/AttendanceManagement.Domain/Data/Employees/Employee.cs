using AttendanceManagement.Data.ExceptionRequests;
using AttendanceManagement.Data.Groups;
using AttendanceManagement.Data.Schedules;
using AttendanceManagement.Data.Workflows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Identity;

namespace AttendanceManagement.Data.Employees
{
    public class Employee : FullAuditedAggregateRoot<Guid>
    {
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public string Sector { get; set; }
        public bool IsActive { get; set; }
        public Guid? GroupId { get; set; } // Each employee belongs to only one group
        public Guid? WorkflowId { get; set; } // Admin assigns workflow based on hierarchy

        // Navigation properties
        public virtual IdentityUser User { get; set; }
        public virtual Group Group { get; set; }
        public virtual Workflow Workflow { get; set; }
        public virtual ICollection<ManagerAssignment> ManagerAssignments { get; set; }
        public virtual ICollection<ManagerAssignment> ManagedEmployees { get; set; }
        public virtual ICollection<ScheduleAssignment> ScheduleAssignments { get; set; }
        public virtual ICollection<ExceptionRequest> ExceptionRequests { get; set; }

        protected Employee()
        {
            ManagerAssignments = new List<ManagerAssignment>();
            ManagedEmployees = new List<ManagerAssignment>();
            ScheduleAssignments = new List<ScheduleAssignment>();
            ExceptionRequests = new List<ExceptionRequest>();
        }

        public Employee(Guid id, Guid userId, string name, string department, string sector, Guid? groupId = null)
            : base(id)
        {
            UserId = userId;
            Name = name;
            Department = department;
            Sector = sector;
            GroupId = groupId;
            IsActive = true;

            ManagerAssignments = new List<ManagerAssignment>();
            ManagedEmployees = new List<ManagerAssignment>();
            ScheduleAssignments = new List<ScheduleAssignment>();
            ExceptionRequests = new List<ExceptionRequest>();
        }

        public void AssignToGroup(Guid? groupId)
        {
            GroupId = groupId;
        }

        public void AssignWorkflow(Guid? workflowId)
        {
            WorkflowId = workflowId;
        }
    }
}
