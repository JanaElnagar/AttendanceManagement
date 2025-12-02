using System;
using System.Collections.Generic;

namespace AttendanceManagement.Dtos.Employees
{
    public class ImportResultDto
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<ImportErrorDto> Errors { get; set; } = new List<ImportErrorDto>();
    }

    public class ImportErrorDto
    {
        public int RowNumber { get; set; }
        public string EmployeeName { get; set; }
        public List<string> ErrorMessages { get; set; } = new List<string>();
    }
}

