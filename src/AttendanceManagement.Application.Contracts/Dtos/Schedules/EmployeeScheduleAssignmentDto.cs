using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendanceManagement.Dtos.Schedules
{
    public class EmployeeScheduleAssignmentDto
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string SeatNumber { get; set; }
        public string FloorNumber { get; set; }
    }
}

