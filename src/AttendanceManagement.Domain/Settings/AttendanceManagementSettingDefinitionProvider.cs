using Volo.Abp.Settings;

namespace AttendanceManagement.Settings;

public class AttendanceManagementSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(AttendanceManagementSettings.MySetting1));
    }
}
