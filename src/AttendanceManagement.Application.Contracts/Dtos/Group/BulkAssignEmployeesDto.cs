using System;
using System.Collections.Generic;

namespace AttendanceManagement.Dtos.Group
{
    public class BulkAssignEmployeesDto
    {
        public Guid GroupId { get; set; }
        public List<Guid> EmployeeIds { get; set; } = new();
    }
}

