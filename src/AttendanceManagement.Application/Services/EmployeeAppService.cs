using AttendanceManagement.Data.Employees;
using EmployeeGroup = AttendanceManagement.Data.Groups.Group;
using AttendanceManagement.Data.Schedules;
using AttendanceManagement.Data.Workflows;
using AttendanceManagement.Dtos.Employees;
using AttendanceManagement.Dtos.Schedules;
using AttendanceManagement.Interfaces;
using AttendanceManagement.Permissions;
using AutoMapper.Internal.Mappers;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using System.Text.RegularExpressions;

namespace AttendanceManagement.Services
{
    public class EmployeeAppService :
        CrudAppService<Employee, EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>,
        IEmployeeAppService
    {
        private readonly IRepository<EmployeeGroup, Guid> _groupRepository;
        private readonly IRepository<Workflow, Guid> _workflow_repository;
        private readonly IRepository<Schedule, Guid> _schedule_repository;
        private readonly IIdentityUserRepository _identityUserRepository;
        private readonly IdentityUserManager _identityUserManager;
        private readonly ILogger<EmployeeAppService> _logger;

        public EmployeeAppService(
            ILogger<EmployeeAppService> logger,
            IRepository<Employee, Guid> repository,
            IRepository<EmployeeGroup, Guid> groupRepository,
            IRepository<Workflow, Guid> workflowRepository,
            IRepository<Schedule, Guid> scheduleRepository,
            IIdentityUserRepository identityUserRepository,
            IdentityUserManager identityUserManager)
            : base(repository)
        {
            _logger = logger;
            _groupRepository = groupRepository;
            _workflow_repository = workflowRepository;
            _schedule_repository = scheduleRepository;
            _identityUserRepository = identityUserRepository;
            _identityUserManager = identityUserManager;

            GetPolicyName = AttendanceManagementPermissions.Employees.Default;
            GetListPolicyName = AttendanceManagementPermissions.Employees.Default;
            CreatePolicyName = AttendanceManagementPermissions.Employees.Create;
            UpdatePolicyName = AttendanceManagementPermissions.Employees.Edit;
            DeletePolicyName = AttendanceManagementPermissions.Employees.Delete;
        }

        public async Task<EmployeeWithDetailsDto> GetWithDetailsAsync(Guid id)
        {
            _logger.LogDebug("GetWithDetailsAsync start: id={Id}", id);

            var employeeQueryable = await Repository
                .WithDetailsAsync(e => e.User, e => e.Group, e => e.Workflow, e => e.ScheduleAssignments, e => e.ManagerAssignments);

            var emp = await employeeQueryable.FirstOrDefaultAsync(e => e.Id == id);

            if (emp == null)
            {
                _logger.LogWarning("Employee not found. id={Id}", id);
                throw new UserFriendlyException($"Employee with id {id} not found.");
            }

            var dto = ObjectMapper.Map<Employee, EmployeeWithDetailsDto>(emp);

            // Get current schedule
            var currentScheduleAssignment = emp.ScheduleAssignments
                .Where(sa => sa.EffectiveFrom <= DateTime.UtcNow
                    && (sa.EffectiveTo == null || sa.EffectiveTo >= DateTime.UtcNow))
                .FirstOrDefault();

            if (currentScheduleAssignment != null)
            {
                // Query schedules directly from the Schedule repository and include days
                var scheduleQueryable = await _schedule_repository.GetQueryableAsync();
                var scheduleWithDays = await scheduleQueryable
                    .Where(s => s.Id == currentScheduleAssignment.ScheduleId)
                    .Include(s => s.ScheduleDays)
                    .FirstOrDefaultAsync();

                if (scheduleWithDays != null)
                {
                    dto.CurrentSchedule = ObjectMapper.Map<Schedule, ScheduleDto>(scheduleWithDays);
                    dto.ScheduleDays = ObjectMapper.Map<List<ScheduleDay>, List<ScheduleDayDto>>(
                        scheduleWithDays.ScheduleDays?.ToList() ?? new List<ScheduleDay>());
                    dto.SeatNumber = currentScheduleAssignment.SeatNumber;
                    dto.FloorNumber = currentScheduleAssignment.FloorNumber;
                }
            }

            // Get primary manager
            var primaryManager = emp.ManagerAssignments
                .Where(ma => ma.IsPrimaryManager
                    && ma.EffectiveFrom <= DateTime.UtcNow
                    && (ma.EffectiveTo == null || ma.EffectiveTo >= DateTime.UtcNow))
                .FirstOrDefault();

            if (primaryManager != null)
            {
                var manager = await Repository.GetAsync(primaryManager.ManagerEmployeeId);
                dto.PrimaryManagerName = manager.Name;
            }

            return dto;
        }

        protected override async Task<IQueryable<Employee>> CreateFilteredQueryAsync(PagedAndSortedResultRequestDto input)
        {
            return await Repository.WithDetailsAsync(e => e.User, e => e.Group, e => e.Workflow);
        }

        public async Task<List<EmployeeDto>> GetActiveEmployeesAsync()
        {
            var queryable = await Repository.WithDetailsAsync(e => e.User, e => e.Group, e => e.Workflow);
            var employees = await queryable
                .Where(e => e.IsActive)
                .OrderBy(e => e.Name)
                .ToListAsync();

            return ObjectMapper.Map<List<Employee>, List<EmployeeDto>>(employees);
        }
        public async Task<EmployeeDto> GetByUserIdAsync(Guid userId)
        {
            var employee = await Repository.FirstOrDefaultAsync(e => e.UserId == userId);
            if (employee == null)
            {
                throw new UserFriendlyException("Employee record not found for current user.");
            }
            return ObjectMapper.Map<Employee, EmployeeDto>(employee);
        }
        public async Task ActivateAsync(Guid id)
        {
            await CheckUpdatePolicyAsync();

            var employee = await Repository.GetAsync(id);
            employee.IsActive = true;
            await Repository.UpdateAsync(employee);
        }

        public async Task DeactivateAsync(Guid id)
        {
            await CheckUpdatePolicyAsync();

            var employee = await Repository.GetAsync(id);
            employee.IsActive = false;
            await Repository.UpdateAsync(employee);
        }

        public async Task AssignManagerAsync(AssignManagerDto input)
        {
            await CheckUpdatePolicyAsync();

            // Validate employee exists
            var employee = await Repository.GetAsync(input.EmployeeId);

            // Validate manager exists
            var manager = await Repository.GetAsync(input.ManagerEmployeeId);

            // Validate employee is not assigning themselves as manager
            if (input.EmployeeId == input.ManagerEmployeeId)
            {
                throw new UserFriendlyException("An employee cannot be assigned as their own manager.");
            }

            var assignment = new ManagerAssignment(
                GuidGenerator.Create(),
                input.EmployeeId,
                input.ManagerEmployeeId,
                input.IsPrimaryManager,
                input.EffectiveFrom
            );

            employee.ManagerAssignments.Add(assignment);
            manager.ManagedEmployees.Add(assignment);

            await Repository.UpdateAsync(employee);
            await Repository.UpdateAsync(manager);
        }

        public async Task<List<ManagerAssignmentDto>> GetManagersAsync(Guid employeeId)
        {
            var employee = await Repository.GetAsync(employeeId);

            var assignments = employee.ManagerAssignments
                .Where(ma => ma.EffectiveFrom <= DateTime.UtcNow
                    && (ma.EffectiveTo == null || ma.EffectiveTo >= DateTime.UtcNow))
                .ToList();

            var dtos = new List<ManagerAssignmentDto>();
            foreach (var assignment in assignments)
            {
                var manager = await Repository.GetAsync(assignment.ManagerEmployeeId);
                var dto = ObjectMapper.Map<ManagerAssignment, ManagerAssignmentDto>(assignment);
                dto.ManagerName = manager.Name;
                dto.EmployeeName = employee.Name;
                dtos.Add(dto);
            }

            return dtos;
        }

        public override async Task<EmployeeDto> CreateAsync(CreateUpdateEmployeeDto input)
        {
            // Check if employee with this UserId already exists
            var existingEmployee = await Repository.FirstOrDefaultAsync(e => e.UserId == input.UserId);
            if (existingEmployee != null)
            {
                throw new UserFriendlyException("An employee with this user already exists.");
            }

            // Check if CompanyId is provided and if it's unique
            if (!string.IsNullOrWhiteSpace(input.CompanyId))
            {
                var existingCompanyId = await Repository.FirstOrDefaultAsync(e => e.CompanyId == input.CompanyId);
                if (existingCompanyId != null)
                {
                    throw new UserFriendlyException("An employee with this Company ID already exists.");
                }
            }

            // Validate GroupId if provided
            if (input.GroupId.HasValue)
            {
                var groupExists = await _groupRepository.AnyAsync(g => g.Id == input.GroupId.Value);
                if (!groupExists)
                {
                    throw new UserFriendlyException("The specified group does not exist.");
                }
            }

            // Validate WorkflowId if provided
            if (input.WorkflowId.HasValue)
            {
                var workflowExists = await _workflow_repository.AnyAsync(w => w.Id == input.WorkflowId.Value);
                if (!workflowExists)
                {
                    throw new UserFriendlyException("The specified workflow does not exist.");
                }
            }

            var employee = new Employee(
                GuidGenerator.Create(),
                input.UserId,
                input.Name,
                input.Department,
                input.Sector,
                input.GroupId,
                input.Email,
                input.CompanyId
            );

            if (input.WorkflowId.HasValue)
            {
                employee.AssignWorkflow(input.WorkflowId.Value);
            }

            await Repository.InsertAsync(employee);
            return ObjectMapper.Map<Employee, EmployeeDto>(employee);
        }

        public override async Task<EmployeeDto> UpdateAsync(Guid id, CreateUpdateEmployeeDto input)
        {
            var employee = await Repository.GetAsync(id);

            // Check if another employee with this UserId exists (excluding current employee)
            var existingEmployee = await Repository.FirstOrDefaultAsync(e => e.UserId == input.UserId && e.Id != id);
            if (existingEmployee != null)
            {
                throw new UserFriendlyException("Another employee with this user already exists.");
            }

            // Check if CompanyId is provided and if it's unique (excluding current employee)
            if (!string.IsNullOrWhiteSpace(input.CompanyId))
            {
                var existingCompanyId = await Repository.FirstOrDefaultAsync(e => e.CompanyId == input.CompanyId && e.Id != id);
                if (existingCompanyId != null)
                {
                    throw new UserFriendlyException("Another employee with this Company ID already exists.");
                }
            }

            // Validate GroupId if provided
            if (input.GroupId.HasValue)
            {
                var groupExists = await _groupRepository.AnyAsync(g => g.Id == input.GroupId.Value);
                if (!groupExists)
                {
                    throw new UserFriendlyException("The specified group does not exist.");
                }
            }

            // Validate WorkflowId if provided
            if (input.WorkflowId.HasValue)
            {
                var workflowExists = await _workflow_repository.AnyAsync(w => w.Id == input.WorkflowId.Value);
                if (!workflowExists)
                {
                    throw new UserFriendlyException("The specified workflow does not exist.");
                }
            }

            employee.Name = input.Name;
            employee.Department = input.Department;
            employee.Sector = input.Sector;
            employee.Email = input.Email;
            employee.CompanyId = input.CompanyId;
            employee.AssignToGroup(input.GroupId);
            employee.AssignWorkflow(input.WorkflowId);

            await Repository.UpdateAsync(employee);
            return ObjectMapper.Map<Employee, EmployeeDto>(employee);
        }

        public async Task<byte[]> ExportToExcelAsync()
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.Employees.Export);

