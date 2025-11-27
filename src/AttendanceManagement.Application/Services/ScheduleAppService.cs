using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.Groups;
using AttendanceManagement.Data.Schedules;
using AttendanceManagement.Dtos.Schedules;
using AttendanceManagement.Interfaces;
using AttendanceManagement.Permissions;
using AutoMapper.Internal.Mappers;
using Microsoft.EntityFrameworkCore;
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
    public class ScheduleAppService :
         CrudAppService<Schedule, ScheduleDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateScheduleDto>,
         IScheduleAppService
    {
        private readonly IRepository<Employee, Guid> _employeeRepository;
        private readonly IRepository<Group, Guid> _groupRepository;

        public ScheduleAppService(
            IRepository<Schedule, Guid> repository,
            IRepository<Employee, Guid> employeeRepository,
            IRepository<Group, Guid> groupRepository)
            : base(repository)
        {
            _employeeRepository = employeeRepository;
            _groupRepository = groupRepository;

            GetPolicyName = AttendanceManagementPermissions.Schedules.Default;
            GetListPolicyName = AttendanceManagementPermissions.Schedules.Default;
            CreatePolicyName = AttendanceManagementPermissions.Schedules.Create;
            UpdatePolicyName = AttendanceManagementPermissions.Schedules.Edit;
            DeletePolicyName = AttendanceManagementPermissions.Schedules.Delete;
        }

        protected override async Task<IQueryable<Schedule>> CreateFilteredQueryAsync(PagedAndSortedResultRequestDto input)
        {
            return (await Repository.WithDetailsAsync(s => s.ScheduleDays));
        }

        public override async Task<ScheduleDto> GetAsync(Guid id)
        {
            var queryable = await Repository.WithDetailsAsync(s => s.ScheduleDays);
            var schedule = await queryable.FirstOrDefaultAsync(s => s.Id == id);

            return ObjectMapper.Map<Schedule, ScheduleDto>(schedule);
        }

        public async Task<List<ScheduleDto>> GetActiveSchedulesAsync()
        {
            var queryable = await Repository.WithDetailsAsync(s => s.ScheduleDays);
            var schedules = await queryable
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            return ObjectMapper.Map<List<Schedule>, List<ScheduleDto>>(schedules);
        }

        public async Task ActivateAsync(Guid id)
        {
            await CheckUpdatePolicyAsync();

            var schedule = await Repository.GetAsync(id);
            schedule.IsActive = true;
            await Repository.UpdateAsync(schedule);
        }

        public async Task DeactivateAsync(Guid id)
        {
            await CheckUpdatePolicyAsync();

            var schedule = await Repository.GetAsync(id);
            schedule.IsActive = false;
            await Repository.UpdateAsync(schedule);
        }

        public async Task AssignScheduleAsync(AssignScheduleDto input)
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.Schedules.Assign);

            var schedule = await Repository.GetAsync(input.ScheduleId);

            var assignment = new ScheduleAssignment(
                GuidGenerator.Create(),
                input.ScheduleId,
                input.EffectiveFrom,
                input.EmployeeId,
                input.GroupId
            )
            {
                SeatNumber = input.SeatNumber,
                FloorNumber = input.FloorNumber,
                EffectiveTo = input.EffectiveTo
            };

            schedule.ScheduleAssignments.Add(assignment);

            if (input.EmployeeId.HasValue)
            {
                var employee = await _employeeRepository.GetAsync(input.EmployeeId.Value);
                employee.ScheduleAssignments.Add(assignment);
                await _employeeRepository.UpdateAsync(employee);
            }

            if (input.GroupId.HasValue)
            {
                var group = await _groupRepository.GetAsync(input.GroupId.Value);
                group.ScheduleAssignments.Add(assignment);
                await _groupRepository.UpdateAsync(group);
            }

            await Repository.UpdateAsync(schedule);
        }

        public async Task<ScheduleAssignmentDto> GetEmployeeCurrentScheduleAsync(Guid employeeId)
        {
            var employee = await _employeeRepository.GetAsync(employeeId);

            var currentAssignment = employee.ScheduleAssignments
                .Where(sa => sa.EffectiveFrom <= DateTime.Now
                    && (sa.EffectiveTo == null || sa.EffectiveTo >= DateTime.Now))
                .FirstOrDefault();

            if (currentAssignment == null)
            {
                return null;
            }

            var schedule = await Repository.GetAsync(currentAssignment.ScheduleId);

            return new ScheduleAssignmentDto
            {
                Id = currentAssignment.Id,
                ScheduleId = currentAssignment.ScheduleId,
                ScheduleName = schedule.Name,
                EmployeeId = currentAssignment.EmployeeId,
                EmployeeName = employee.Name,
                GroupId = currentAssignment.GroupId,
                EffectiveFrom = currentAssignment.EffectiveFrom,
                EffectiveTo = currentAssignment.EffectiveTo,
                SeatNumber = currentAssignment.SeatNumber,
                FloorNumber = currentAssignment.FloorNumber
            };
        }

        // TODO: Implement export functionality
        public async Task<byte[]> ExportEmployeeScheduleAsync(Guid employeeId)
        {
            throw new NotImplementedException();
        }

        public override async Task<ScheduleDto> CreateAsync(CreateUpdateScheduleDto input)
        {
            var schedule = new Schedule(
                GuidGenerator.Create(),
                input.Name,
                input.Description
            );

            foreach (var dayDto in input.ScheduleDays)
            {
                var scheduleDay = new ScheduleDay(
                    GuidGenerator.Create(),
                    schedule.Id,
                    dayDto.DayOfWeek,
                    dayDto.IsOnSite
                );
                schedule.ScheduleDays.Add(scheduleDay);
            }

            await Repository.InsertAsync(schedule);
            return ObjectMapper.Map<Schedule, ScheduleDto>(schedule);
        }

        public override async Task<ScheduleDto> UpdateAsync(Guid id, CreateUpdateScheduleDto input)
        {
            var queryable = await Repository.WithDetailsAsync(s => s.ScheduleDays);
            var schedule = await queryable.FirstOrDefaultAsync(s => s.Id == id);

            schedule.Name = input.Name;
            schedule.Description = input.Description;

            // Clear existing days and add new ones
            schedule.ScheduleDays.Clear();
            foreach (var dayDto in input.ScheduleDays)
            {
                var scheduleDay = new ScheduleDay(
                    GuidGenerator.Create(),
                    schedule.Id,
                    dayDto.DayOfWeek,
                    dayDto.IsOnSite
                );
                schedule.ScheduleDays.Add(scheduleDay);
            }

            await Repository.UpdateAsync(schedule);
            return ObjectMapper.Map<Schedule, ScheduleDto>(schedule);
        }
    }
}
