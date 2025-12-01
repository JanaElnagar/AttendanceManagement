using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.ExceptionRequests;
using AttendanceManagement.Data.Schedules;
using AttendanceManagement.Data.Workflows;
using AttendanceManagement.Dtos.ExceptionRequests;
using AttendanceManagement.Enums;
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
using Volo.Abp.Content;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Users;

namespace AttendanceManagement.Services
{
    public class ExceptionRequestAppService : ApplicationService, IExceptionRequestAppService
    {
        private readonly IRepository<Employee, Guid> _employeeRepository;
        private readonly IRepository<Workflow, Guid> _workflowRepository;
        private readonly IRepository<Schedule, Guid> _scheduleRepository;
        private readonly IExceptionRequestRepository _exceptionRequestRepository;
        private readonly IScheduleRepository _scheduleAssignmentRepository;
        private readonly ICurrentUser _currentUser;
        private readonly ILogger<ExceptionRequestAppService> _logger;

        public ExceptionRequestAppService(
            IExceptionRequestRepository exceptionRequestRepository,
            IScheduleRepository scheduleAssignmentRepository,
            IRepository<Employee, Guid> employeeRepository,
            IRepository<Workflow, Guid> workflowRepository,
            IRepository<Schedule, Guid> scheduleRepository,
            ICurrentUser currentUser,
            ILogger<ExceptionRequestAppService> logger
            )
        {
            _exceptionRequestRepository = exceptionRequestRepository;
            _scheduleAssignmentRepository = scheduleAssignmentRepository;
            _scheduleRepository = scheduleRepository;
            _employeeRepository = employeeRepository;
            _workflowRepository = workflowRepository;
            _currentUser = currentUser;
            _logger = logger;
        }

        public async Task<ExceptionRequestDto> GetAsync(Guid id)
        {
            var queryable = await _exceptionRequestRepository.WithDetailsAsync(
                er => er.Employee,
                er => er.Workflow,
                er => er.Workflow.WorkflowSteps,
                er => er.ApprovalHistories,
                er => er.Attachments);

            var request = await queryable.FirstOrDefaultAsync(er => er.Id == id);
            if (request == null) {
                throw new UserFriendlyException("Exception request not found.");
            }

            // Load ApproverEmployee for each approval history
            foreach (var history in request.ApprovalHistories)
            {
                if (history.ApproverEmployeeId.HasValue && history.ApproverEmployee == null)
                {
                    history.ApproverEmployee = await _employeeRepository.GetAsync(history.ApproverEmployeeId.Value);
                }
            }

            var dto = ObjectMapper.Map<ExceptionRequest, ExceptionRequestDto>(request);

            // Populate ApproverEmployeeName for doctor approvals (where ApproverEmployeeId is null)
            foreach (var history in dto.ApprovalHistories)
            {
                if (!history.ApproverEmployeeId.HasValue && string.IsNullOrEmpty(history.ApproverEmployeeName))
                {
                    // This is a doctor approval - set a default name
                    // In a real scenario, you might want to store the doctor's user name separately
                    history.ApproverEmployeeName = "Doctor";
                }
            }

            return dto;
        }

        public async Task<PagedResultDto<ExceptionRequestDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.ExceptionRequests.ViewAll);

            var queryable = await _exceptionRequestRepository.WithDetailsAsync(
                er => er.Employee,
                er => er.Workflow);

            var totalCount = await queryable.CountAsync();

            var requests = await queryable
                .OrderByDescending(er => er.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();

            return new PagedResultDto<ExceptionRequestDto>(
                totalCount,
                ObjectMapper.Map<List<ExceptionRequest>, List<ExceptionRequestDto>>(requests)
            );
        }

