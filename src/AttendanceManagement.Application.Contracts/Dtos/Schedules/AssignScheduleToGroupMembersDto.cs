using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendanceManagement.Dtos.Schedules
{
    public class AssignScheduleToGroupMembersDto
    {
        [Required(ErrorMessage = "Group is required")]
        public Guid GroupId { get; set; }

        [Required(ErrorMessage = "Schedule is required")]
        public Guid ScheduleId { get; set; }

        [Required(ErrorMessage = "Effective from date is required")]
        public DateTime EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }

        public List<EmployeeScheduleAssignmentDto> EmployeeAssignments { get; set; } = new();
    }
}