            var queryable = await Repository.WithDetailsAsync(e => e.User, e => e.Group, e => e.Workflow);
            var employees = await queryable
                .OrderBy(e => e.Name)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Employees");

                // Title
                worksheet.Cell(1, 1).Value = "Employees List";
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Range(1, 1, 1, 8).Merge();

                // Headers
                var headerRow = 3;
                worksheet.Cell(headerRow, 1).Value = "Name";
                worksheet.Cell(headerRow, 2).Value = "Email";
                worksheet.Cell(headerRow, 3).Value = "Company ID";
                worksheet.Cell(headerRow, 4).Value = "Department";
                worksheet.Cell(headerRow, 5).Value = "Sector";
                worksheet.Cell(headerRow, 6).Value = "Group";
                worksheet.Cell(headerRow, 7).Value = "Workflow";
                worksheet.Cell(headerRow, 8).Value = "Status";

                // Style headers
                var headerRange = worksheet.Range(headerRow, 1, headerRow, 8);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Data
                int row = headerRow + 1;
                foreach (var employee in employees)
                {
                    worksheet.Cell(row, 1).Value = employee.Name;
                    worksheet.Cell(row, 2).Value = employee.Email ?? "-";
                    worksheet.Cell(row, 3).Value = employee.CompanyId ?? "-";
                    worksheet.Cell(row, 4).Value = employee.Department ?? "-";
                    worksheet.Cell(row, 5).Value = employee.Sector ?? "-";
                    worksheet.Cell(row, 6).Value = employee.Group?.Name ?? "-";
                    worksheet.Cell(row, 7).Value = employee.Workflow?.Name ?? "-";
                    worksheet.Cell(row, 8).Value = employee.IsActive ? "Active" : "Inactive";
                    row++;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public async Task<byte[]> DownloadImportTemplateAsync()
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.Employees.Create);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Employee Import Template");