        public async Task<ExceptionRequestDto> CreateAsync(CreateExceptionRequestDto input)
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.ExceptionRequests.Create);

            // Get current employee
            var employee = await _employeeRepository
                .FirstOrDefaultAsync(e => e.UserId == _currentUser.Id.Value);

            if (employee == null)
            {
                throw new UserFriendlyException("Employee record not found for current user.");
            }

            if (!employee.WorkflowId.HasValue)
            {
                throw new UserFriendlyException("No workflow assigned to this employee.");
            }

            // Validate exception date is for a scheduled on-site day
            var currentScheduleAssignment = await _scheduleAssignmentRepository.GetScheduleAssignmentByEmployeeId(employee.Id);

            if (currentScheduleAssignment == null)
            {
                throw new UserFriendlyException("You don't have an active schedule.");
            }

            // Load schedule with days
            var scheduleQueryable = await _scheduleRepository.WithDetailsAsync(s => s.ScheduleDays);
            var schedule = await scheduleQueryable
                .FirstOrDefaultAsync(s => s.Id == currentScheduleAssignment.ScheduleId);

            if (schedule == null)
            {
                throw new UserFriendlyException("Schedule not found.");
            }

            var scheduleDay = schedule.ScheduleDays?
                .FirstOrDefault(sd => sd.DayOfWeek == input.ExceptionDate.DayOfWeek);

            if (scheduleDay == null || !scheduleDay.IsOnSite)
            {
                throw new UserFriendlyException(
                    "Exception requests can only be submitted for scheduled on-site days.");
            }

            // Load workflow to determine initial step
            var workflowQueryable = await _workflowRepository.WithDetailsAsync(w => w.WorkflowSteps);
            var workflow = await workflowQueryable
                .FirstOrDefaultAsync(w => w.Id == employee.WorkflowId.Value);

            if (workflow == null)
            {
                throw new UserFriendlyException("Workflow not found.");
            }

            // Determine initial step based on request type
            int initialStepOrder = 1;
            if (input.Type == ExceptionRequestType.Sick)
            {
                // For sick leave, find the first Doctor step
                var firstDoctorStep = workflow.WorkflowSteps
                    .OrderBy(ws => ws.StepOrder)
                    .FirstOrDefault(ws => ws.ApproverType == ApproverType.Doctor);
                
                if (firstDoctorStep != null)
                {
                    initialStepOrder = firstDoctorStep.StepOrder;
                }
                else
                {
                    // If no Doctor step exists, start at step 1 (fallback)
                    _logger.LogWarning($"Workflow {workflow.Id} does not have a Doctor step. Sick leave request will start at step 1.");
                    initialStepOrder = 1;
                }
            }
            else
            {
                // For non-sick, skip Doctor steps and start at first Manager/HR step
                var firstNonDoctorStep = workflow.WorkflowSteps
                    .OrderBy(ws => ws.StepOrder)
                    .FirstOrDefault(ws => ws.ApproverType != ApproverType.Doctor);
                
                if (firstNonDoctorStep != null)
                {
                    initialStepOrder = firstNonDoctorStep.StepOrder;
                }
                else
                {
                    // If all steps are Doctor (shouldn't happen for non-sick), start at step 1
                    initialStepOrder = 1;
                }
            }

            var exceptionRequest = new ExceptionRequest(
                GuidGenerator.Create(),
                employee.Id,
                input.ExceptionDate,
                input.Reason,
                input.Type,
                employee.WorkflowId.Value
            );

            // Set the initial step order
            exceptionRequest.CurrentStepOrder = initialStepOrder;

            await _exceptionRequestRepository.InsertAsync(exceptionRequest);

            return ObjectMapper.Map<ExceptionRequest, ExceptionRequestDto>(exceptionRequest);
        }

        public async Task<ExceptionRequestDto> ApproveOrRejectAsync(ApproveRejectExceptionRequestDto input)
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.ExceptionRequests.Approve);

            var queryable = await _exceptionRequestRepository.WithDetailsAsync(
                er => er.Workflow.WorkflowSteps,
                er => er.ApprovalHistories);

            var request = await queryable.FirstOrDefaultAsync(er => er.Id == input.ExceptionRequestId);
            if (request == null) {
                throw new UserFriendlyException("Exception request not found.");
            }

            if (request.Status != ExceptionRequestStatus.Pending)
            {
                throw new UserFriendlyException("This request has already been processed.");
            }

            // Get current workflow step
            var currentStep = request.Workflow.WorkflowSteps
                .FirstOrDefault(ws => ws.StepOrder == request.CurrentStepOrder);

            if (currentStep == null)
            {
                throw new UserFriendlyException("Invalid workflow step.");
            }

            // Verify approver based on approver type
            bool isAuthorized = false;
            Guid? approverEmployeeId = null;

            if (currentStep.ApproverType == ApproverType.Doctor)
            {
                // For doctor approval, check if user has ApproveAsDoctor permission
                // Doctors may not have employee records
                try
                {
                    await CheckPolicyAsync(AttendanceManagementPermissions.ExceptionRequests.ApproveAsDoctor);
                    isAuthorized = true;
                    // For doctor, we don't need ApproverEmployeeId - any doctor with permission can approve
                }
                catch
                {
                    isAuthorized = false;
                }
            }
            else
            {
                // For Manager/HR, check if current user is the assigned employee
                var currentEmployee = await _employeeRepository
                    .FirstOrDefaultAsync(e => e.UserId == _currentUser.Id.Value);

                if (currentEmployee == null)
                {
                    throw new UserFriendlyException("Employee record not found for current user.");
                }

                if (currentStep.ApproverEmployeeId.HasValue && 
                    currentStep.ApproverEmployeeId.Value == currentEmployee.Id)
                {
                    isAuthorized = true;
                    approverEmployeeId = currentEmployee.Id;
                }
            }

            if (!isAuthorized)
            {
                throw new UserFriendlyException("You are not authorized to approve this request at this step.");
            }

            // Get approver employee ID (for doctor, may be null)
            if (!approverEmployeeId.HasValue && currentStep.ApproverType == ApproverType.Doctor)
            {
                // For doctors, try to find employee record, but it's optional
                var doctorEmployee = await _employeeRepository
                    .FirstOrDefaultAsync(e => e.UserId == _currentUser.Id.Value);
                approverEmployeeId = doctorEmployee?.Id;
            }

            // Create approval history
            var history = new ExceptionRequestApprovalHistory(
                GuidGenerator.Create(),
                request.Id,
                currentStep.Id,
                approverEmployeeId, // Can be null for external doctors
                currentStep.StepOrder,
                input.Action,
                input.Notes
            );

            request.ApprovalHistories.Add(history);

            if (input.Action == ApprovalAction.Approved)
            {
                // Check if there are more steps
                var nextStep = request.Workflow.WorkflowSteps
                    .FirstOrDefault(ws => ws.StepOrder == request.CurrentStepOrder + 1);

                if (nextStep != null)
                {
                    request.Approve(currentStep.StepOrder);
                }
                else
                {
                    request.FinalApprove();
                }
            }
            else if (input.Action == ApprovalAction.Rejected)
            {
                request.Reject();
            }

            await _exceptionRequestRepository.UpdateAsync(request);

            return ObjectMapper.Map<ExceptionRequest, ExceptionRequestDto>(request);
        }

        public async Task<List<ExceptionRequestDto>> GetMyRequestsAsync()
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.ExceptionRequests.ViewOwn);

            var employee = await _employeeRepository
                .FirstOrDefaultAsync(e => e.UserId == _currentUser.Id.Value);

            if (employee == null)
            {
                _logger.LogDebug("employee with id: " + _currentUser.Id + "NOT Found");
                throw new UserFriendlyException("Employee record not found for current user.");
            }
            else
            {
                _logger.LogDebug("employee with name: " + employee.Id + "Found");
            }
              

            var requests = await _exceptionRequestRepository.GetExceptionRequestsByEmployeeId(employee.Id);
            _logger.LogDebug("requests: " + requests);
            return ObjectMapper.Map<List<ExceptionRequest>, List<ExceptionRequestDto>>(requests);
        }

        public async Task<List<ExceptionRequestDto>> GetPendingApprovalsAsync()
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.ExceptionRequests.Approve);

            var queryable = await _exceptionRequestRepository.WithDetailsAsync(
                er => er.Employee,
                er => er.Workflow.WorkflowSteps);

            var allPendingRequests = await queryable
                .Where(er => er.Status == ExceptionRequestStatus.Pending)
                .ToListAsync();

            // Check if user has ViewAll permission (admin) - return all pending requests
            bool canViewAll = false;
            try
            {
                await CheckPolicyAsync(AttendanceManagementPermissions.ExceptionRequests.ViewAll);
                canViewAll = true;
            }
            catch
            {
                // User doesn't have ViewAll permission
                canViewAll = false;
            }

            if (canViewAll)
            {
                return ObjectMapper.Map<List<ExceptionRequest>, List<ExceptionRequestDto>>(allPendingRequests);
            }

            // For non-admin users, filter by approver
            var currentEmployee = await _employeeRepository
                .FirstOrDefaultAsync(e => e.UserId == _currentUser.Id.Value);

            // Check if user has doctor approval permission
            bool canApproveAsDoctor = false;
            try
            {
                await CheckPolicyAsync(AttendanceManagementPermissions.ExceptionRequests.ApproveAsDoctor);
                canApproveAsDoctor = true;
                _logger.LogDebug($"User {_currentUser.Id} has ApproveAsDoctor permission");
            }
            catch
            {
                canApproveAsDoctor = false;
                _logger.LogDebug($"User {_currentUser.Id} does NOT have ApproveAsDoctor permission");
            }

            // Filter requests where current user can approve
            var myPendingRequests = allPendingRequests
                .Where(er => er.Workflow != null 
                    && er.Workflow.WorkflowSteps != null)
                .Where(er =>
                {
                    var currentStep = er.Workflow.WorkflowSteps
                        .FirstOrDefault(ws => ws != null && ws.StepOrder == er.CurrentStepOrder);
                    
                    if (currentStep == null)
                    {
                        _logger.LogDebug($"Request {er.Id}: No step found for CurrentStepOrder {er.CurrentStepOrder}");
                        return false;
                    }

                    // Check if step requires doctor approval
                    if (currentStep.ApproverType == ApproverType.Doctor)
                    {
                        // Doctors should only see sick leave requests
                        bool canSee = canApproveAsDoctor && er.Type == ExceptionRequestType.Sick;
                        _logger.LogDebug($"Request {er.Id}: Doctor step check - canApproveAsDoctor={canApproveAsDoctor}, Type={er.Type}, canSee={canSee}");
                        return canSee;
                    }

                    // For Manager/HR, check if current employee is assigned
                    if (currentEmployee != null && 
                        currentStep.ApproverEmployeeId.HasValue &&
                        currentStep.ApproverEmployeeId.Value == currentEmployee.Id)
                    {
                        return true;
                    }

                    return false;
                })
                .ToList();

            _logger.LogDebug($"User {_currentUser.Id} can see {myPendingRequests.Count} pending requests");

            return ObjectMapper.Map<List<ExceptionRequest>, List<ExceptionRequestDto>>(myPendingRequests);
        }

        public async Task CancelAsync(Guid id)
        {
            await CheckPolicyAsync(AttendanceManagementPermissions.ExceptionRequests.Delete);

            var request = await _exceptionRequestRepository.GetAsync(id);

            if (request.Status != ExceptionRequestStatus.Pending)
            {
                throw new UserFriendlyException("Only pending requests can be cancelled.");
            }

            request.Status = ExceptionRequestStatus.Cancelled;
            await _exceptionRequestRepository.UpdateAsync(request);
        }

        public async Task<Guid> UploadAttachmentAsync(
            Guid exceptionRequestId,
            IRemoteStreamContent file,
            AttachmentType attachmentType)
        {
            var request = await _exceptionRequestRepository.GetAsync(exceptionRequestId);

            // Ensure fileName is not null
            var fileName = file.FileName ?? throw new UserFriendlyException("File name cannot be null.");
            var filePath = $"attachments/{exceptionRequestId}/{fileName}";

            // TODO: Implement actual file saving logic

            var attachment = new ExceptionRequestAttachment(
                GuidGenerator.Create(),
                exceptionRequestId,
                fileName,
                filePath,
                file.ContentType,
                attachmentType
            );

            request.Attachments.Add(attachment);
            await _exceptionRequestRepository.UpdateAsync(request);

            return attachment.Id;
        }
    }
}
