using AttendanceManagement.Samples;
using Xunit;

namespace AttendanceManagement.EntityFrameworkCore.Applications;

[Collection(AttendanceManagementTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<AttendanceManagementEntityFrameworkCoreTestModule>
{

}
