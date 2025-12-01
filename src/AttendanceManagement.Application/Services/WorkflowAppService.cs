using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.Workflows;
using AttendanceManagement.Dtos.Workflows;
using AttendanceManagement.Interfaces;
using AttendanceManagement.Permissions;
using AutoMapper.Internal.Mappers;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
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

namespace AttendanceManagement.Services
{
    public class WorkflowAppService :
        CrudAppService<Workflow, WorkflowDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateWorkflowDto>,
        IWorkflowAppService
    {
        private readonly IRepository<Employee, Guid> _employeeRepository;

        public WorkflowAppService(
            IRepository<Workflow, Guid> repository,
            IRepository<Employee, Guid> employeeRepository)
            : base(repository)
        {
            _employeeRepository = employeeRepository;

            GetPolicyName = AttendanceManagementPermissions.Workflows.Default;
            GetListPolicyName = AttendanceManagementPermissions.Workflows.Default;
            CreatePolicyName = AttendanceManagementPermissions.Workflows.Create;
            UpdatePolicyName = AttendanceManagementPermissions.Workflows.Edit;
            DeletePolicyName = AttendanceManagementPermissions.Workflows.Delete;
        }

        protected override async Task<IQueryable<Workflow>> CreateFilteredQueryAsync(PagedAndSortedResultRequestDto input)
        {
            return await Repository.WithDetailsAsync(w => w.WorkflowSteps);
        }

        public override async Task<WorkflowDto> GetAsync(Guid id)
        {
            var queryable = await Repository.WithDetailsAsync(w => w.WorkflowSteps);
            var workflow = await queryable.FirstOrDefaultAsync(w => w.Id == id);

            if (workflow == null)
            {
                throw new UserFriendlyException("Workflow not found");            // TODO: Use proper exception handling
            }

            var dto = ObjectMapper.Map<Workflow, WorkflowDto>(workflow);

            // Load approver names
            foreach (var step in dto.WorkflowSteps)
            {
                if (step.ApproverEmployeeId.HasValue)
                {
                    var employee = await _employeeRepository.GetAsync(step.ApproverEmployeeId.Value);
                    step.ApproverEmployeeName = employee.Name;
                }
            }

            return dto;
        }

        public async Task<List<WorkflowDto>> GetActiveWorkflowsAsync()
        {
            var queryable = await Repository.WithDetailsAsync(w => w.WorkflowSteps);
            var workflows = await queryable
                .Where(w => w.IsActive)
                .OrderBy(w => w.Name)
                .ToListAsync();

            return ObjectMapper.Map<List<Workflow>, List<WorkflowDto>>(workflows);
        }

        public async Task ActivateAsync(Guid id)
        {
            await CheckUpdatePolicyAsync();

            var workflow = await Repository.GetAsync(id);
            workflow.IsActive = true;
            await Repository.UpdateAsync(workflow);
        }

        public async Task DeactivateAsync(Guid id)
        {
            await CheckUpdatePolicyAsync();

            var workflow = await Repository.GetAsync(id);
            workflow.IsActive = false;
            await Repository.UpdateAsync(workflow);
        }

        public async Task AssignWorkflowToEmployeeAsync(Guid employeeId, Guid workflowId)
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.Workflows.ManageSteps);

