using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Models.Entities;

namespace MyHotel.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // Core
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<UserHotelRole> UserHotelRoles => Set<UserHotelRole>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<UserDepartment> UserDepartments => Set<UserDepartment>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();

    // Operations
    public DbSet<Log> Logs => Set<Log>();
    public DbSet<PassLog> PassLogs => Set<PassLog>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomNotice> RoomNotices => Set<RoomNotice>();
    public DbSet<DailyCheck> DailyChecks => Set<DailyCheck>();
    public DbSet<InspectionTemplate> InspectionTemplates => Set<InspectionTemplate>();
    public DbSet<LostFound> LostFoundItems => Set<LostFound>();
    public DbSet<PreventiveMaintenance> PreventiveMaintenances => Set<PreventiveMaintenance>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Message> Messages => Set<Message>();

    // Scheduling & HR
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingAttendee> MeetingAttendees => Set<MeetingAttendee>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<HousekeepingRating> HousekeepingRatings => Set<HousekeepingRating>();

    // Inventory
    public DbSet<InventoryCategory> InventoryCategories => Set<InventoryCategory>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    // New modules
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<GroupSale> GroupSales => Set<GroupSale>();
    public DbSet<CashReport> CashReports => Set<CashReport>();
    public DbSet<CleaningRequest> CleaningRequests => Set<CleaningRequest>();

    // Finance
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // UserHotelRole
        builder.Entity<UserHotelRole>()
            .HasIndex(x => new { x.UserId, x.HotelId, x.Role }).IsUnique();

        // UserDepartment
        builder.Entity<UserDepartment>()
            .HasIndex(x => new { x.UserId, x.DepartmentId }).IsUnique();

        // Evaluation - multiple FK to ApplicationUser
        builder.Entity<Evaluation>()
            .HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Evaluation>()
            .HasOne(e => e.Evaluator)
            .WithMany()
            .HasForeignKey(e => e.EvaluatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // HousekeepingRating - multiple FK to ApplicationUser
        builder.Entity<HousekeepingRating>()
            .HasOne(h => h.Housekeeper)
            .WithMany()
            .HasForeignKey(h => h.HousekeeperId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<HousekeepingRating>()
            .HasOne(h => h.RatedBy)
            .WithMany()
            .HasForeignKey(h => h.RatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Message - multiple FK to ApplicationUser
        builder.Entity<Message>()
            .HasOne(m => m.FromUser)
            .WithMany()
            .HasForeignKey(m => m.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>()
            .HasOne(m => m.ToUser)
            .WithMany()
            .HasForeignKey(m => m.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Schedule
        builder.Entity<Schedule>()
            .HasOne(s => s.Employee)
            .WithMany()
            .HasForeignKey(s => s.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // MeetingAttendee
        builder.Entity<MeetingAttendee>()
            .HasIndex(x => new { x.MeetingId, x.UserId }).IsUnique();

        // InventoryTransaction
        builder.Entity<InventoryTransaction>()
            .HasOne(t => t.Item)
            .WithMany(i => i.Transactions)
            .HasForeignKey(t => t.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed default permissions
        builder.Entity<Permission>().HasData(
            new Permission { Id = 1, Name = "logs.view", Module = "Logs", Description = "View logs" },
            new Permission { Id = 2, Name = "logs.create", Module = "Logs", Description = "Create logs" },
            new Permission { Id = 3, Name = "logs.delete", Module = "Logs", Description = "Delete logs" },
            new Permission { Id = 4, Name = "passlogs.view", Module = "Pass Logs", Description = "View pass logs" },
            new Permission { Id = 5, Name = "passlogs.create", Module = "Pass Logs", Description = "Create pass logs" },
            new Permission { Id = 6, Name = "workorders.view", Module = "Work Orders", Description = "View work orders" },
            new Permission { Id = 7, Name = "workorders.create", Module = "Work Orders", Description = "Create work orders" },
            new Permission { Id = 8, Name = "workorders.assign", Module = "Work Orders", Description = "Assign work orders" },
            new Permission { Id = 9, Name = "workorders.delete", Module = "Work Orders", Description = "Delete work orders" },
            new Permission { Id = 10, Name = "complaints.view", Module = "Complaints", Description = "View complaints" },
            new Permission { Id = 11, Name = "complaints.create", Module = "Complaints", Description = "Create complaints" },
            new Permission { Id = 12, Name = "complaints.manage", Module = "Complaints", Description = "Manage complaints" },
            new Permission { Id = 13, Name = "rooms.view", Module = "Rooms", Description = "View rooms" },
            new Permission { Id = 14, Name = "rooms.manage", Module = "Rooms", Description = "Manage rooms" },
            new Permission { Id = 15, Name = "dailychecks.view", Module = "Daily Checks", Description = "View daily checks" },
            new Permission { Id = 16, Name = "dailychecks.manage", Module = "Daily Checks", Description = "Manage daily checks" },
            new Permission { Id = 17, Name = "lostfound.view", Module = "Lost & Found", Description = "View lost & found" },
            new Permission { Id = 18, Name = "lostfound.manage", Module = "Lost & Found", Description = "Manage lost & found" },
            new Permission { Id = 19, Name = "maintenance.view", Module = "Maintenance", Description = "View maintenance" },
            new Permission { Id = 20, Name = "maintenance.manage", Module = "Maintenance", Description = "Manage maintenance" },
            new Permission { Id = 21, Name = "readings.view", Module = "Readings", Description = "View readings" },
            new Permission { Id = 22, Name = "readings.create", Module = "Readings", Description = "Create readings" },
            new Permission { Id = 23, Name = "assets.view", Module = "Assets", Description = "View assets" },
            new Permission { Id = 24, Name = "assets.manage", Module = "Assets", Description = "Manage assets" },
            new Permission { Id = 25, Name = "messaging.view", Module = "Messaging", Description = "View messages" },
            new Permission { Id = 26, Name = "messaging.send", Module = "Messaging", Description = "Send messages" },
            new Permission { Id = 27, Name = "scheduling.view", Module = "Scheduling", Description = "View schedules" },
            new Permission { Id = 28, Name = "scheduling.manage", Module = "Scheduling", Description = "Manage schedules" },
            new Permission { Id = 29, Name = "inventory.view", Module = "Inventory", Description = "View inventory" },
            new Permission { Id = 30, Name = "inventory.manage", Module = "Inventory", Description = "Manage inventory" },
            new Permission { Id = 31, Name = "meetings.view", Module = "Meetings", Description = "View meetings" },
            new Permission { Id = 32, Name = "meetings.manage", Module = "Meetings", Description = "Manage meetings" },
            new Permission { Id = 33, Name = "evaluations.view", Module = "Evaluations", Description = "View evaluations" },
            new Permission { Id = 34, Name = "evaluations.manage", Module = "Evaluations", Description = "Manage evaluations" },
            new Permission { Id = 35, Name = "reports.view", Module = "Reports", Description = "View reports" },
            new Permission { Id = 36, Name = "budget.view", Module = "Budget", Description = "View budget" },
            new Permission { Id = 37, Name = "budget.manage", Module = "Budget", Description = "Manage budget" },
            new Permission { Id = 38, Name = "directory.view", Module = "Directory", Description = "View directory" },
            new Permission { Id = 39, Name = "directory.manage", Module = "Directory", Description = "Manage directory" },
            new Permission { Id = 40, Name = "hotel.manage", Module = "Admin", Description = "Manage hotel settings" },
            new Permission { Id = 41, Name = "users.manage", Module = "Admin", Description = "Manage users and roles" },
            new Permission { Id = 42, Name = "hkratings.view", Module = "HK Ratings", Description = "View HK ratings" },
            new Permission { Id = 43, Name = "hkratings.rate", Module = "HK Ratings", Description = "Rate housekeepers" }
        );
    }
}
