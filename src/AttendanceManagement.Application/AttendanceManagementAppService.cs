using System;
using System.Collections.Generic;
using System.Text;
using AttendanceManagement.Localization;
using Volo.Abp.Application.Services;

namespace AttendanceManagement;

/* Inherit your application services from this class.
 */
public abstract class AttendanceManagementAppService : ApplicationService
{
    protected AttendanceManagementAppService()
    {
        LocalizationResource = typeof(AttendanceManagementResource);
    }
}