            var employee = await _employeeRepository.GetAsync(employeeId);
            employee.AssignWorkflow(workflowId);
            await _employeeRepository.UpdateAsync(employee);
        }

        public override async Task<WorkflowDto> CreateAsync(CreateUpdateWorkflowDto input)
        {
            // Validate workflow steps have unique step orders
            var duplicateStepOrders = input.WorkflowSteps
                .GroupBy(s => s.StepOrder)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateStepOrders.Any())
            {
                throw new UserFriendlyException($"Duplicate step orders found: {string.Join(", ", duplicateStepOrders)}");
            }

            // Validate approver employee exists for non-Doctor steps
            foreach (var stepDto in input.WorkflowSteps)
            {
                if (stepDto.ApproverType != Enums.ApproverType.Doctor && stepDto.ApproverEmployeeId.HasValue)
                {
                    var employeeExists = await _employeeRepository.AnyAsync(e => e.Id == stepDto.ApproverEmployeeId.Value);
                    if (!employeeExists)
                    {
                        throw new UserFriendlyException($"Approver employee with ID {stepDto.ApproverEmployeeId.Value} does not exist for step {stepDto.StepOrder}.");
                    }
                }
                else if (stepDto.ApproverType != Enums.ApproverType.Doctor && !stepDto.ApproverEmployeeId.HasValue)
                {
                    throw new UserFriendlyException($"Approver employee is required for step {stepDto.StepOrder} (ApproverType: {stepDto.ApproverType}).");
                }
            }

            var workflow = new Workflow(
                GuidGenerator.Create(),
                input.Name,
                input.Description
            );

            foreach (var stepDto in input.WorkflowSteps.OrderBy(s => s.StepOrder))
            {
                var step = new WorkflowStep(
                    GuidGenerator.Create(),
                    workflow.Id,
                    stepDto.StepOrder,
                    stepDto.ApproverType,
                    stepDto.ApproverEmployeeId
                );
                workflow.WorkflowSteps.Add(step);
            }

            await Repository.InsertAsync(workflow);
            return ObjectMapper.Map<Workflow, WorkflowDto>(workflow);
        }

        public override async Task<WorkflowDto> UpdateAsync(Guid id, CreateUpdateWorkflowDto input)
        {
            var queryable = await Repository.WithDetailsAsync(w => w.WorkflowSteps);
            var workflow = await queryable.FirstOrDefaultAsync(w => w.Id == id);
            
            if (workflow == null)
            {
                throw new UserFriendlyException("Workflow not found");
            }

            // Validate workflow steps have unique step orders
            var duplicateStepOrders = input.WorkflowSteps
                .GroupBy(s => s.StepOrder)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateStepOrders.Any())
            {
                throw new UserFriendlyException($"Duplicate step orders found: {string.Join(", ", duplicateStepOrders)}");
            }

            // Validate approver employee exists for non-Doctor steps
            foreach (var stepDto in input.WorkflowSteps)
            {
                if (stepDto.ApproverType != Enums.ApproverType.Doctor && stepDto.ApproverEmployeeId.HasValue)
                {
                    var employeeExists = await _employeeRepository.AnyAsync(e => e.Id == stepDto.ApproverEmployeeId.Value);
                    if (!employeeExists)
                    {
                        throw new UserFriendlyException($"Approver employee with ID {stepDto.ApproverEmployeeId.Value} does not exist for step {stepDto.StepOrder}.");
                    }
                }
                else if (stepDto.ApproverType != Enums.ApproverType.Doctor && !stepDto.ApproverEmployeeId.HasValue)
                {
                    throw new UserFriendlyException($"Approver employee is required for step {stepDto.StepOrder} (ApproverType: {stepDto.ApproverType}).");
                }
            }

            workflow.Name = input.Name;
            workflow.Description = input.Description;

            // Clear and rebuild steps
            workflow.WorkflowSteps.Clear();
            foreach (var stepDto in input.WorkflowSteps.OrderBy(s => s.StepOrder))
            {
                var step = new WorkflowStep(
                    GuidGenerator.Create(),
                    workflow.Id,
                    stepDto.StepOrder,
                    stepDto.ApproverType,
                    stepDto.ApproverEmployeeId
                );
                workflow.WorkflowSteps.Add(step);
            }

            await Repository.UpdateAsync(workflow);
            return ObjectMapper.Map<Workflow, WorkflowDto>(workflow);
        }

        public async Task<byte[]> ExportToExcelAsync()
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.Workflows.Export);

            var queryable = await Repository.WithDetailsAsync(w => w.WorkflowSteps);
            var workflows = await queryable
                .OrderBy(w => w.Name)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Workflows");

                // Title
                worksheet.Cell(1, 1).Value = "Workflows List";
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Range(1, 1, 1, 4).Merge();

                // Headers
                var headerRow = 3;
                worksheet.Cell(headerRow, 1).Value = "Name";
                worksheet.Cell(headerRow, 2).Value = "Description";
                worksheet.Cell(headerRow, 3).Value = "Steps";
                worksheet.Cell(headerRow, 4).Value = "Status";

                // Style headers
                var headerRange = worksheet.Range(headerRow, 1, headerRow, 4);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Data
                int row = headerRow + 1;
                foreach (var workflow in workflows)
                {
                    worksheet.Cell(row, 1).Value = workflow.Name;
                    worksheet.Cell(row, 2).Value = workflow.Description ?? "-";
                    worksheet.Cell(row, 3).Value = workflow.WorkflowSteps?.Count ?? 0;
                    worksheet.Cell(row, 4).Value = workflow.IsActive ? "Active" : "Inactive";
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
