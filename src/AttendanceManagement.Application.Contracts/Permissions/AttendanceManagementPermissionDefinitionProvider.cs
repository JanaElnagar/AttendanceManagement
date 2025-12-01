using AttendanceManagement.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace AttendanceManagement.Permissions
{

    public class AttendanceManagementPermissionDefinitionProvider : PermissionDefinitionProvider
    {
        public override void Define(IPermissionDefinitionContext context)
        {
            //Define your own permissions here. Example:
            //myGroup.AddPermission(AttendanceManagementPermissions.MyPermission1, L("Permission:MyPermission1"));

            var attendanceGroup = context.AddGroup(
                    AttendanceManagementPermissions.GroupName,
                    L("Permission:AttendanceManagement"));

            // Dashboard permissions
            var dashboardPermission = attendanceGroup.AddPermission(
                AttendanceManagementPermissions.Dashboard.DashboardGroup,
                L("Permission:Dashboard"));

            // Employee permissions
            var employeePermission = attendanceGroup.AddPermission(
                AttendanceManagementPermissions.Employees.Default,
                L("Permission:Employees"));
            employeePermission.AddChild(
                AttendanceManagementPermissions.Employees.Create,
                L("Permission:Create"));
            employeePermission.AddChild(
                AttendanceManagementPermissions.Employees.Edit,
                L("Permission:Edit"));
            employeePermission.AddChild(
                AttendanceManagementPermissions.Employees.Delete,
                L("Permission:Delete"));
            employeePermission.AddChild(
                AttendanceManagementPermissions.Employees.Export,
                L("Permission:Export"));

            // Group permissions
            var groupPermission = attendanceGroup.AddPermission(
                AttendanceManagementPermissions.Groups.Default,
                L("Permission:Groups"));
            groupPermission.AddChild(
                AttendanceManagementPermissions.Groups.Create,
                L("Permission:Create"));
            groupPermission.AddChild(
                AttendanceManagementPermissions.Groups.Edit,
                L("Permission:Edit"));
            groupPermission.AddChild(
                AttendanceManagementPermissions.Groups.Delete,
                L("Permission:Delete"));
            groupPermission.AddChild(
                AttendanceManagementPermissions.Groups.ManageMembers,
                L("Permission:ManageMembers"));
            groupPermission.AddChild(
                AttendanceManagementPermissions.Groups.Export,
                L("Permission:Export"));

            // Schedule permissions
            var schedulePermission = attendanceGroup.AddPermission(
                AttendanceManagementPermissions.Schedules.Default,
                L("Permission:Schedules"));
            schedulePermission.AddChild(
                AttendanceManagementPermissions.Schedules.Create,
                L("Permission:Create"));
            schedulePermission.AddChild(
                AttendanceManagementPermissions.Schedules.Edit,
                L("Permission:Edit"));
            schedulePermission.AddChild(
                AttendanceManagementPermissions.Schedules.Delete,
                L("Permission:Delete"));
            schedulePermission.AddChild(
                AttendanceManagementPermissions.Schedules.Assign,
                L("Permission:Assign"));
            schedulePermission.AddChild(
                AttendanceManagementPermissions.Schedules.ViewOwn,
                L("Permission:ViewOwn"));
            schedulePermission.AddChild(
                AttendanceManagementPermissions.Schedules.Export,
                L("Permission:Export"));

            // Workflow permissions
            var workflowPermission = attendanceGroup.AddPermission(
                AttendanceManagementPermissions.Workflows.Default,
                L("Permission:Workflows"));
            workflowPermission.AddChild(
                AttendanceManagementPermissions.Workflows.Create,
                L("Permission:Create"));
            workflowPermission.AddChild(
                AttendanceManagementPermissions.Workflows.Edit,
                L("Permission:Edit"));
            workflowPermission.AddChild(
                AttendanceManagementPermissions.Workflows.Delete,
                L("Permission:Delete"));
            workflowPermission.AddChild(
                AttendanceManagementPermissions.Workflows.ManageSteps,
                L("Permission:ManageSteps"));
            workflowPermission.AddChild(
                AttendanceManagementPermissions.Workflows.Export,
                L("Permission:Export"));

            // Exception Request permissions
            var exceptionRequestPermission = attendanceGroup.AddPermission(
                AttendanceManagementPermissions.ExceptionRequests.Default,
                L("Permission:ExceptionRequests"));
            exceptionRequestPermission.AddChild(
                AttendanceManagementPermissions.ExceptionRequests.Create,
                L("Permission:Create"));
            exceptionRequestPermission.AddChild(
                AttendanceManagementPermissions.ExceptionRequests.Edit,
                L("Permission:Edit"));
            exceptionRequestPermission.AddChild(
                AttendanceManagementPermissions.ExceptionRequests.Delete,
                L("Permission:Delete"));
            exceptionRequestPermission.AddChild(
                AttendanceManagementPermissions.ExceptionRequests.ViewOwn,
                L("Permission:ViewOwn"));
            exceptionRequestPermission.AddChild(
                AttendanceManagementPermissions.ExceptionRequests.ViewAll,
                L("Permission:ViewAll"));
            exceptionRequestPermission.AddChild(
                AttendanceManagementPermissions.ExceptionRequests.Approve,
                L("Permission:Approve"));
            exceptionRequestPermission.AddChild(
                AttendanceManagementPermissions.ExceptionRequests.ApproveAsManager,
                L("Permission:ApproveAsManager"));
            exceptionRequestPermission.AddChild(
                AttendanceManagementPermissions.ExceptionRequests.ApproveAsHR,
                L("Permission:ApproveAsHR"));
            exceptionRequestPermission.AddChild(
                AttendanceManagementPermissions.ExceptionRequests.ApproveAsDoctor,
                L("Permission:ApproveAsDoctor"));

            // Report permissions
            var reportPermission = attendanceGroup.AddPermission(
                AttendanceManagementPermissions.Reports.Default,
                L("Permission:Reports"));
            reportPermission.AddChild(
                AttendanceManagementPermissions.Reports.EmployeeSchedules,
                L("Permission:EmployeeSchedules"));
            reportPermission.AddChild(
                AttendanceManagementPermissions.Reports.PendingRequests,
                L("Permission:PendingRequests"));
            reportPermission.AddChild(
                AttendanceManagementPermissions.Reports.ApprovalHistory,
                L("Permission:ApprovalHistory"));
            reportPermission.AddChild(
                AttendanceManagementPermissions.Reports.Export,
                L("Permission:Export"));
        }

        private static LocalizableString L(string name)
        {
            return LocalizableString.Create<AttendanceManagementResource>(name);
        }
    }
}