using AttendanceManagement.Samples;
using Xunit;

namespace AttendanceManagement.EntityFrameworkCore.Domains;

[Collection(AttendanceManagementTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<AttendanceManagementEntityFrameworkCoreTestModule>
{

}
