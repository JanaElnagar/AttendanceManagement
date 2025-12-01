using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.ExceptionRequests;
using AttendanceManagement.Data.Groups;
using AttendanceManagement.Data.Schedules;
using AttendanceManagement.Data.Workflows;
using AttendanceManagement.Enums;
using AttendanceManagement.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace AttendanceManagement.Data
{
    public class AttendanceManagementDataSeeder : IDataSeedContributor, ITransientDependency
    {
        private readonly IRepository<Employee, Guid> _employeeRepository;
        private readonly IRepository<Group, Guid> _groupRepository;
        private readonly IRepository<Schedule, Guid> _scheduleRepository;
        private readonly IRepository<Workflow, Guid> _workflowRepository;
        private readonly IIdentityUserRepository _identityUserRepository;
        private readonly IdentityUserManager _identityUserManager;
        private readonly IdentityRoleManager _identityRoleManager;
        private readonly IPermissionManager _permissionManager;
        private readonly IGuidGenerator _guidGenerator;

        // Predefined GUIDs for consistent seeding
        private Guid _adminUserId;
        private Guid _hrManagerUserId;
        private Guid _manager1UserId;
        private Guid _manager2UserId;
        private Guid _doctorUserId;
        private Guid _employee1UserId;
        private Guid _employee2UserId;
        private Guid _employee3UserId;
        private Guid _employee4UserId;
        private Guid _employee5UserId;

        public AttendanceManagementDataSeeder(
            IRepository<Employee, Guid> employeeRepository,
            IRepository<Group, Guid> groupRepository,
            IRepository<Schedule, Guid> scheduleRepository,
            IRepository<Workflow, Guid> workflowRepository,
            IIdentityUserRepository identityUserRepository,
            IdentityUserManager identityUserManager,
            IdentityRoleManager identityRoleManager,
            IPermissionManager permissionManager,
            IGuidGenerator guidGenerator)
        {
            _employeeRepository = employeeRepository;
            _groupRepository = groupRepository;
            _scheduleRepository = scheduleRepository;
            _workflowRepository = workflowRepository;
            _identityUserRepository = identityUserRepository;
            _identityUserManager = identityUserManager;
            _identityRoleManager = identityRoleManager;
            _permissionManager = permissionManager;
            _guidGenerator = guidGenerator;
        }

        public async Task SeedAsync(DataSeedContext context)
        {
            // Check if already seeded
            if (await _employeeRepository.GetCountAsync() > 0)
            {
                return;
            }

            // Get existing Identity users (created by ABP)
            var adminUser = await _identityUserRepository.FindByNormalizedUserNameAsync("ADMIN");
            _adminUserId = adminUser.Id;

            // Seed Roles and Permissions first
            await SeedRolesAndPermissionsAsync();

            // Seed Identity Users first
            await SeedIdentityUsersAsync();

            // Assign roles to users
            await AssignRolesToUsersAsync();

            // Seed Groups first (before employees)
            var groups = await SeedGroupsAsync();

            // Seed Workflows (before employees so we can assign them)
            var workflows = await SeedWorkflowsAsync(employees: null);

            // Seed Employees with group assignments and workflow assignments
            var employees = await SeedEmployeesAsync(groups, workflows);

            // Update workflow steps with actual employee IDs now that employees exist
            await UpdateWorkflowStepsWithEmployeeIds(workflows, employees);

            // Seed Manager Assignments (attach to employee aggregates and update)
            await SeedManagerAssignmentsAsync(employees);

            // Seed Schedules (including days) - schedules are aggregates
            var schedules = await SeedSchedulesAsync();

            // Seed Schedule Assignments (attach to schedule aggregates and update)
            await SeedScheduleAssignmentsAsync(schedules, employees, groups);
        }

        private async Task SeedIdentityUsersAsync()
        {
            // Get or create users
            var hrManager = await GetOrCreateUserAsync("hrmanager", "hr@attendance.com", "HR Manager", "HRManager123!");
            _hrManagerUserId = hrManager.Id;

            var manager1 = await GetOrCreateUserAsync("manager1", "manager1@attendance.com", "John Manager", "Manager123!");
            _manager1UserId = manager1.Id;

            var manager2 = await GetOrCreateUserAsync("manager2", "manager2@attendance.com", "Sarah Manager", "Manager123!");
            _manager2UserId = manager2.Id;

            var doctor = await GetOrCreateUserAsync("doctor", "doctor@attendance.com", "Dr. Smith", "Doctor123!");
            _doctorUserId = doctor.Id;

            var emp1 = await GetOrCreateUserAsync("emp1", "emp1@attendance.com", "Alice Employee", "Employee123!");
            _employee1UserId = emp1.Id;

            var emp2 = await GetOrCreateUserAsync("emp2", "emp2@attendance.com", "Bob Employee", "Employee123!");
            _employee2UserId = emp2.Id;

            var emp3 = await GetOrCreateUserAsync("emp3", "emp3@attendance.com", "Charlie Employee", "Employee123!");
            _employee3UserId = emp3.Id;

            var emp4 = await GetOrCreateUserAsync("emp4", "emp4@attendance.com", "Diana Employee", "Employee123!");
            _employee4UserId = emp4.Id;

            var emp5 = await GetOrCreateUserAsync("emp5", "emp5@attendance.com", "Eve Employee", "Employee123!");
            _employee5UserId = emp5.Id;
        }

        private async Task<IdentityUser> GetOrCreateUserAsync(string userName, string email, string name, string password)
        {
            var user = await _identityUserRepository.FindByNormalizedUserNameAsync(userName.ToUpperInvariant());
            if (user == null)
            {
                user = new IdentityUser(_guidGenerator.Create(), userName, email)
                {
                    Name = name
                };
                await _identityUserRepository.InsertAsync(user);
            }
            return user;
        }

        private async Task SeedRolesAndPermissionsAsync()
        {
            // Create roles
            var adminRole = await GetOrCreateRoleAsync("admin", "Administrator");
            var hrManagerRole = await GetOrCreateRoleAsync("hrmanager", "HR Manager");
            var managerRole = await GetOrCreateRoleAsync("manager", "Manager");
            var doctorRole = await GetOrCreateRoleAsync("doctor", "Doctor");
            var employeeRole = await GetOrCreateRoleAsync("employee", "Employee");

            // Assign permissions to Admin role (all permissions)
            await SetRolePermissionsAsync(adminRole.Name, new[]
            {
                AttendanceManagementPermissions.Employees.Default,
                AttendanceManagementPermissions.Employees.Create,
                AttendanceManagementPermissions.Employees.Edit,
                AttendanceManagementPermissions.Employees.Delete,
                AttendanceManagementPermissions.Groups.Default,
                AttendanceManagementPermissions.Groups.Create,
                AttendanceManagementPermissions.Groups.Edit,
                AttendanceManagementPermissions.Groups.Delete,
                AttendanceManagementPermissions.Groups.ManageMembers,
                AttendanceManagementPermissions.Schedules.Default,
                AttendanceManagementPermissions.Schedules.Create,
                AttendanceManagementPermissions.Schedules.Edit,
                AttendanceManagementPermissions.Schedules.Delete,
                AttendanceManagementPermissions.Schedules.Assign,
                AttendanceManagementPermissions.Schedules.ViewOwn,
                AttendanceManagementPermissions.Schedules.Export,
                AttendanceManagementPermissions.Workflows.Default,
                AttendanceManagementPermissions.Workflows.Create,
                AttendanceManagementPermissions.Workflows.Edit,
                AttendanceManagementPermissions.Workflows.Delete,
                AttendanceManagementPermissions.Workflows.ManageSteps,
                AttendanceManagementPermissions.ExceptionRequests.Default,
                AttendanceManagementPermissions.ExceptionRequests.Create,
                AttendanceManagementPermissions.ExceptionRequests.Edit,
                AttendanceManagementPermissions.ExceptionRequests.Delete,
                AttendanceManagementPermissions.ExceptionRequests.ViewOwn,
                AttendanceManagementPermissions.ExceptionRequests.ViewAll,
                AttendanceManagementPermissions.ExceptionRequests.Approve,
                AttendanceManagementPermissions.ExceptionRequests.ApproveAsManager,
                AttendanceManagementPermissions.ExceptionRequests.ApproveAsHR,
                AttendanceManagementPermissions.ExceptionRequests.ApproveAsDoctor
            });

            // Assign permissions to HR Manager role
            await SetRolePermissionsAsync(hrManagerRole.Name, new[]
            {
                AttendanceManagementPermissions.Employees.Default,
                AttendanceManagementPermissions.Employees.Create,
                AttendanceManagementPermissions.Employees.Edit,
                AttendanceManagementPermissions.Groups.Default,
                AttendanceManagementPermissions.Groups.Create,
                AttendanceManagementPermissions.Groups.Edit,
                AttendanceManagementPermissions.Groups.ManageMembers,
                AttendanceManagementPermissions.Schedules.Default,
                AttendanceManagementPermissions.Schedules.Create,
                AttendanceManagementPermissions.Schedules.Edit,
                AttendanceManagementPermissions.Schedules.Assign,
                AttendanceManagementPermissions.Schedules.ViewOwn,
                AttendanceManagementPermissions.Schedules.Export,
                AttendanceManagementPermissions.Workflows.Default,
                AttendanceManagementPermissions.Workflows.Create,
                AttendanceManagementPermissions.Workflows.Edit,
                AttendanceManagementPermissions.Workflows.ManageSteps,
                AttendanceManagementPermissions.ExceptionRequests.Default,
                AttendanceManagementPermissions.ExceptionRequests.ViewAll,
                AttendanceManagementPermissions.ExceptionRequests.Approve,
                AttendanceManagementPermissions.ExceptionRequests.ApproveAsHR
            });

            // Assign permissions to Manager role
            await SetRolePermissionsAsync(managerRole.Name, new[]
            {
                AttendanceManagementPermissions.Schedules.Default,
                AttendanceManagementPermissions.Schedules.ViewOwn,
                AttendanceManagementPermissions.ExceptionRequests.Default,
                AttendanceManagementPermissions.ExceptionRequests.ViewOwn,
                AttendanceManagementPermissions.ExceptionRequests.Approve,
                AttendanceManagementPermissions.ExceptionRequests.ApproveAsManager
            });

            // Assign permissions to Doctor role
            await SetRolePermissionsAsync(doctorRole.Name, new[]
            {
                AttendanceManagementPermissions.ExceptionRequests.Default,
                AttendanceManagementPermissions.ExceptionRequests.Approve,
                AttendanceManagementPermissions.ExceptionRequests.ApproveAsDoctor
            });

            // Assign permissions to Employee role
            await SetRolePermissionsAsync(employeeRole.Name, new[]
            {
                AttendanceManagementPermissions.Schedules.Default,
                AttendanceManagementPermissions.Schedules.ViewOwn,
                AttendanceManagementPermissions.ExceptionRequests.Default,
                AttendanceManagementPermissions.ExceptionRequests.Create,
                AttendanceManagementPermissions.ExceptionRequests.ViewOwn
            });
        }

        private async Task<IdentityRole> GetOrCreateRoleAsync(string roleName, string displayName)
        {
            var role = await _identityRoleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                role = new IdentityRole(_guidGenerator.Create(), roleName);
                await _identityRoleManager.CreateAsync(role);
            }
            return role;
        }

        private async Task SetRolePermissionsAsync(string roleName, string[] permissions)
        {
            foreach (var permission in permissions)
            {
                await _permissionManager.SetForRoleAsync(roleName, permission, true);
            }
        }

        private async Task AssignRolesToUsersAsync()
        {
            var adminUser = await _identityUserRepository.FindByNormalizedUserNameAsync("ADMIN");
            var hrManagerUser = await _identityUserRepository.FindByNormalizedUserNameAsync("HRMANAGER");
            var manager1User = await _identityUserRepository.FindByNormalizedUserNameAsync("MANAGER1");
            var manager2User = await _identityUserRepository.FindByNormalizedUserNameAsync("MANAGER2");
            var doctorUser = await _identityUserRepository.FindByNormalizedUserNameAsync("DOCTOR");
            var emp1User = await _identityUserRepository.FindByNormalizedUserNameAsync("EMP1");
            var emp2User = await _identityUserRepository.FindByNormalizedUserNameAsync("EMP2");
            var emp3User = await _identityUserRepository.FindByNormalizedUserNameAsync("EMP3");
            var emp4User = await _identityUserRepository.FindByNormalizedUserNameAsync("EMP4");
            var emp5User = await _identityUserRepository.FindByNormalizedUserNameAsync("EMP5");

            if (adminUser != null)
            {
                await _identityUserManager.AddToRoleAsync(adminUser, "admin");
            }

            if (hrManagerUser != null)
            {
                await _identityUserManager.AddToRoleAsync(hrManagerUser, "hrmanager");
            }

            if (manager1User != null)
            {
                await _identityUserManager.AddToRoleAsync(manager1User, "manager");
            }

            if (manager2User != null)
            {
                await _identityUserManager.AddToRoleAsync(manager2User, "manager");
            }

            if (doctorUser != null)
            {
                await _identityUserManager.AddToRoleAsync(doctorUser, "doctor");
            }

            if (emp1User != null)
            {
                await _identityUserManager.AddToRoleAsync(emp1User, "employee");
            }

            if (emp2User != null)
            {
                await _identityUserManager.AddToRoleAsync(emp2User, "employee");
            }

            if (emp3User != null)
            {
                await _identityUserManager.AddToRoleAsync(emp3User, "employee");
            }

            if (emp4User != null)
            {
                await _identityUserManager.AddToRoleAsync(emp4User, "employee");
            }

            if (emp5User != null)
            {
                await _identityUserManager.AddToRoleAsync(emp5User, "employee");
            }
        }

        private async Task<Dictionary<string, Group>> SeedGroupsAsync()
        {
            var groups = new Dictionary<string, Group>
            {
                ["engineering"] = new Group(
                    _guidGenerator.Create(),
                    "Engineering Team",
                    "Software development and engineering team"
                ),
                ["marketing"] = new Group(
                    _guidGenerator.Create(),
                    "Marketing Team",
                    "Digital marketing and content team"
                ),
                ["management"] = new Group(
                    _guidGenerator.Create(),
                    "Management Team",
                    "Managers and team leads"
                )
            };

            await _groupRepository.InsertManyAsync(groups.Values);

            return groups;
        }

        private async Task<Dictionary<string, Employee>> SeedEmployeesAsync(
            Dictionary<string, Group> groups,
            Dictionary<string, Workflow> workflows)
        {
            var employees = new Dictionary<string, Employee>();

            // HR Manager
            var hrManager = new Employee(
                _guidGenerator.Create(),
                _hrManagerUserId,
                "HR Manager",
                "Human Resources",
                "HR Sector",
                groups["management"].Id
            )
            {
                ManagerAssignments = new List<ManagerAssignment>(),
                ManagedEmployees = new List<ManagerAssignment>(),
                ScheduleAssignments = new List<ScheduleAssignment>(),
                ExceptionRequests = new List<ExceptionRequest>()
            };
            hrManager.AssignWorkflow(null); // HR doesn't need workflow
            employees["hrmanager"] = hrManager;

            // Managers
            var manager1 = new Employee(
                _guidGenerator.Create(),
                _manager1UserId,
                "John Manager",
                "Engineering",
                "Software Development",
                groups["management"].Id
            )
            {
                ManagerAssignments = new List<ManagerAssignment>(),
                ManagedEmployees = new List<ManagerAssignment>(),
                ScheduleAssignments = new List<ScheduleAssignment>(),
                ExceptionRequests = new List<ExceptionRequest>()
            };
            manager1.AssignWorkflow(workflows["manager"].Id);
            employees["manager1"] = manager1;

            var manager2 = new Employee(
                _guidGenerator.Create(),
                _manager2UserId,
                "Sarah Manager",
                "Marketing",
                "Digital Marketing",
                groups["management"].Id
            )
            {
                ManagerAssignments = new List<ManagerAssignment>(),
                ManagedEmployees = new List<ManagerAssignment>(),
                ScheduleAssignments = new List<ScheduleAssignment>(),
                ExceptionRequests = new List<ExceptionRequest>()
            };
            manager2.AssignWorkflow(workflows["manager"].Id);
            employees["manager2"] = manager2;

            // Regular Employees - Junior Level
            var emp1 = new Employee(
                _guidGenerator.Create(),
                _employee1UserId,
                "Alice Employee",
                "Engineering",
                "Software Development",
                groups["engineering"].Id
            )
            {
                ManagerAssignments = new List<ManagerAssignment>(),
                ManagedEmployees = new List<ManagerAssignment>(),
                ScheduleAssignments = new List<ScheduleAssignment>(),
                ExceptionRequests = new List<ExceptionRequest>()
            };
            emp1.AssignWorkflow(workflows["junior"].Id);
            employees["emp1"] = emp1;

            var emp2 = new Employee(
                _guidGenerator.Create(),
                _employee2UserId,
                "Bob Employee",
                "Engineering",
                "Software Development",
                groups["engineering"].Id
            )
            {
                ManagerAssignments = new List<ManagerAssignment>(),
                ManagedEmployees = new List<ManagerAssignment>(),
                ScheduleAssignments = new List<ScheduleAssignment>(),
                ExceptionRequests = new List<ExceptionRequest>()
            };
            emp2.AssignWorkflow(workflows["junior"].Id);
            employees["emp2"] = emp2;

            // Regular Employees - Senior Level
            var emp3 = new Employee(
                _guidGenerator.Create(),
                _employee3UserId,
                "Charlie Employee",
                "Marketing",
                "Digital Marketing",
                groups["marketing"].Id
            )
            {
                ManagerAssignments = new List<ManagerAssignment>(),
                ManagedEmployees = new List<ManagerAssignment>(),
                ScheduleAssignments = new List<ScheduleAssignment>(),
                ExceptionRequests = new List<ExceptionRequest>()
            };
            emp3.AssignWorkflow(workflows["senior"].Id);
            employees["emp3"] = emp3;

            var emp4 = new Employee(
                _guidGenerator.Create(),
                _employee4UserId,
                "Diana Employee",
                "Marketing",
                "Digital Marketing",
                groups["marketing"].Id
            )
            {
                ManagerAssignments = new List<ManagerAssignment>(),
                ManagedEmployees = new List<ManagerAssignment>(),
                ScheduleAssignments = new List<ScheduleAssignment>(),
                ExceptionRequests = new List<ExceptionRequest>()
            };
            emp4.AssignWorkflow(workflows["senior"].Id);
            employees["emp4"] = emp4;

            var emp5 = new Employee(
                _guidGenerator.Create(),
                _employee5UserId,
                "Eve Employee",
                "Engineering",
                "Software Development",
                groups["engineering"].Id
            )
            {
                ManagerAssignments = new List<ManagerAssignment>(),
                ManagedEmployees = new List<ManagerAssignment>(),
                ScheduleAssignments = new List<ScheduleAssignment>(),
                ExceptionRequests = new List<ExceptionRequest>()
            };
            emp5.AssignWorkflow(workflows["senior"].Id);
            employees["emp5"] = emp5;

            await _employeeRepository.InsertManyAsync(employees.Values);

            return employees;
        }

        private async Task SeedManagerAssignmentsAsync(Dictionary<string, Employee> employees)
        {
            var assignments = new List<ManagerAssignment>
            {
                // Manager 1 manages emp1, emp2, emp5
                new ManagerAssignment(
                    _guidGenerator.Create(),
                    employees["emp1"].Id,
                    employees["manager1"].Id,
                    true,
                    DateTime.UtcNow.AddMonths(-6)
                ),
                new ManagerAssignment(
                    _guidGenerator.Create(),
                    employees["emp2"].Id,
                    employees["manager1"].Id,
                    true,
                    DateTime.UtcNow.AddMonths(-6)
                ),
                new ManagerAssignment(
                    _guidGenerator.Create(),
                    employees["emp5"].Id,
                    employees["manager1"].Id,
                    true,
                    DateTime.UtcNow.AddMonths(-6)
                ),

                // Manager 2 manages emp3, emp4
                new ManagerAssignment(
                    _guidGenerator.Create(),
                    employees["emp3"].Id,
                    employees["manager2"].Id,
                    true,
                    DateTime.UtcNow.AddMonths(-6)
                ),
                new ManagerAssignment(
                    _guidGenerator.Create(),
                    employees["emp4"].Id,
                    employees["manager2"].Id,
                    true,
                    DateTime.UtcNow.AddMonths(-6)
                )
            };

            // Attach assignments to employee aggregates
            foreach (var assignment in assignments)
            {
                var employee = employees.Values.First(e => e.Id == assignment.EmployeeId);
                var manager = employees.Values.First(e => e.Id == assignment.ManagerEmployeeId);

                employee.ManagerAssignments.Add(assignment);
                manager.ManagedEmployees.Add(assignment);
            }

            // Update employee aggregates (this will cascade save the assignments)
            foreach (var emp in employees.Values)
            {
                if (emp.ManagerAssignments.Any() || emp.ManagedEmployees.Any())
                {
                    await _employeeRepository.UpdateAsync(emp);
                }
            }
        }

        private async Task<Dictionary<string, Schedule>> SeedSchedulesAsync()
        {
            var schedules = new Dictionary<string, Schedule>
            {
                ["hybrid_3days"] = new Schedule(
                    _guidGenerator.Create(),
                    "Hybrid 3 Days On-Site",
                    "3 days on-site (Sun, Tue, Thu), 2 days remote"
                )
                {
                    ScheduleDays = new List<ScheduleDay>(),
                    ScheduleAssignments = new List<ScheduleAssignment>()
                },
                ["hybrid_2days"] = new Schedule(
                    _guidGenerator.Create(),
                    "Hybrid 2 Days On-Site",
                    "2 days on-site (Mon, Wed), 3 days remote"
                )
                {
                    ScheduleDays = new List<ScheduleDay>(),
                    ScheduleAssignments = new List<ScheduleAssignment>()
                },
                ["full_onsite"] = new Schedule(
                    _guidGenerator.Create(),
                    "Full On-Site",
                    "5 days on-site per week"
                )
                {
                    ScheduleDays = new List<ScheduleDay>(),
                    ScheduleAssignments = new List<ScheduleAssignment>()
                },
                ["full_remote"] = new Schedule(
                    _guidGenerator.Create(),
                    "Full Remote",
                    "Remote work arrangement"
                )
                {
                    ScheduleDays = new List<ScheduleDay>(),
                    ScheduleAssignments = new List<ScheduleAssignment>()
                }
            };

            // Add schedule days directly into schedule aggregates
            // Hybrid 3 Days: Sun, Tue, Thu on-site
            var hybrid3Days = new[]
            {
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_3days"].Id, DayOfWeek.Sunday, true),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_3days"].Id, DayOfWeek.Monday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_3days"].Id, DayOfWeek.Tuesday, true),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_3days"].Id, DayOfWeek.Wednesday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_3days"].Id, DayOfWeek.Thursday, true),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_3days"].Id, DayOfWeek.Friday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_3days"].Id, DayOfWeek.Saturday, false)
            };
            foreach (var d in hybrid3Days) schedules["hybrid_3days"].ScheduleDays.Add(d);

            // Hybrid 2 Days: Mon, Wed on-site
            var hybrid2Days = new[]
            {
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_2days"].Id, DayOfWeek.Sunday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_2days"].Id, DayOfWeek.Monday, true),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_2days"].Id, DayOfWeek.Tuesday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_2days"].Id, DayOfWeek.Wednesday, true),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_2days"].Id, DayOfWeek.Thursday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_2days"].Id, DayOfWeek.Friday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["hybrid_2days"].Id, DayOfWeek.Saturday, false)
            };
            foreach (var d in hybrid2Days) schedules["hybrid_2days"].ScheduleDays.Add(d);

            // Full On-Site: Sun-Thu
            var fullOnsiteDays = new[]
            {
                new ScheduleDay(_guidGenerator.Create(), schedules["full_onsite"].Id, DayOfWeek.Sunday, true),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_onsite"].Id, DayOfWeek.Monday, true),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_onsite"].Id, DayOfWeek.Tuesday, true),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_onsite"].Id, DayOfWeek.Wednesday, true),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_onsite"].Id, DayOfWeek.Thursday, true),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_onsite"].Id, DayOfWeek.Friday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_onsite"].Id, DayOfWeek.Saturday, false)
            };
            foreach (var d in fullOnsiteDays) schedules["full_onsite"].ScheduleDays.Add(d);

            // Full Remote: all days remote
            var fullRemoteDays = new[]
            {
                new ScheduleDay(_guidGenerator.Create(), schedules["full_remote"].Id, DayOfWeek.Sunday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_remote"].Id, DayOfWeek.Monday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_remote"].Id, DayOfWeek.Tuesday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_remote"].Id, DayOfWeek.Wednesday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_remote"].Id, DayOfWeek.Thursday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_remote"].Id, DayOfWeek.Friday, false),
                new ScheduleDay(_guidGenerator.Create(), schedules["full_remote"].Id, DayOfWeek.Saturday, false)
            };
            foreach (var d in fullRemoteDays) schedules["full_remote"].ScheduleDays.Add(d);

            // Persist schedules (EF will persist schedule days as children of Schedule aggregate)
            await _scheduleRepository.InsertManyAsync(schedules.Values);

            return schedules;
        }

        private async Task SeedScheduleAssignmentsAsync(
            Dictionary<string, Schedule> schedules,
            Dictionary<string, Employee> employees,
            Dictionary<string, Group> groups)
        {
            var assignments = new List<ScheduleAssignment>
            {
                // Individual assignments with seat numbers
                new ScheduleAssignment(
                    _guidGenerator.Create(),
                    schedules["hybrid_3days"].Id,
                    DateTime.UtcNow.AddMonths(-3),
                    employeeId: employees["emp1"].Id
                ) { SeatNumber = "A-101", FloorNumber = "Building A, Floor 1" },

                new ScheduleAssignment(
                    _guidGenerator.Create(),
                    schedules["hybrid_3days"].Id,
                    DateTime.UtcNow.AddMonths(-3),
                    employeeId: employees["emp2"].Id
                ) { SeatNumber = "A-102", FloorNumber = "Building A, Floor 1" },

                new ScheduleAssignment(
                    _guidGenerator.Create(),
                    schedules["hybrid_2days"].Id,
                    DateTime.UtcNow.AddMonths(-3),
                    employeeId: employees["emp3"].Id
                ) { SeatNumber = "B-201", FloorNumber = "Building B, Floor 2" },

                new ScheduleAssignment(
                    _guidGenerator.Create(),
                    schedules["hybrid_2days"].Id,
                    DateTime.UtcNow.AddMonths(-3),
                    employeeId: employees["emp4"].Id
                ) { SeatNumber = "B-202", FloorNumber = "Building B, Floor 2" },

                new ScheduleAssignment(
                    _guidGenerator.Create(),
                    schedules["full_onsite"].Id,
                    DateTime.UtcNow.AddMonths(-3),
                    employeeId: employees["emp5"].Id
                ) { SeatNumber = "A-103", FloorNumber = "Building A, Floor 1" },

                // Manager and HR full remote
                new ScheduleAssignment(
                    _guidGenerator.Create(),
                    schedules["full_remote"].Id,
                    DateTime.UtcNow.AddMonths(-3),
                    employeeId: employees["manager1"].Id
                ),

                new ScheduleAssignment(
                    _guidGenerator.Create(),
                    schedules["full_remote"].Id,
                    DateTime.UtcNow.AddMonths(-3),
                    employeeId: employees["manager2"].Id
                ),

                new ScheduleAssignment(
                    _guidGenerator.Create(),
                    schedules["full_remote"].Id,
                    DateTime.UtcNow.AddMonths(-3),
                    employeeId: employees["hrmanager"].Id
                )
            };

            // Attach assignments to schedule and employee aggregates
            foreach (var assignment in assignments)
            {
                var schedule = schedules.Values.First(s => s.Id == assignment.ScheduleId);
                schedule.ScheduleAssignments.Add(assignment);

                if (assignment.EmployeeId.HasValue)
                {
                    var emp = employees.Values.First(e => e.Id == assignment.EmployeeId.Value);
                    emp.ScheduleAssignments.Add(assignment);
                }

                if (assignment.GroupId.HasValue)
                {
                    var grp = groups.Values.First(g => g.Id == assignment.GroupId.Value);
                    grp.ScheduleAssignments.Add(assignment);
                }
            }

            // Update schedule aggregates (will cascade save assignments)
            foreach (var schedule in schedules.Values)
            {
                if (schedule.ScheduleAssignments.Any())
                {
                    await _scheduleRepository.UpdateAsync(schedule);
                }
            }

            // Update employee aggregates if they have schedule assignments
            foreach (var emp in employees.Values)
            {
                if (emp.ScheduleAssignments.Any())
                {
                    await _employeeRepository.UpdateAsync(emp);
                }
            }
        }

        private async Task<Dictionary<string, Workflow>> SeedWorkflowsAsync(Dictionary<string, Employee> employees)
        {
            // Create hierarchical workflows before employees are created
            var workflows = new Dictionary<string, Workflow>();

            // Workflow 1: Junior Level (Senior -> Team Lead -> Department Lead/HR)
            var juniorWorkflow = new Workflow(
                _guidGenerator.Create(),
                "Junior Employee Workflow",
                "3-step approval: Senior -> Team Lead -> Department Lead"
            )
            {
                WorkflowSteps = new List<WorkflowStep>()
            };
            workflows["junior"] = juniorWorkflow;

            // Workflow 2: Senior Level (Team Lead -> Department Lead/HR)
            var seniorWorkflow = new Workflow(
                _guidGenerator.Create(),
                "Senior Employee Workflow",
                "2-step approval: Team Lead -> Department Lead"
            )
            {
                WorkflowSteps = new List<WorkflowStep>()
            };
            workflows["senior"] = seniorWorkflow;

            // Workflow 3: Manager/Team Lead Level (Department Lead/HR only)
            var managerWorkflow = new Workflow(
                _guidGenerator.Create(),
                "Manager Workflow",
                "1-step approval: Department Lead/HR"
            )
            {
                WorkflowSteps = new List<WorkflowStep>()
            };
            workflows["manager"] = managerWorkflow;

            // If employees are already seeded, add the steps with actual employee IDs
            if (employees != null && employees.Any())
            {
                // Junior workflow steps - Add Doctor step first for sick leave, then Manager steps
                juniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    juniorWorkflow.Id,
                    1,
                    ApproverType.Doctor,
                    null // Doctor doesn't need employee ID
                ));
                juniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    juniorWorkflow.Id,
                    2,
                    ApproverType.Manager,
                    employees["manager1"].Id // Senior/Direct Manager
                ));
                juniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    juniorWorkflow.Id,
                    3,
                    ApproverType.Manager,
                    employees["manager2"].Id // Team Lead
                ));
                juniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    juniorWorkflow.Id,
                    4,
                    ApproverType.HRManager,
                    employees["hrmanager"].Id // Department Lead/HR
                ));

                // Senior workflow steps - Add Doctor step first for sick leave
                seniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    seniorWorkflow.Id,
                    1,
                    ApproverType.Doctor,
                    null // Doctor doesn't need employee ID
                ));
                seniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    seniorWorkflow.Id,
                    2,
                    ApproverType.Manager,
                    employees["manager2"].Id // Team Lead
                ));
                seniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    seniorWorkflow.Id,
                    3,
                    ApproverType.HRManager,
                    employees["hrmanager"].Id // Department Lead/HR
                ));

                // Manager workflow steps - Add Doctor step first for sick leave
                managerWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    managerWorkflow.Id,
                    1,
                    ApproverType.Doctor,
                    null // Doctor doesn't need employee ID
                ));
                managerWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    managerWorkflow.Id,
                    2,
                    ApproverType.HRManager,
                    employees["hrmanager"].Id
                ));
            }
            else
            {
                // Initial seeding without employees - we'll add placeholder steps
                // These will be updated later when we know the employee IDs
                // For now, add steps with null approver IDs (will be updated after employee creation)

                // Junior workflow placeholder steps - Add Doctor step first
                juniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    juniorWorkflow.Id,
                    1,
                    ApproverType.Doctor,
                    null // Doctor doesn't need employee ID
                ));
                juniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    juniorWorkflow.Id,
                    2,
                    ApproverType.Manager,
                    null
                ));
                juniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    juniorWorkflow.Id,
                    3,
                    ApproverType.Manager,
                    null
                ));
                juniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    juniorWorkflow.Id,
                    4,
                    ApproverType.HRManager,
                    null
                ));

                // Senior workflow placeholder steps - Add Doctor step first
                seniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    seniorWorkflow.Id,
                    1,
                    ApproverType.Doctor,
                    null // Doctor doesn't need employee ID
                ));
                seniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    seniorWorkflow.Id,
                    2,
                    ApproverType.Manager,
                    null
                ));
                seniorWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    seniorWorkflow.Id,
                    3,
                    ApproverType.HRManager,
                    null
                ));

                // Manager workflow placeholder steps - Add Doctor step first
                managerWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    managerWorkflow.Id,
                    1,
                    ApproverType.Doctor,
                    null // Doctor doesn't need employee ID
                ));
                managerWorkflow.WorkflowSteps.Add(new WorkflowStep(
                    _guidGenerator.Create(),
                    managerWorkflow.Id,
                    2,
                    ApproverType.HRManager,
                    null
                ));
            }

            // Insert workflows with steps as children of the Workflow aggregate
            await _workflowRepository.InsertManyAsync(workflows.Values);

            return workflows;
        }

        private async Task UpdateWorkflowStepsWithEmployeeIds(
            Dictionary<string, Workflow> workflows,
            Dictionary<string, Employee> employees)
        {
            // Update junior workflow steps
            // Step 0 = Doctor (skip, no employee ID needed)
            // Step 1 = Manager 1
            // Step 2 = Manager 2
            // Step 3 = HR Manager
            var juniorWorkflow = workflows["junior"];
            var juniorSteps = juniorWorkflow.WorkflowSteps.OrderBy(ws => ws.StepOrder).ToList();
            if (juniorSteps.Count >= 4)
            {
                juniorSteps[1].ApproverEmployeeId = employees["manager1"].Id; // Step 2
                juniorSteps[2].ApproverEmployeeId = employees["manager2"].Id; // Step 3
                juniorSteps[3].ApproverEmployeeId = employees["hrmanager"].Id; // Step 4
            }

            // Update senior workflow steps
            // Step 0 = Doctor (skip, no employee ID needed)
            // Step 1 = Manager 2
            // Step 2 = HR Manager
            var seniorWorkflow = workflows["senior"];
            var seniorSteps = seniorWorkflow.WorkflowSteps.OrderBy(ws => ws.StepOrder).ToList();
            if (seniorSteps.Count >= 3)
            {
                seniorSteps[1].ApproverEmployeeId = employees["manager2"].Id; // Step 2
                seniorSteps[2].ApproverEmployeeId = employees["hrmanager"].Id; // Step 3
            }

            // Update manager workflow steps
            // Step 0 = Doctor (skip, no employee ID needed)
            // Step 1 = HR Manager
            var managerWorkflow = workflows["manager"];
            var managerSteps = managerWorkflow.WorkflowSteps.OrderBy(ws => ws.StepOrder).ToList();
            if (managerSteps.Count >= 2)
            {
                managerSteps[1].ApproverEmployeeId = employees["hrmanager"].Id; // Step 2
            }

            // Persist the updates
            await _workflowRepository.UpdateAsync(juniorWorkflow);
            await _workflowRepository.UpdateAsync(seniorWorkflow);
            await _workflowRepository.UpdateAsync(managerWorkflow);
        }
    }
}
