using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.Groups;
using AttendanceManagement.Data.Schedules;
using AttendanceManagement.Data.Workflows;
using AttendanceManagement.Dtos.Employees;
using AttendanceManagement.Dtos.Schedules;
using AttendanceManagement.Interfaces;
using AttendanceManagement.Permissions;
using AutoMapper.Internal.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace AttendanceManagement.Services
{
    public class EmployeeAppService :
        CrudAppService<Employee, EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>,
        IEmployeeAppService
    {
        private readonly IRepository<Group, Guid> _groupRepository;
        private readonly IRepository<Workflow, Guid> _workflow_repository;
        private readonly IRepository<Schedule, Guid> _schedule_repository;
        private readonly ILogger<EmployeeAppService> _logger;

        public EmployeeAppService(
            ILogger<EmployeeAppService> logger,
            IRepository<Employee, Guid> repository,
            IRepository<Group, Guid> groupRepository,
            IRepository<Workflow, Guid> workflowRepository,
            IRepository<Schedule, Guid> scheduleRepository)
            : base(repository)
        {
            _logger = logger;
            _groupRepository = groupRepository;
            _workflow_repository = workflowRepository;
            _schedule_repository = scheduleRepository;

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
                .Where(sa => sa.EffectiveFrom <= DateTime.Now
                    && (sa.EffectiveTo == null || sa.EffectiveTo >= DateTime.Now))
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
                    && ma.EffectiveFrom <= DateTime.Now
                    && (ma.EffectiveTo == null || ma.EffectiveTo >= DateTime.Now))
                .FirstOrDefault();

            if (primaryManager != null)
            {
                var manager = await Repository.GetAsync(primaryManager.ManagerEmployeeId);
                dto.PrimaryManagerName = manager.Name;
            }

            return dto;
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

            var employee = await Repository.GetAsync(input.EmployeeId);
            var manager = await Repository.GetAsync(input.ManagerEmployeeId);

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
                .Where(ma => ma.EffectiveFrom <= DateTime.Now
                    && (ma.EffectiveTo == null || ma.EffectiveTo >= DateTime.Now))
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
            var employee = new Employee(
                GuidGenerator.Create(),
                input.UserId,
                input.Name,
                input.Department,
                input.Sector,
                input.GroupId
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

            employee.Name = input.Name;
            employee.Department = input.Department;
            employee.Sector = input.Sector;
            employee.AssignToGroup(input.GroupId);
            employee.AssignWorkflow(input.WorkflowId);

            await Repository.UpdateAsync(employee);
            return ObjectMapper.Map<Employee, EmployeeDto>(employee);
        }
    }
}
