using AttendanceManagement.Data.Employees;
using AttendanceManagement.Data.ExceptionRequests;
using AttendanceManagement.Data.Groups;
using AttendanceManagement.Data.Notifications;
using AttendanceManagement.Data.Schedules;
using AttendanceManagement.Data.Workflows;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace AttendanceManagement.EntityFrameworkCore
{
    public static class AttendanceManagementDbContextModelCreatingExtensions
    {
        public static void ConfigureAttendanceManagement(this ModelBuilder builder)
        {
            Check.NotNull(builder, nameof(builder));

            // Employee Configuration
            builder.Entity<Employee>(b =>
            {
                b.ToTable("Employees");
                b.ConfigureByConvention();

                b.Property(e => e.Name).IsRequired().HasMaxLength(256);
                b.Property(e => e.Department).HasMaxLength(256);
                b.Property(e => e.Sector).HasMaxLength(256);

                b.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict)  // Prevent cascade delete
                    .IsRequired();                       // UserId is required

                b.HasIndex(e => e.UserId).IsUnique();

                b.HasOne(e => e.Group)
                    .WithMany(g => g.Employees)
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(e => e.Workflow)
                    .WithMany()
                    .HasForeignKey(e => e.WorkflowId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(e => e.UserId);
                b.HasIndex(e => e.IsActive);
                b.HasIndex(e => e.GroupId);
                b.HasIndex(e => e.WorkflowId);
            });

            // ManagerAssignment Configuration
            builder.Entity<ManagerAssignment>(b =>
            {
                b.ToTable("ManagerAssignments");
                b.ConfigureByConvention();

                b.HasOne(ma => ma.Employee)
                    .WithMany(e => e.ManagerAssignments)
                    .HasForeignKey(ma => ma.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(ma => ma.Manager)
                    .WithMany(e => e.ManagedEmployees)
                    .HasForeignKey(ma => ma.ManagerEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(ma => new { ma.EmployeeId, ma.IsPrimaryManager });
            });

            // Group Configuration
            builder.Entity<Group>(b =>
            {
                b.ToTable("Groups");
                b.ConfigureByConvention();

                b.Property(g => g.Name).IsRequired().HasMaxLength(256);
                b.Property(g => g.Description).HasMaxLength(1000);

                b.HasIndex(g => g.Name);
            });

            // Schedule Configuration
            builder.Entity<Schedule>(b =>
            {
                b.ToTable("Schedules");
                b.ConfigureByConvention();

                b.Property(s => s.Name).IsRequired().HasMaxLength(256);
                b.Property(s => s.Description).HasMaxLength(1000);

                b.HasIndex(s => s.Name);
            });

            // ScheduleDay Configuration
            builder.Entity<ScheduleDay>(b =>
            {
                b.ToTable("ScheduleDays");
                b.ConfigureByConvention();

                b.HasOne(sd => sd.Schedule)
                    .WithMany(s => s.ScheduleDays)
                    .HasForeignKey(sd => sd.ScheduleId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(sd => new { sd.ScheduleId, sd.DayOfWeek }).IsUnique();
            });

            // ScheduleAssignment Configuration
            builder.Entity<ScheduleAssignment>(b =>
            {
                b.ToTable("ScheduleAssignments");
                b.ConfigureByConvention();

                b.Property(sa => sa.SeatNumber).HasMaxLength(50);
                b.Property(sa => sa.FloorNumber).HasMaxLength(100);

                b.HasOne(sa => sa.Schedule)
                    .WithMany(s => s.ScheduleAssignments)
                    .HasForeignKey(sa => sa.ScheduleId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(sa => sa.Employee)
                    .WithMany(e => e.ScheduleAssignments)
                    .HasForeignKey(sa => sa.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(sa => sa.Group)
                    .WithMany(g => g.ScheduleAssignments)
                    .HasForeignKey(sa => sa.GroupId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(sa => new { sa.ScheduleId, sa.EmployeeId, sa.EffectiveFrom });
                b.HasIndex(sa => new { sa.ScheduleId, sa.GroupId, sa.EffectiveFrom });
            });

            // Workflow Configuration
            builder.Entity<Workflow>(b =>
            {
                b.ToTable("Workflows");
                b.ConfigureByConvention();

                b.Property(w => w.Name).IsRequired().HasMaxLength(256);
                b.Property(w => w.Description).HasMaxLength(1000);

                b.HasIndex(w => w.Name);
            });

            // WorkflowStep Configuration
            builder.Entity<WorkflowStep>(b =>
            {
                b.ToTable("WorkflowSteps");
                b.ConfigureByConvention();

                b.HasOne(ws => ws.Workflow)
                    .WithMany(w => w.WorkflowSteps)
                    .HasForeignKey(ws => ws.WorkflowId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(ws => ws.ApproverEmployee)
                    .WithMany()
                    .HasForeignKey(ws => ws.ApproverEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(ws => new { ws.WorkflowId, ws.StepOrder }).IsUnique();
            });

            // ExceptionRequest Configuration
            builder.Entity<ExceptionRequest>(b =>
            {
                b.ToTable("ExceptionRequests");
                b.ConfigureByConvention();

                b.Property(er => er.Reason).IsRequired().HasMaxLength(2000);

                b.HasOne(er => er.Employee)
                    .WithMany(e => e.ExceptionRequests)
                    .HasForeignKey(er => er.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(er => er.Workflow)
                    .WithMany(w => w.ExceptionRequests)
                    .HasForeignKey(er => er.WorkflowId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(er => new { er.EmployeeId, er.ExceptionDate });
                b.HasIndex(er => er.Status);
                b.HasIndex(er => er.Type);
            });

            // ExceptionRequestApprovalHistory Configuration
            builder.Entity<ExceptionRequestApprovalHistory>(b =>
            {
                b.ToTable("ExceptionRequestApprovalHistories");
                b.ConfigureByConvention();

                b.Property(erah => erah.Notes).HasMaxLength(2000);

                b.HasOne(erah => erah.ExceptionRequest)
                    .WithMany(er => er.ApprovalHistories)
                    .HasForeignKey(erah => erah.ExceptionRequestId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(erah => erah.WorkflowStep)
                    .WithMany(ws => ws.ApprovalHistories)
                    .HasForeignKey(erah => erah.WorkflowStepId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(erah => erah.ApproverEmployee)
                    .WithMany()
                    .HasForeignKey(erah => erah.ApproverEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false); // Allow null for external doctors

                b.HasIndex(erah => new { erah.ExceptionRequestId, erah.StepOrder });
            });

            // ExceptionRequestAttachment Configuration
            builder.Entity<ExceptionRequestAttachment>(b =>
            {
                b.ToTable("ExceptionRequestAttachments");
                b.ConfigureByConvention();

                b.Property(era => era.FileName).IsRequired().HasMaxLength(512);
                b.Property(era => era.FilePath).IsRequired().HasMaxLength(1024);
                b.Property(era => era.ContentType).HasMaxLength(256);

                b.HasOne(era => era.ExceptionRequest)
                    .WithMany(er => er.Attachments)
                    .HasForeignKey(era => era.ExceptionRequestId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(era => era.ExceptionRequestId);
            });
        }
    }
}