                // Instructions
                worksheet.Cell(1, 1).Value = "Employee Import Template";
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Range(1, 1, 1, 8).Merge();

                worksheet.Cell(3, 1).Value = "Instructions:";
                worksheet.Cell(3, 1).Style.Font.Bold = true;
                worksheet.Cell(4, 1).Value = "1. Fill in all required fields (marked with *).";
                worksheet.Cell(5, 1).Value = "2. Email must be unique and valid format.";
                worksheet.Cell(6, 1).Value = "3. Company ID must be unique (e.g., Y1431).";
                worksheet.Cell(7, 1).Value = "4. Department and Sector are required fields.";
                worksheet.Cell(8, 1).Value = "5. UserName is optional - if not provided, will be generated from email.";
                worksheet.Cell(9, 1).Value = "6. GroupName and WorkflowName must match existing names (case-sensitive).";

                // Headers
                var headerRow = 11;
                worksheet.Cell(headerRow, 1).Value = "Name *";
                worksheet.Cell(headerRow, 2).Value = "Email *";
                worksheet.Cell(headerRow, 3).Value = "Company ID *";
                worksheet.Cell(headerRow, 4).Value = "Department *";
                worksheet.Cell(headerRow, 5).Value = "Sector *";
                worksheet.Cell(headerRow, 6).Value = "GroupName";
                worksheet.Cell(headerRow, 7).Value = "WorkflowName";
                worksheet.Cell(headerRow, 8).Value = "UserName";

