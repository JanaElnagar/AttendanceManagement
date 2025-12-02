using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.ExceptionRequests;
using AttendanceManagement.Data.Schedules;
using AttendanceManagement.Data.Workflows;
using AttendanceManagement.Dtos.ExceptionRequests;
using AttendanceManagement.Enums;
using AttendanceManagement.Interfaces;
using AttendanceManagement.Permissions;
using AutoMapper.Internal.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
using Volo.Abp.Authorization;
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
        private readonly IConfiguration _configuration;

        public ExceptionRequestAppService(
            IExceptionRequestRepository exceptionRequestRepository,
            IScheduleRepository scheduleAssignmentRepository,
            IRepository<Employee, Guid> employeeRepository,
            IRepository<Workflow, Guid> workflowRepository,
            IRepository<Schedule, Guid> scheduleRepository,
            ICurrentUser currentUser,
            ILogger<ExceptionRequestAppService> logger,
            IConfiguration configuration
            )
        {
            _exceptionRequestRepository = exceptionRequestRepository;
            _scheduleAssignmentRepository = scheduleAssignmentRepository;
            _scheduleRepository = scheduleRepository;
            _employeeRepository = employeeRepository;
            _workflowRepository = workflowRepository;
            _currentUser = currentUser;
            _logger = logger;
            _configuration = configuration;
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
            // Only show requests that are at the exact step where the current user is assigned as approver
            var myPendingRequests = new List<ExceptionRequest>();

            foreach (var er in allPendingRequests)
            {
                // Skip if workflow or steps are null
                if (er.Workflow == null || er.Workflow.WorkflowSteps == null)
                {
                    _logger.LogDebug($"Request {er.Id}: Skipping - Workflow or WorkflowSteps is null");
                    continue;
                }

                // Find the step that the request is currently at
                var currentStep = er.Workflow.WorkflowSteps
                    .FirstOrDefault(ws => ws != null && ws.StepOrder == er.CurrentStepOrder);
                
                if (currentStep == null)
                {
                    _logger.LogDebug($"Request {er.Id}: No step found for CurrentStepOrder {er.CurrentStepOrder}");
                    continue;
                }

                _logger.LogDebug($"Request {er.Id}: CurrentStepOrder={er.CurrentStepOrder}, StepApproverType={currentStep.ApproverType}, StepApproverEmployeeId={currentStep.ApproverEmployeeId}");

                // Check if step requires doctor approval
                if (currentStep.ApproverType == ApproverType.Doctor)
                {
                    // Doctors should only see sick leave requests at doctor steps
                    if (canApproveAsDoctor && er.Type == ExceptionRequestType.Sick)
                    {
                        _logger.LogDebug($"Request {er.Id}: Doctor step - INCLUDED");
                        myPendingRequests.Add(er);
                    }
                    else
                    {
                        _logger.LogDebug($"Request {er.Id}: Doctor step - EXCLUDED (canApproveAsDoctor={canApproveAsDoctor}, Type={er.Type})");
                    }
                    continue;
                }

                // For Manager/HR, ensure:
                // 1. The request is at the exact step where this employee is assigned
                // 2. The current employee matches the approver for this specific step
                if (currentEmployee == null)
                {
                    _logger.LogDebug($"Request {er.Id}: EXCLUDED - CurrentEmployee is null");
                    continue;
                }

                // CRITICAL: Only include if the current step's approver matches the current employee
                // AND the request is at this exact step (already verified by finding currentStep)
                if (!currentStep.ApproverEmployeeId.HasValue)
                {
                    _logger.LogDebug($"Request {er.Id}: EXCLUDED - Step {currentStep.StepOrder} has no ApproverEmployeeId");
                    continue;
                }

                if (currentStep.ApproverEmployeeId.Value != currentEmployee.Id)
                {
                    _logger.LogDebug($"Request {er.Id}: EXCLUDED - Step {currentStep.StepOrder} ApproverEmployeeId ({currentStep.ApproverEmployeeId.Value}) != CurrentEmployeeId ({currentEmployee.Id})");
                    continue;
                }

                // Double-check: Ensure the request is at this exact step
                if (er.CurrentStepOrder != currentStep.StepOrder)
                {
                    _logger.LogDebug($"Request {er.Id}: EXCLUDED - CurrentStepOrder ({er.CurrentStepOrder}) != StepOrder ({currentStep.StepOrder})");
                    continue;
                }

                // All checks passed - this request is at the exact step where this manager is assigned
                _logger.LogDebug($"Request {er.Id}: INCLUDED - Manager/HR match at step {currentStep.StepOrder}");
                myPendingRequests.Add(er);
            }

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
            var relativePath = $"attachments/{exceptionRequestId}/{fileName}";

            // Determine the base path for file storage
            var currentDir = Directory.GetCurrentDirectory();
            string basePath = null;
            
            // Try to find wwwroot directory
            if (Directory.Exists(Path.Combine(currentDir, "wwwroot")))
            {
                basePath = Path.Combine(currentDir, "wwwroot");
            }
            else
            {
                // Try one level up (for HttpApi.Host)
                var parentDir = Directory.GetParent(currentDir)?.FullName;
                if (parentDir != null)
                {
                    var hostPath = Path.Combine(parentDir, "AttendanceManagement.HttpApi.Host", "wwwroot");
                    if (Directory.Exists(hostPath))
                    {
                        basePath = hostPath;
                    }
                }
            }

            if (basePath == null)
            {
                // Fallback: create wwwroot in current directory
                basePath = Path.Combine(currentDir, "wwwroot");
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }
            }

            // Create the attachments directory structure
            var fullPath = Path.Combine(basePath, relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Save the file
            using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                await file.GetStream().CopyToAsync(fileStream);
            }

            var attachment = new ExceptionRequestAttachment(
                GuidGenerator.Create(),
                exceptionRequestId,
                fileName,
                relativePath, // Store relative path
                file.ContentType,
                attachmentType
            );

            request.Attachments.Add(attachment);
            await _exceptionRequestRepository.UpdateAsync(request);

            return attachment.Id;
        }

        public async Task<byte[]> DownloadAttachmentAsync(Guid attachmentId)
        {
            // Get the exception request with attachments
            var queryable = await _exceptionRequestRepository.GetQueryableAsync();
            var request = await queryable
                .Include(r => r.Attachments)
                .FirstOrDefaultAsync(r => r.Attachments.Any(a => a.Id == attachmentId));

            if (request == null)
            {
                throw new UserFriendlyException("Exception request not found.");
            }

            var attachment = request.Attachments.FirstOrDefault(a => a.Id == attachmentId);
            if (attachment == null)
            {
                throw new UserFriendlyException("Attachment not found.");
            }

            // Check if user has permission to view this attachment
            // User can view if they are the requester, or if they have approve permission
            var canView = false;
            var employee = await _employeeRepository.GetAsync(request.EmployeeId);
            if (_currentUser.Id.HasValue && employee.UserId == _currentUser.Id.Value)
            {
                canView = true;
            }

            if (!canView)
            {
                canView = await AuthorizationService.IsGrantedAsync(AttendanceManagementPermissions.ExceptionRequests.Approve);
            }

            if (!canView)
            {
                throw new AbpAuthorizationException("You do not have permission to view this attachment.");
            }

            // Use the stored file path from the attachment
            // The path is stored as relative: "attachments/{requestId}/{fileName}"
            var relativePath = attachment.FilePath;
            var currentDir = Directory.GetCurrentDirectory();
            string filePath = null;

            // Try multiple possible locations
            var pathsToTry = new List<string>
            {
                Path.Combine(currentDir, "wwwroot", relativePath),
                Path.Combine(currentDir, relativePath)
            };

            // Try one level up (for HttpApi.Host)
            var parentDir = Directory.GetParent(currentDir)?.FullName;
            if (parentDir != null)
            {
                pathsToTry.Add(Path.Combine(parentDir, "AttendanceManagement.HttpApi.Host", "wwwroot", relativePath));
                pathsToTry.Add(Path.Combine(parentDir, "AttendanceManagement.Blazor", "wwwroot", relativePath));
            }

            // Try configuration path if set
            var configPath = _configuration["App:AttachmentsPath"];
            if (!string.IsNullOrEmpty(configPath))
            {
                pathsToTry.Insert(0, Path.Combine(configPath, relativePath));
            }

            foreach (var path in pathsToTry)
            {
                if (File.Exists(path))
                {
                    filePath = path;
                    break;
                }
            }

            if (filePath == null || !File.Exists(filePath))
            {
                _logger.LogWarning($"Attachment file not found. Tried paths: {string.Join(", ", pathsToTry)}. Attachment FilePath: {attachment.FilePath}");
                throw new UserFriendlyException($"File not found on server. Path: {attachment.FilePath}");
            }

            return await File.ReadAllBytesAsync(filePath);
        }
    }
}
