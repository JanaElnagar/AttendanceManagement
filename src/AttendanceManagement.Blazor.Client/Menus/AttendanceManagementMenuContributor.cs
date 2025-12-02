using AttendanceManagement.Localization;
using AttendanceManagement.MultiTenancy;
using AttendanceManagement.Permissions;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using Volo.Abp.Account.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity.Blazor;
using Volo.Abp.SettingManagement.Blazor.Menus;
using Volo.Abp.TenantManagement.Blazor.Navigation;
using Volo.Abp.UI.Navigation;

namespace AttendanceManagement.Blazor.Client.Menus;

public class AttendanceManagementMenuContributor : IMenuContributor
{
    private readonly IConfiguration _configuration;

    public AttendanceManagementMenuContributor(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
        else if (context.Menu.Name == StandardMenus.User)
        {
            await ConfigureUserMenuAsync(context);
        }
    }

    private Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<AttendanceManagementResource>();

        context.Menu.Items.Insert(
            0,
            new ApplicationMenuItem(
                AttendanceManagementMenus.Home,
                l["Menu:Home"],
                "/",
                icon: "fas fa-home"
            )
        );
        // Attendance Management Menu Group
        var attendanceMenu = new ApplicationMenuItem(
            "AttendanceManagement",
            l["Menu:AttendanceManagement"],
            icon: "fa fa-calendar-check"
        );

        // My Schedule
        attendanceMenu.AddItem(
            new ApplicationMenuItem(
                "AttendanceManagement.MySchedule",
                l["Menu:MySchedule"],
                url: "/my-schedule",
                icon: "fa fa-calendar"
            ).RequirePermissions(AttendanceManagementPermissions.Schedules.ViewOwn)
        );

        // My Exception Requests
        attendanceMenu.AddItem(
            new ApplicationMenuItem(
                "AttendanceManagement.MyRequests",
                l["Menu:MyExceptionRequests"],
                url: "/my-exception-requests",
                icon: "fa fa-file-alt"
            ).RequirePermissions(AttendanceManagementPermissions.ExceptionRequests.ViewOwn)
        );

        // Pending Approvals (for managers/HR)
        attendanceMenu.AddItem(
            new ApplicationMenuItem(
                "AttendanceManagement.PendingApprovals",
                l["Menu:PendingApprovals"],
                url: "/pending-approvals",
                icon: "fa fa-check-circle"
            ).RequirePermissions(AttendanceManagementPermissions.ExceptionRequests.Approve)
        );

        // Admin Section
        var adminMenu = attendanceMenu.AddItem(
            new ApplicationMenuItem(
                "AttendanceManagement.Admin",
                l["Menu:Administration"],
                icon: "fa fa-cog"
            )
        );

        // Employees Management
        adminMenu.AddItem(
            new ApplicationMenuItem(
                "AttendanceManagement.Employees",
                l["Menu:Employees"],
                url: "/employees",
                icon: "fa fa-users"
            ).RequirePermissions(AttendanceManagementPermissions.Employees.Default)
        );

        // Groups Management
        adminMenu.AddItem(
            new ApplicationMenuItem(
                "AttendanceManagement.Groups",
                l["Menu:Groups"],
                url: "/groups",
                icon: "fa fa-object-group"
            ).RequirePermissions(AttendanceManagementPermissions.Groups.Default)
        );

        // Schedules Management
        adminMenu.AddItem(
            new ApplicationMenuItem(
                "AttendanceManagement.Schedules",
                l["Menu:Schedules"],
                url: "/schedules",
                icon: "fa fa-calendar-alt"
            ).RequirePermissions(AttendanceManagementPermissions.Schedules.Assign)
        );

        // Workflows Management
        adminMenu.AddItem(
            new ApplicationMenuItem(
                "AttendanceManagement.Workflows",
                l["Menu:Workflows"],
                url: "/workflows",
                icon: "fa fa-project-diagram"
            ).RequirePermissions(AttendanceManagementPermissions.Workflows.Default)
        );

        // All Exception Requests (for HR/Admin)
        //adminMenu.AddItem(
        //    new ApplicationMenuItem(
        //        "AttendanceManagement.AllRequests",
        //        l["Menu:AllExceptionRequests"],
        //        url: "/all-exception-requests",
        //        icon: "fa fa-list"
        //    ).RequirePermissions(AttendanceManagementPermissions.ExceptionRequests.ViewAll)
        //);

        context.Menu.Items.Insert(1, attendanceMenu);

        var administration = context.Menu.GetAdministration();

        if (MultiTenancyConsts.IsEnabled)
        {
            administration.SetSubItemOrder(TenantManagementMenuNames.GroupName, 1);
        }
        else
        {
            administration.TryRemoveMenuItem(TenantManagementMenuNames.GroupName);
        }

        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 2);
        administration.SetSubItemOrder(SettingManagementMenus.GroupName, 3);

        return Task.CompletedTask;
    }

    private Task ConfigureUserMenuAsync(MenuConfigurationContext context)
    {
        var accountStringLocalizer = context.GetLocalizer<AccountResource>();

        var authServerUrl = _configuration["AuthServer:Authority"] ?? "";

        context.Menu.AddItem(new ApplicationMenuItem(
            "Account.Manage",
            accountStringLocalizer["MyAccount"],
            $"{authServerUrl.EnsureEndsWith('/')}Account/Manage",
            icon: "fa fa-cog",
            order: 1000,
            target: "_blank").RequireAuthenticated());

        return Task.CompletedTask;
    }
}
