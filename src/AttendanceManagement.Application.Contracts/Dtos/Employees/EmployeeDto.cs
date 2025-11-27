using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace AttendanceManagement.Dtos.Employees
{
    public class EmployeeDto : FullAuditedEntityDto<Guid>
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }  // from IdentityUser
        public string Email { get; set; }       // from IdentityUser
        public string Name { get; set; }
        public string Department { get; set; }
        public string Sector { get; set; }
        public bool IsActive { get; set; }
        public Guid? GroupId { get; set; }
        public string GroupName { get; set; }
        public Guid? WorkflowId { get; set; }
        public string WorkflowName { get; set; }
    }
}
