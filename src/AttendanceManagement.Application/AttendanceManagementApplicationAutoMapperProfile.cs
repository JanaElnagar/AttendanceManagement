using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.ExceptionRequests;
using AttendanceManagement.Data.Groups;
using AttendanceManagement.Data.Schedules;
using AttendanceManagement.Data.Workflows;
using AttendanceManagement.Dtos.Employees;
using AttendanceManagement.Dtos.ExceptionRequests;
using AttendanceManagement.Dtos.Group;
using AttendanceManagement.Dtos.Schedules;
using AttendanceManagement.Dtos.Workflows;
using AutoMapper;
using System.Linq;

namespace AttendanceManagement;

public class AttendanceManagementApplicationAutoMapperProfile : Profile
{
    public AttendanceManagementApplicationAutoMapperProfile()
    {

        // Employee mappings
        CreateMap<Employee, EmployeeDto>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User != null ? src.User.UserName : null))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User != null ? src.User.Email : null))
            .ForMember(dest => dest.GroupName, opt => opt.MapFrom(src => src.Group != null ? src.Group.Name : null))
            .ForMember(dest => dest.WorkflowName, opt => opt.MapFrom(src => src.Workflow != null ? src.Workflow.Name : null));

        CreateMap<Employee, EmployeeWithDetailsDto>()
            .ForMember(dest => dest.GroupName, opt => opt.MapFrom(src => src.Group != null ? src.Group.Name : null))
            .ForMember(dest => dest.WorkflowName, opt => opt.MapFrom(src => src.Workflow != null ? src.Workflow.Name : null));

        CreateMap<CreateUpdateEmployeeDto, Employee>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore());

        CreateMap<ManagerAssignment, ManagerAssignmentDto>();

        // Group mappings
        CreateMap<Group, GroupDto>()
            .ForMember(dest => dest.EmployeeCount, opt => opt.MapFrom(src => src.Employees.Count(e => e.IsActive)));

        CreateMap<Group, GroupWithEmployeesDto>()
            .ForMember(dest => dest.EmployeeCount, opt => opt.MapFrom(src => src.Employees.Count(e => e.IsActive)));

        CreateMap<CreateUpdateGroupDto, Group>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore());

        // Schedule mappings
        CreateMap<Schedule, ScheduleDto>();
        CreateMap<CreateUpdateScheduleDto, Schedule>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.ScheduleDays, opt => opt.Ignore());

        CreateMap<ScheduleDay, ScheduleDayDto>();
        CreateMap<CreateScheduleDayDto, ScheduleDay>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.ScheduleId, opt => opt.Ignore());

        CreateMap<ScheduleAssignment, ScheduleAssignmentDto>()
            .ForMember(dest => dest.ScheduleName, opt => opt.MapFrom(src => src.Schedule != null ? src.Schedule.Name : null))
            .ForMember(dest => dest.EmployeeName, opt => opt.MapFrom(src => src.Employee != null ? src.Employee.Name : null))
            .ForMember(dest => dest.GroupName, opt => opt.MapFrom(src => src.Group != null ? src.Group.Name : null));

        // Workflow mappings
        CreateMap<Workflow, WorkflowDto>();
        CreateMap<CreateUpdateWorkflowDto, Workflow>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.WorkflowSteps, opt => opt.Ignore());

        CreateMap<WorkflowStep, WorkflowStepDto>()
            .ForMember(dest => dest.ApproverEmployeeName, opt => opt.MapFrom(src => src.ApproverEmployee != null ? src.ApproverEmployee.Name : null));

        CreateMap<CreateWorkflowStepDto, WorkflowStep>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.WorkflowId, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore());

        // ExceptionRequest mappings
        CreateMap<ExceptionRequest, ExceptionRequestDto>()
            .ForMember(dest => dest.EmployeeName, opt => opt.MapFrom(src => src.Employee != null ? src.Employee.Name : null))
            .ForMember(dest => dest.WorkflowName, opt => opt.MapFrom(src => src.Workflow != null ? src.Workflow.Name : null));

        CreateMap<CreateExceptionRequestDto, ExceptionRequest>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.EmployeeId, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.WorkflowId, opt => opt.Ignore())
            .ForMember(dest => dest.CurrentStepOrder, opt => opt.Ignore());

        CreateMap<ExceptionRequestApprovalHistory, ExceptionRequestApprovalHistoryDto>()
            .ForMember(dest => dest.ApproverEmployeeName, opt => opt.MapFrom(src => src.ApproverEmployee != null ? src.ApproverEmployee.Name : null));

        CreateMap<ExceptionRequestAttachment, ExceptionRequestAttachmentDto>();
    }
}
