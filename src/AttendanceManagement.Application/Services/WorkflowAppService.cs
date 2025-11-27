using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.Workflows;
using AttendanceManagement.Dtos.Workflows;
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
                throw new UserFriendlyException("Workflow not found");                      // TODO: Use proper exception handling
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
    }
}
