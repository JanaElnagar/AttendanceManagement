using AttendanceManagement.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace AttendanceManagement.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class AttendanceManagementController : AbpControllerBase
{
    protected AttendanceManagementController()
    {
        LocalizationResource = typeof(AttendanceManagementResource);
    }
}
