using AttendanceManagement.Localization;
using Volo.Abp.AspNetCore.Components;

namespace AttendanceManagement.Blazor.Client;

public abstract class AttendanceManagementComponentBase : AbpComponentBase
{
    protected AttendanceManagementComponentBase()
    {
        LocalizationResource = typeof(AttendanceManagementResource);
    }
}
