using Volo.Abp.Reflection;

namespace AttendanceManagement.Permissions;

public static class AttendanceManagementPermissions
{
    public const string GroupName = "AttendanceManagement";

    //Add your own permission names. Example:
    //public const string MyPermission1 = GroupName + ".MyPermission1";
    public static class Dashboard
    {
        public const string DashboardGroup = GroupName + ".Dashboard";
        public const string Host = DashboardGroup + ".Host";
        public const string Tenant = DashboardGroup + ".Tenant";
    }

    public static class Employees
    {
        public const string Default = GroupName + ".Employees";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Groups
    {
        public const string Default = GroupName + ".Groups";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string ManageMembers = Default + ".ManageMembers";
    }

    public static class Schedules
    {
        public const string Default = GroupName + ".Schedules";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Assign = Default + ".Assign";
        public const string ViewOwn = Default + ".ViewOwn";
        public const string Export = Default + ".Export";
    }

    public static class Workflows
    {
        public const string Default = GroupName + ".Workflows";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string ManageSteps = Default + ".ManageSteps";
    }

    public static class ExceptionRequests
    {
        public const string Default = GroupName + ".ExceptionRequests";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string ViewOwn = Default + ".ViewOwn";
        public const string ViewAll = Default + ".ViewAll";
        public const string Approve = Default + ".Approve";
        public const string ApproveAsManager = Default + ".ApproveAsManager";
        public const string ApproveAsHR = Default + ".ApproveAsHR";
        public const string ApproveAsDoctor = Default + ".ApproveAsDoctor";
    }

    public static class Reports
    {
        public const string Default = GroupName + ".Reports";
        public const string EmployeeSchedules = Default + ".EmployeeSchedules";
        public const string PendingRequests = Default + ".PendingRequests";
        public const string ApprovalHistory = Default + ".ApprovalHistory";
        public const string Export = Default + ".Export";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(AttendanceManagementPermissions));
    }
}
