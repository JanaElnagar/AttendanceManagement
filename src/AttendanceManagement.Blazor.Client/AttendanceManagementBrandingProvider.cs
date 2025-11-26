using Microsoft.Extensions.Localization;
using AttendanceManagement.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace AttendanceManagement.Blazor.Client;

[Dependency(ReplaceServices = true)]
public class AttendanceManagementBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<AttendanceManagementResource> _localizer;

    public AttendanceManagementBrandingProvider(IStringLocalizer<AttendanceManagementResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
