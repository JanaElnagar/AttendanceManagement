using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.Groups;
using AttendanceManagement.Dtos.Employees;
using AttendanceManagement.Dtos.Group;
using AttendanceManagement.Interfaces;
using AttendanceManagement.Permissions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using AutoMapper.Internal.Mappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp;

namespace AttendanceManagement.Services
{
    public class GroupAppService :
        CrudAppService<Group, GroupDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateGroupDto>,
        IGroupAppService
    {
        public GroupAppService(IRepository<Group, Guid> repository) : base(repository)
        {
            GetPolicyName = AttendanceManagementPermissions.Groups.Default;
            GetListPolicyName = AttendanceManagementPermissions.Groups.Default;
            CreatePolicyName = AttendanceManagementPermissions.Groups.Create;
            UpdatePolicyName = AttendanceManagementPermissions.Groups.Edit;
            DeletePolicyName = AttendanceManagementPermissions.Groups.Delete;
        }

        protected override async Task<IQueryable<Group>> CreateFilteredQueryAsync(PagedAndSortedResultRequestDto input)
        {
            return await Repository.WithDetailsAsync(g => g.Employees);
        }

        public async Task<GroupWithEmployeesDto> GetWithEmployeesAsync(Guid id)
        {
            var queryable = await Repository.WithDetailsAsync(g => g.Employees);
            var group = await queryable.FirstOrDefaultAsync(g => g.Id == id);

            var dto = ObjectMapper.Map<Group, GroupWithEmployeesDto>(group);
            dto.Employees = ObjectMapper.Map<List<Employee>, List<EmployeeDto>>(
                group.Employees.Where(e => e.IsActive).ToList());

            return dto;
        }

        public async Task<List<GroupDto>> GetActiveGroupsAsync()
        {
            var queryable = await Repository.WithDetailsAsync(g => g.Employees);
            var groups = await queryable
                .Where(g => g.IsActive)
                .OrderBy(g => g.Name)
                .ToListAsync();

            return ObjectMapper.Map<List<Group>, List<GroupDto>>(groups);
        }

        public async Task ActivateAsync(Guid id)
        {
            await CheckUpdatePolicyAsync();

            var group = await Repository.GetAsync(id);
            group.IsActive = true;
            await Repository.UpdateAsync(group);
        }

        public async Task DeactivateAsync(Guid id)
        {
            await CheckUpdatePolicyAsync();

            var group = await Repository.GetAsync(id);
            group.IsActive = false;
            await Repository.UpdateAsync(group);
        }

        public override async Task<GroupDto> CreateAsync(CreateUpdateGroupDto input)
        {
            // Validate name uniqueness
            var existingGroup = await Repository.FirstOrDefaultAsync(g => g.Name == input.Name);
            if (existingGroup != null)
            {
                throw new UserFriendlyException("A group with this name already exists.");
            }

            var group = new Group(
                GuidGenerator.Create(),
                input.Name,
                input.Description
            );

            await Repository.InsertAsync(group);
            return ObjectMapper.Map<Group, GroupDto>(group);
        }

        public override async Task<GroupDto> UpdateAsync(Guid id, CreateUpdateGroupDto input)
        {
            var group = await Repository.GetAsync(id);

            // Validate name uniqueness (excluding current group)
            var existingGroup = await Repository.FirstOrDefaultAsync(g => g.Name == input.Name && g.Id != id);
            if (existingGroup != null)
            {
                throw new UserFriendlyException("A group with this name already exists.");
            }

            group.Name = input.Name;
            group.Description = input.Description;

            await Repository.UpdateAsync(group);
            return ObjectMapper.Map<Group, GroupDto>(group);
        }

        public async Task<byte[]> ExportToExcelAsync()
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.Groups.Export);

            var queryable = await Repository.WithDetailsAsync(g => g.Employees);
            var groups = await queryable
                .OrderBy(g => g.Name)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Groups");

                // Title
                worksheet.Cell(1, 1).Value = "Groups List";
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Range(1, 1, 1, 4).Merge();

                // Headers
                var headerRow = 3;
                worksheet.Cell(headerRow, 1).Value = "Name";
                worksheet.Cell(headerRow, 2).Value = "Description";
                worksheet.Cell(headerRow, 3).Value = "Employee Count";
                worksheet.Cell(headerRow, 4).Value = "Status";

                // Style headers
                var headerRange = worksheet.Range(headerRow, 1, headerRow, 4);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Data
                int row = headerRow + 1;
                foreach (var group in groups)
                {
                    worksheet.Cell(row, 1).Value = group.Name;
                    worksheet.Cell(row, 2).Value = group.Description ?? "-";
                    worksheet.Cell(row, 3).Value = group.Employees?.Count(e => e.IsActive) ?? 0;
                    worksheet.Cell(row, 4).Value = group.IsActive ? "Active" : "Inactive";
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
    }
}
