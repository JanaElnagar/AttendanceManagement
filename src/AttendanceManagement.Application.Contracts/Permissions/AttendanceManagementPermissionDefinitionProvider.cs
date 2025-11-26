using AttendanceManagement.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace AttendanceManagement.Permissions;

public class AttendanceManagementPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(AttendanceManagementPermissions.GroupName);
        //Define your own permissions here. Example:
        //myGroup.AddPermission(AttendanceManagementPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<AttendanceManagementResource>(name);
    }
}