                // Style headers
                var headerRange = worksheet.Range(headerRow, 1, headerRow, 8);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Example row
                var exampleRow = headerRow + 1;
                worksheet.Cell(exampleRow, 1).Value = "John Doe";
                worksheet.Cell(exampleRow, 2).Value = "john.doe@company.com";
                worksheet.Cell(exampleRow, 3).Value = "Y1431";
                worksheet.Cell(exampleRow, 4).Value = "IT";
                worksheet.Cell(exampleRow, 5).Value = "Technology";
                worksheet.Cell(exampleRow, 6).Value = "Development Team";
                worksheet.Cell(exampleRow, 7).Value = "Standard Workflow";
                worksheet.Cell(exampleRow, 8).Value = "johndoe";

                // Style example row
                var exampleRange = worksheet.Range(exampleRow, 1, exampleRow, 8);
                exampleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7F3FF");
                exampleRange.Style.Font.Italic = true;

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public async Task<ImportResultDto> ImportFromExcelAsync(byte[] fileData)
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.Employees.Create);

            var result = new ImportResultDto();
            var errors = new List<ImportErrorDto>();

            using (var stream = new MemoryStream(fileData))
            using (var workbook = new XLWorkbook(stream))
            {
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    throw new UserFriendlyException("Excel file is empty or invalid.");
                }

                // Find header row (look for "Name" column)
                int headerRow = 0;
                for (int row = 1; row <= 20; row++)
                {
                    var cellValue = worksheet.Cell(row, 1).GetString().Trim();
                    if (cellValue.Equals("Name", StringComparison.OrdinalIgnoreCase) || 
                        cellValue.Equals("Name *", StringComparison.OrdinalIgnoreCase))
                    {
                        headerRow = row;
                        break;
                    }
                }

                if (headerRow == 0)
                {
                    throw new UserFriendlyException("Could not find header row in Excel file.");
                }

                // Process data rows
                int rowNumber = headerRow + 1;
                while (rowNumber <= worksheet.LastRowUsed().RowNumber())
                {
                    var name = worksheet.Cell(rowNumber, 1).GetString().Trim();
                    var email = worksheet.Cell(rowNumber, 2).GetString().Trim();
                    var companyId = worksheet.Cell(rowNumber, 3).GetString().Trim();
                    var department = worksheet.Cell(rowNumber, 4).GetString().Trim();
                    var sector = worksheet.Cell(rowNumber, 5).GetString().Trim();
                    var groupName = worksheet.Cell(rowNumber, 6).GetString().Trim();
                    var workflowName = worksheet.Cell(rowNumber, 7).GetString().Trim();
                    var userName = worksheet.Cell(rowNumber, 8).GetString().Trim();

                    result.TotalRows++;

                    // Skip empty rows
                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(companyId))
                    {
                        rowNumber++;
                        continue;
                    }

                    var rowErrors = new List<string>();

                    // Validate required fields
                    if (string.IsNullOrWhiteSpace(name))
                        rowErrors.Add("Name is required.");

                    if (string.IsNullOrWhiteSpace(email))
                        rowErrors.Add("Email is required.");
                    else if (!IsValidEmail(email))
                        rowErrors.Add("Invalid email format.");

                    if (string.IsNullOrWhiteSpace(companyId))
                        rowErrors.Add("Company ID is required.");

                    if (string.IsNullOrWhiteSpace(department))
                        rowErrors.Add("Department is required.");

                    if (string.IsNullOrWhiteSpace(sector))
                        rowErrors.Add("Sector is required.");

                    // Validate field lengths
                    if (!string.IsNullOrWhiteSpace(name) && name.Length > 200)
                        rowErrors.Add("Name cannot exceed 200 characters.");

                    if (!string.IsNullOrWhiteSpace(email) && email.Length > 256)
                        rowErrors.Add("Email cannot exceed 256 characters.");

                    if (!string.IsNullOrWhiteSpace(companyId) && companyId.Length > 100)
                        rowErrors.Add("Company ID cannot exceed 100 characters.");

                    if (!string.IsNullOrWhiteSpace(department) && department.Length > 100)
                        rowErrors.Add("Department cannot exceed 100 characters.");

                    if (!string.IsNullOrWhiteSpace(sector) && sector.Length > 100)
                        rowErrors.Add("Sector cannot exceed 100 characters.");

                    // Perform all validations BEFORE creating user
                    if (rowErrors.Any())
                    {
                        errors.Add(new ImportErrorDto
                        {
                            RowNumber = rowNumber,
                            EmployeeName = name ?? "Unknown",
                            ErrorMessages = rowErrors
                        });
                        result.FailureCount++;
                        rowNumber++;
                        continue;
                    }

                    // Check if CompanyId is unique (before creating user)
                    var existingCompanyId = await Repository.FirstOrDefaultAsync(e => e.CompanyId == companyId);
                    if (existingCompanyId != null)
                    {
                        rowErrors.Add($"Company ID already exists: {existingCompanyId.Name}");
                        errors.Add(new ImportErrorDto
                        {
                            RowNumber = rowNumber,
                            EmployeeName = name,
                            ErrorMessages = rowErrors
                        });
                        result.FailureCount++;
                        rowNumber++;
                        continue;
                    }

                    // Find Group by name if provided (before creating user)
                    Guid? groupId = null;
                    if (!string.IsNullOrWhiteSpace(groupName))
                    {
                        var group = await _groupRepository.FirstOrDefaultAsync(g => g.Name == groupName);
                        if (group == null)
                        {
                            rowErrors.Add($"Group not found: {groupName}");
                            errors.Add(new ImportErrorDto
                            {
                                RowNumber = rowNumber,
                                EmployeeName = name,
                                ErrorMessages = rowErrors
                            });
                            result.FailureCount++;
                            rowNumber++;
                            continue;
                        }
                        groupId = group.Id;
                    }

                    // Find Workflow by name if provided (before creating user)
                    Guid? workflowId = null;
                    if (!string.IsNullOrWhiteSpace(workflowName))
                    {
                        var workflow = await _workflow_repository.FirstOrDefaultAsync(w => w.Name == workflowName);
                        if (workflow == null)
                        {
                            rowErrors.Add($"Workflow not found: {workflowName}");
                            errors.Add(new ImportErrorDto
                            {
                                RowNumber = rowNumber,
                                EmployeeName = name,
                                ErrorMessages = rowErrors
                            });
                            result.FailureCount++;
                            rowNumber++;
                            continue;
                        }
                        workflowId = workflow.Id;
                    }

                    // Check if user already exists and has an employee record
                    IdentityUser existingUser = null;
                    if (!string.IsNullOrWhiteSpace(userName))
                    {
                        existingUser = await _identityUserRepository.FindByNormalizedUserNameAsync(userName.ToUpperInvariant());
                    }

                    if (existingUser == null && !string.IsNullOrWhiteSpace(email))
                    {
                        existingUser = await _identityUserManager.FindByEmailAsync(email);
                    }

                    if (existingUser != null)
                    {
                        // Check if employee with this UserId already exists
                        var existingEmployee = await Repository.FirstOrDefaultAsync(e => e.UserId == existingUser.Id);
                        if (existingEmployee != null)
                        {
                            rowErrors.Add($"Employee with this user already exists: {existingEmployee.Name}");
                            errors.Add(new ImportErrorDto
                            {
                                RowNumber = rowNumber,
                                EmployeeName = name,
                                ErrorMessages = rowErrors
                            });
                            result.FailureCount++;
                            rowNumber++;
                            continue;
                        }
                    }

                    // Try to create/update employee - only create user if all validations pass
                    try
                    {
                        IdentityUser user = existingUser;

                        // Only create user if it doesn't exist and all validations passed
                        if (user == null)
                        {
                            // Create new user
                            var generatedUserName = !string.IsNullOrWhiteSpace(userName) 
                                ? userName 
                                : email.Split('@')[0].ToLowerInvariant();

                            // Ensure username is unique
                            var baseUserName = generatedUserName;
                            int counter = 1;
                            while (await _identityUserRepository.FindByNormalizedUserNameAsync(generatedUserName.ToUpperInvariant()) != null)
                            {
                                generatedUserName = $"{baseUserName}{counter}";
                                counter++;
                            }

                            user = new IdentityUser(GuidGenerator.Create(), generatedUserName, email)
                            {
                                Name = name
                            };
                            await _identityUserRepository.InsertAsync(user);
                        }

                        // Create employee
                        var createDto = new CreateUpdateEmployeeDto
                        {
                            UserId = user.Id,
                            Name = name,
                            Email = email,
                            CompanyId = companyId,
                            Department = department,
                            Sector = sector,
                            GroupId = groupId,
                            WorkflowId = workflowId
                        };

                        await CreateAsync(createDto);
                        result.SuccessCount++;
                    }
                    catch (UserFriendlyException ex)
                    {
                        rowErrors.Add(ex.Message);
                        errors.Add(new ImportErrorDto
                        {
                            RowNumber = rowNumber,
                            EmployeeName = name,
                            ErrorMessages = rowErrors
                        });
                        result.FailureCount++;
                    }
                    catch (Exception ex)
                    {
                        rowErrors.Add($"Unexpected error: {ex.Message}");
                        errors.Add(new ImportErrorDto
                        {
                            RowNumber = rowNumber,
                            EmployeeName = name,
                            ErrorMessages = rowErrors
                        });
                        result.FailureCount++;
                    }

                    rowNumber++;
                }
            }

            result.Errors = errors;
            return result;
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
                return regex.IsMatch(email);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating email format for email: {Email}", email);
                return false;
            }
        }
    }
}
