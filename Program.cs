using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=myhotel.db"));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedAccount = false;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HotelContextService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<ImpersonationService>();

// MVC with global CSRF protection
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

// Auto-migrate and seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Seed admin user if none exists
    if (!await userManager.Users.AnyAsync())
    {
        // ── Admin ──
        var admin = new ApplicationUser
        {
            UserName = "admin@myhotel.com",
            Email = "admin@myhotel.com",
            FirstName = "System",
            LastName = "Admin",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(admin, "admin123");

        // ── Hotels ──
        var aloft = new Hotel { Name = "Aloft Austin Round Rock", Address = "2952 Sun Chase Blvd", City = "Round Rock", State = "TX", Phone = "(512) 555-0100" };
        var element = new Hotel { Name = "Element Hotel", Address = "1500 Innovation Blvd", City = "Round Rock", State = "TX", Phone = "(512) 555-0200" };
        db.Hotels.AddRange(aloft, element);
        await db.SaveChangesAsync();

        // Admin gets access to both hotels
        db.UserHotelRoles.Add(new UserHotelRole { UserId = admin.Id, HotelId = aloft.Id, Role = AppRole.Admin });
        db.UserHotelRoles.Add(new UserHotelRole { UserId = admin.Id, HotelId = element.Id, Role = AppRole.Admin });

        // ── Departments for both hotels ──
        var deptNames = new[] { "Front Desk", "Housekeeping", "Maintenance", "Security", "Food & Beverage" };
        var aloftDepts = new Dictionary<string, Department>();
        var elementDepts = new Dictionary<string, Department>();
        foreach (var name in deptNames)
        {
            var d1 = new Department { Name = name, HotelId = aloft.Id };
            var d2 = new Department { Name = name, HotelId = element.Id };
            db.Departments.Add(d1);
            db.Departments.Add(d2);
            aloftDepts[name] = d1;
            elementDepts[name] = d2;
        }
        await db.SaveChangesAsync();

        // ── GM: Jaime Guerrero (manages both hotels) ──
        var jaime = new ApplicationUser
        {
            UserName = "jaime@myhotel.com",
            Email = "jaime@myhotel.com",
            FirstName = "Jaime",
            LastName = "Guerrero",
            Phone = "5125550001",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(jaime, "jaime123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = jaime.Id, HotelId = aloft.Id, Role = AppRole.GeneralManager });
        db.UserHotelRoles.Add(new UserHotelRole { UserId = jaime.Id, HotelId = element.Id, Role = AppRole.GeneralManager });

        // ── AGM: Sofia Martinez (Aloft) ──
        var sofia = new ApplicationUser
        {
            UserName = "sofia@myhotel.com",
            Email = "sofia@myhotel.com",
            FirstName = "Sofia",
            LastName = "Martinez",
            Phone = "5125550002",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(sofia, "sofia123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = sofia.Id, HotelId = aloft.Id, Role = AppRole.AssistantGM });

        // ── AGM: Daniel Rivera (Element) ──
        var daniel = new ApplicationUser
        {
            UserName = "daniel@myhotel.com",
            Email = "daniel@myhotel.com",
            FirstName = "Daniel",
            LastName = "Rivera",
            Phone = "5125550003",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(daniel, "daniel123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = daniel.Id, HotelId = element.Id, Role = AppRole.AssistantGM });

        // ═══════════════════════════════════
        // ── ALOFT AUSTIN ROUND ROCK STAFF ──
        // ═══════════════════════════════════

        // Front Desk Manager: Maria Lopez
        var maria = new ApplicationUser { UserName = "maria@myhotel.com", Email = "maria@myhotel.com", FirstName = "Maria", LastName = "Lopez", Phone = "5125550010", EmailConfirmed = true };
        await userManager.CreateAsync(maria, "maria123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = maria.Id, HotelId = aloft.Id, Role = AppRole.DepartmentManager });
        db.UserDepartments.Add(new UserDepartment { UserId = maria.Id, DepartmentId = aloftDepts["Front Desk"].Id, IsManager = true });

        // Front Desk Employees
        var carlos = new ApplicationUser { UserName = "carlos@myhotel.com", Email = "carlos@myhotel.com", FirstName = "Carlos", LastName = "Hernandez", Phone = "5125550011", EmailConfirmed = true };
        await userManager.CreateAsync(carlos, "carlos123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = carlos.Id, HotelId = aloft.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = carlos.Id, DepartmentId = aloftDepts["Front Desk"].Id, IsManager = false });

        var jessica = new ApplicationUser { UserName = "jessica@myhotel.com", Email = "jessica@myhotel.com", FirstName = "Jessica", LastName = "Nguyen", Phone = "5125550012", EmailConfirmed = true };
        await userManager.CreateAsync(jessica, "jess123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = jessica.Id, HotelId = aloft.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = jessica.Id, DepartmentId = aloftDepts["Front Desk"].Id, IsManager = false });

        // Housekeeping Manager: Rosa Garcia
        var rosa = new ApplicationUser { UserName = "rosa@myhotel.com", Email = "rosa@myhotel.com", FirstName = "Rosa", LastName = "Garcia", Phone = "5125550020", EmailConfirmed = true };
        await userManager.CreateAsync(rosa, "rosa123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = rosa.Id, HotelId = aloft.Id, Role = AppRole.DepartmentManager });
        db.UserDepartments.Add(new UserDepartment { UserId = rosa.Id, DepartmentId = aloftDepts["Housekeeping"].Id, IsManager = true });

        // Housekeeping Employees
        var lupita = new ApplicationUser { UserName = "lupita@myhotel.com", Email = "lupita@myhotel.com", FirstName = "Lupita", LastName = "Ramirez", Phone = "5125550021", EmailConfirmed = true };
        await userManager.CreateAsync(lupita, "lupita123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = lupita.Id, HotelId = aloft.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = lupita.Id, DepartmentId = aloftDepts["Housekeeping"].Id, IsManager = false });

        var ana = new ApplicationUser { UserName = "ana@myhotel.com", Email = "ana@myhotel.com", FirstName = "Ana", LastName = "Torres", Phone = "5125550022", EmailConfirmed = true };
        await userManager.CreateAsync(ana, "ana123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = ana.Id, HotelId = aloft.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = ana.Id, DepartmentId = aloftDepts["Housekeeping"].Id, IsManager = false });

        var elena = new ApplicationUser { UserName = "elena@myhotel.com", Email = "elena@myhotel.com", FirstName = "Elena", LastName = "Vasquez", Phone = "5125550023", EmailConfirmed = true };
        await userManager.CreateAsync(elena, "elena123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = elena.Id, HotelId = aloft.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = elena.Id, DepartmentId = aloftDepts["Housekeeping"].Id, IsManager = false });

        // Maintenance Manager: Roberto Morales
        var roberto = new ApplicationUser { UserName = "roberto@myhotel.com", Email = "roberto@myhotel.com", FirstName = "Roberto", LastName = "Morales", Phone = "5125550030", EmailConfirmed = true };
        await userManager.CreateAsync(roberto, "roberto123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = roberto.Id, HotelId = aloft.Id, Role = AppRole.DepartmentManager });
        db.UserDepartments.Add(new UserDepartment { UserId = roberto.Id, DepartmentId = aloftDepts["Maintenance"].Id, IsManager = true });

        // Maintenance Employees
        var miguel = new ApplicationUser { UserName = "miguel@myhotel.com", Email = "miguel@myhotel.com", FirstName = "Miguel", LastName = "Castillo", Phone = "5125550031", EmailConfirmed = true };
        await userManager.CreateAsync(miguel, "miguel123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = miguel.Id, HotelId = aloft.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = miguel.Id, DepartmentId = aloftDepts["Maintenance"].Id, IsManager = false });

        // Security Employee: David Reyes
        var david = new ApplicationUser { UserName = "david@myhotel.com", Email = "david@myhotel.com", FirstName = "David", LastName = "Reyes", Phone = "5125550040", EmailConfirmed = true };
        await userManager.CreateAsync(david, "david123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = david.Id, HotelId = aloft.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = david.Id, DepartmentId = aloftDepts["Security"].Id, IsManager = false });

        // ══════════════════════════
        // ── ELEMENT HOTEL STAFF ──
        // ══════════════════════════

        // Front Desk Manager: Patricia Flores
        var patricia = new ApplicationUser { UserName = "patricia@myhotel.com", Email = "patricia@myhotel.com", FirstName = "Patricia", LastName = "Flores", Phone = "5125550110", EmailConfirmed = true };
        await userManager.CreateAsync(patricia, "patricia123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = patricia.Id, HotelId = element.Id, Role = AppRole.DepartmentManager });
        db.UserDepartments.Add(new UserDepartment { UserId = patricia.Id, DepartmentId = elementDepts["Front Desk"].Id, IsManager = true });

        // Front Desk Employees
        var kevin = new ApplicationUser { UserName = "kevin@myhotel.com", Email = "kevin@myhotel.com", FirstName = "Kevin", LastName = "Johnson", Phone = "5125550111", EmailConfirmed = true };
        await userManager.CreateAsync(kevin, "kevin123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = kevin.Id, HotelId = element.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = kevin.Id, DepartmentId = elementDepts["Front Desk"].Id, IsManager = false });

        var samantha = new ApplicationUser { UserName = "samantha@myhotel.com", Email = "samantha@myhotel.com", FirstName = "Samantha", LastName = "Williams", Phone = "5125550112", EmailConfirmed = true };
        await userManager.CreateAsync(samantha, "sam123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = samantha.Id, HotelId = element.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = samantha.Id, DepartmentId = elementDepts["Front Desk"].Id, IsManager = false });

        // Housekeeping Manager: Carmen Delgado
        var carmen = new ApplicationUser { UserName = "carmen@myhotel.com", Email = "carmen@myhotel.com", FirstName = "Carmen", LastName = "Delgado", Phone = "5125550120", EmailConfirmed = true };
        await userManager.CreateAsync(carmen, "carmen123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = carmen.Id, HotelId = element.Id, Role = AppRole.DepartmentManager });
        db.UserDepartments.Add(new UserDepartment { UserId = carmen.Id, DepartmentId = elementDepts["Housekeeping"].Id, IsManager = true });

        // Housekeeping Employees
        var guadalupe = new ApplicationUser { UserName = "guadalupe@myhotel.com", Email = "guadalupe@myhotel.com", FirstName = "Guadalupe", LastName = "Mendoza", Phone = "5125550121", EmailConfirmed = true };
        await userManager.CreateAsync(guadalupe, "guada123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = guadalupe.Id, HotelId = element.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = guadalupe.Id, DepartmentId = elementDepts["Housekeeping"].Id, IsManager = false });

        var isabela = new ApplicationUser { UserName = "isabela@myhotel.com", Email = "isabela@myhotel.com", FirstName = "Isabela", LastName = "Cruz", Phone = "5125550122", EmailConfirmed = true };
        await userManager.CreateAsync(isabela, "isabela123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = isabela.Id, HotelId = element.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = isabela.Id, DepartmentId = elementDepts["Housekeeping"].Id, IsManager = false });

        // Maintenance Manager: Fernando Ortiz
        var fernando = new ApplicationUser { UserName = "fernando@myhotel.com", Email = "fernando@myhotel.com", FirstName = "Fernando", LastName = "Ortiz", Phone = "5125550130", EmailConfirmed = true };
        await userManager.CreateAsync(fernando, "fernando123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = fernando.Id, HotelId = element.Id, Role = AppRole.DepartmentManager });
        db.UserDepartments.Add(new UserDepartment { UserId = fernando.Id, DepartmentId = elementDepts["Maintenance"].Id, IsManager = true });

        // Maintenance Employee
        var jorge = new ApplicationUser { UserName = "jorge@myhotel.com", Email = "jorge@myhotel.com", FirstName = "Jorge", LastName = "Sandoval", Phone = "5125550131", EmailConfirmed = true };
        await userManager.CreateAsync(jorge, "jorge123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = jorge.Id, HotelId = element.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = jorge.Id, DepartmentId = elementDepts["Maintenance"].Id, IsManager = false });

        // Security Employee: Alex Thompson
        var alex = new ApplicationUser { UserName = "alex@myhotel.com", Email = "alex@myhotel.com", FirstName = "Alex", LastName = "Thompson", Phone = "5125550140", EmailConfirmed = true };
        await userManager.CreateAsync(alex, "alex123");
        db.UserHotelRoles.Add(new UserHotelRole { UserId = alex.Id, HotelId = element.Id, Role = AppRole.Employee });
        db.UserDepartments.Add(new UserDepartment { UserId = alex.Id, DepartmentId = elementDepts["Security"].Id, IsManager = false });

        await db.SaveChangesAsync();

        // ╔══════════════════════════════════════════╗
        // ║  SEED OPERATIONAL DATA FOR BOTH HOTELS   ║
        // ╚══════════════════════════════════════════╝

        var today = DateTime.UtcNow;
        var todayStr = today.ToString("yyyy-MM-dd");
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        // ── ROOMS ──
        // Aloft: 4 floors, 10 rooms each
        var roomStatuses = new[] { "vacant-clean", "occupied", "vacant-dirty", "occupied", "vacant-clean", "occupied", "inspected", "occupied", "vacant-clean", "out-of-order" };
        var roomTypes = new[] { "standard", "standard", "standard", "deluxe", "standard", "suite", "standard", "deluxe", "accessible", "standard" };
        for (int floor = 1; floor <= 4; floor++)
        {
            for (int r = 1; r <= 10; r++)
            {
                var idx = r - 1;
                db.Rooms.Add(new Room { HotelId = aloft.Id, Number = $"{floor}{r:D2}", Floor = floor, Type = roomTypes[idx], Status = roomStatuses[idx] });
            }
        }
        // Element: 3 floors, 8 rooms each
        for (int floor = 1; floor <= 3; floor++)
        {
            for (int r = 1; r <= 8; r++)
            {
                var idx = r % roomStatuses.Length;
                db.Rooms.Add(new Room { HotelId = element.Id, Number = $"{floor}{r:D2}", Floor = floor, Type = roomTypes[idx], Status = roomStatuses[idx] });
            }
        }
        await db.SaveChangesAsync();

        // Add some room notices
        var aloftRooms = await db.Rooms.Where(r => r.HotelId == aloft.Id).ToListAsync();
        db.RoomNotices.Add(new RoomNotice { RoomId = aloftRooms.First(r => r.Number == "204").Id, Type = "VIP", Note = "Gold member, extra towels requested" });
        db.RoomNotices.Add(new RoomNotice { RoomId = aloftRooms.First(r => r.Number == "306").Id, Type = "late-checkout", Note = "Late checkout approved until 2 PM" });
        db.RoomNotices.Add(new RoomNotice { RoomId = aloftRooms.First(r => r.Number == "108").Id, Type = "DND", Note = "Do not disturb since yesterday" });
        db.RoomNotices.Add(new RoomNotice { RoomId = aloftRooms.First(r => r.Number == "401").Id, Type = "early-check-in", Note = "Arriving at 10 AM" });

        // ── WORK ORDERS ──
        db.WorkOrders.AddRange(
            new WorkOrder { HotelId = aloft.Id, Room = "110", Description = "TV not turning on, guest reported remote also not working", Priority = "high", Status = "pending", AssignedTo = "Miguel Castillo", CreatedBy = "Maria Lopez", CreatedAt = today.AddHours(-3) },
            new WorkOrder { HotelId = aloft.Id, Room = "205", Description = "Bathroom faucet leaking, slow drip", Priority = "normal", Status = "in-progress", AssignedTo = "Miguel Castillo", CreatedBy = "Carlos Hernandez", Notes = $"[{today.AddHours(-1):g}] Checked faucet, need new washer. Ordered part.", CreatedAt = yesterday },
            new WorkOrder { HotelId = aloft.Id, Room = "302", Description = "AC making loud noise, guest cannot sleep", Priority = "urgent", Status = "pending", AssignedTo = "Roberto Morales", CreatedBy = "Jessica Nguyen", CreatedAt = today.AddHours(-1) },
            new WorkOrder { HotelId = aloft.Id, Room = "108", Description = "Replace burnt out light bulb in bathroom", Priority = "low", Status = "completed", AssignedTo = "Miguel Castillo", CreatedBy = "Rosa Garcia", CompletedAt = yesterday, CreatedAt = twoDaysAgo },
            new WorkOrder { HotelId = aloft.Id, Room = "Lobby", Description = "Front entrance door not closing properly", Priority = "high", Status = "in-progress", AssignedTo = "Roberto Morales", CreatedBy = "Sofia Martinez", Notes = $"[{today.AddHours(-2):g}] Hinge is loose, tightening screws.", CreatedAt = yesterday },
            new WorkOrder { HotelId = aloft.Id, Room = "Pool", Description = "Pool heater not maintaining temperature", Priority = "normal", Status = "pending", CreatedBy = "David Reyes", CreatedAt = today.AddHours(-5) },
            new WorkOrder { HotelId = aloft.Id, Room = "404", Description = "Toilet running continuously", Priority = "high", Status = "completed", AssignedTo = "Miguel Castillo", CreatedBy = "Maria Lopez", CompletedAt = today.AddHours(-2), CreatedAt = yesterday, Notes = $"[{today.AddHours(-2):g}] Replaced flapper valve." },
            new WorkOrder { HotelId = element.Id, Room = "201", Description = "Mini fridge not cooling", Priority = "high", Status = "pending", AssignedTo = "Jorge Sandoval", CreatedBy = "Patricia Flores", CreatedAt = today.AddHours(-2) },
            new WorkOrder { HotelId = element.Id, Room = "105", Description = "Shower head needs replacement", Priority = "normal", Status = "in-progress", AssignedTo = "Fernando Ortiz", CreatedBy = "Kevin Johnson", CreatedAt = yesterday },
            new WorkOrder { HotelId = element.Id, Room = "Gym", Description = "Treadmill belt slipping", Priority = "low", Status = "pending", CreatedBy = "Daniel Rivera", CreatedAt = today.AddHours(-4) },
            new WorkOrder { HotelId = element.Id, Room = "302", Description = "Safe not opening with guest code", Priority = "urgent", Status = "completed", AssignedTo = "Fernando Ortiz", CreatedBy = "Samantha Williams", CompletedAt = today.AddHours(-1), CreatedAt = today.AddHours(-3) }
        );

        // ── COMPLAINTS ──
        db.Complaints.AddRange(
            new Complaint { HotelId = aloft.Id, GuestName = "Robert Smith", Room = "302", Description = "AC extremely loud, could not sleep all night. Requesting room change or compensation.", Status = "open", AssignedTo = "Sofia Martinez", CreatedBy = "Carlos Hernandez", IsEscalated = true, CreatedAt = today.AddHours(-2) },
            new Complaint { HotelId = aloft.Id, GuestName = "Jennifer Park", Room = "205", Description = "Water stain on ceiling, room smells musty", Status = "in-progress", AssignedTo = "Rosa Garcia", CreatedBy = "Maria Lopez", CreatedAt = yesterday },
            new Complaint { HotelId = aloft.Id, GuestName = "David Wilson", Room = "108", Description = "Noisy neighbors in room 109, called front desk twice", Status = "resolved", Resolution = "Spoke with guests in 109, moved Mr. Wilson to room 210", Satisfaction = 4, CreatedBy = "Jessica Nguyen", ResolvedAt = yesterday.AddHours(2), CreatedAt = twoDaysAgo },
            new Complaint { HotelId = aloft.Id, GuestName = "Maria Santos", Room = "401", Description = "Room was not ready at check-in time despite early check-in request", Status = "resolved", Resolution = "Upgraded to suite 406, complimentary breakfast", Satisfaction = 5, CompensationNotes = "Free breakfast voucher x2", CreatedBy = "Maria Lopez", ResolvedAt = yesterday, CreatedAt = yesterday.AddHours(-3) },
            new Complaint { HotelId = element.Id, GuestName = "Thomas Brown", Room = "201", Description = "Mini fridge not working, food spoiled", Status = "open", AssignedTo = "Patricia Flores", CreatedBy = "Kevin Johnson", CompensationNotes = "Offered $50 credit for spoiled groceries", CreatedAt = today.AddHours(-3) },
            new Complaint { HotelId = element.Id, GuestName = "Lisa Chen", Room = "108", Description = "Found hair in the bathroom, room not properly cleaned", Status = "in-progress", AssignedTo = "Carmen Delgado", CreatedBy = "Samantha Williams", CreatedAt = today.AddHours(-5) }
        );

        // ── LOGS ──
        db.Logs.AddRange(
            new Log { HotelId = aloft.Id, Category = "front-desk", Message = "Guest Robert Smith (room 302) complained about AC noise. Logged work order #3. Offered room change but guest prefers to stay. Escalated to AGM.", CreatedBy = "Carlos Hernandez", CreatedAt = today.AddHours(-2) },
            new Log { HotelId = aloft.Id, Category = "security", Message = "Fire alarm test completed on all floors. All alarms functioning properly. Elevator recall test passed.", CreatedBy = "David Reyes", IsRead = true, ReadBy = "Sofia Martinez", ReadAt = today.AddHours(-1), CreatedAt = today.AddHours(-4) },
            new Log { HotelId = aloft.Id, Category = "housekeeping", Message = "Deep cleaning completed on floor 3. All rooms inspected and restocked. One mattress needs replacement in 308.", CreatedBy = "Rosa Garcia", CreatedAt = today.AddHours(-5) },
            new Log { HotelId = aloft.Id, Category = "maintenance", Message = "Pool heater inspection - thermostat reading 78F, target is 82F. Adjusting. Filter pressure at 18 PSI.", CreatedBy = "Roberto Morales", CreatedAt = today.AddHours(-6) },
            new Log { HotelId = aloft.Id, Category = "cash-count", Message = "AM shift cash count: Starting $200.00, Ending $347.50. Credit card transactions: $2,450.00. No discrepancies.", CreatedBy = "Maria Lopez", IsRead = true, ReadBy = "Jessica Nguyen", ReadAt = today.AddHours(-1), CreatedAt = today.AddHours(-3) },
            new Log { HotelId = aloft.Id, Category = "key-audit", Message = "Key audit complete. All master keys accounted for. 3 guest keys deactivated for checkouts: 102, 207, 310.", CreatedBy = "Carlos Hernandez", CreatedAt = today.AddHours(-7) },
            new Log { HotelId = aloft.Id, Category = "wake-up-call", Message = "Wake-up calls scheduled: Room 204 at 5:30 AM, Room 306 at 6:00 AM, Room 401 at 7:00 AM.", CreatedBy = "Jessica Nguyen", IsRead = true, ReadBy = "Carlos Hernandez", ReadAt = today.AddHours(-6), CreatedAt = yesterday.AddHours(18) },
            new Log { HotelId = aloft.Id, Category = "general", Message = "Marriott Bonvoy inspection scheduled for next Tuesday. All departments need to review brand standards checklist.", CreatedBy = "Jaime Guerrero", CreatedAt = yesterday },
            new Log { HotelId = aloft.Id, Category = "guest-shipment", Message = "Package received for guest in room 204 (Robert Garcia). FedEx tracking #789456123. Stored in back office.", CreatedBy = "Jessica Nguyen", CreatedAt = today.AddHours(-4) },
            new Log { HotelId = element.Id, Category = "front-desk", Message = "Guest Lisa Chen (room 108) reported room cleanliness issue. HK manager notified. Room re-cleaned and inspected.", CreatedBy = "Kevin Johnson", CreatedAt = today.AddHours(-5) },
            new Log { HotelId = element.Id, Category = "security", Message = "Parking lot camera #3 offline. IT contacted. Temporary patrol coverage assigned to Alex.", CreatedBy = "Alex Thompson", CreatedAt = today.AddHours(-3) },
            new Log { HotelId = element.Id, Category = "maintenance", Message = "Gym treadmill belt slipping. Placed out-of-order sign. Parts on order, ETA 2 business days.", CreatedBy = "Fernando Ortiz", CreatedAt = today.AddHours(-4) },
            new Log { HotelId = element.Id, Category = "housekeeping", Message = "Inventory check: Low on king-size sheets (only 8 sets remaining). Order placed with vendor.", CreatedBy = "Carmen Delgado", CreatedAt = today.AddHours(-6) }
        );

        // ── PASS LOGS ──
        db.PassLogs.AddRange(
            new PassLog { HotelId = aloft.Id, ShiftFrom = "Night", ShiftTo = "AM", Message = "Quiet night. Guest in 302 called at 2 AM about AC - logged work order. Pool heater alarm went off at 4 AM, reset it. VIP arriving today room 204.", Priority = "high", CreatedBy = "Jessica Nguyen", IsRead = true, ReadBy = "Carlos Hernandez", ReadAt = today.AddHours(-7), CreatedAt = today.AddHours(-8) },
            new PassLog { HotelId = aloft.Id, ShiftFrom = "AM", ShiftTo = "PM", Message = "Busy morning. 8 checkouts processed, 5 check-ins. Work order for lobby door still in progress. Brand inspection next Tuesday - start prepping. Cash count balanced.", Priority = "normal", CreatedBy = "Carlos Hernandez", CreatedAt = today.AddHours(-2) },
            new PassLog { HotelId = aloft.Id, ShiftFrom = "AM", ShiftTo = "PM", Message = "Housekeeping update: Floor 3 deep clean complete. Short staffed tomorrow - Ana called out. Need to redistribute rooms.", Priority = "high", CreatedBy = "Rosa Garcia", CreatedAt = today.AddHours(-3) },
            new PassLog { HotelId = element.Id, ShiftFrom = "Night", ShiftTo = "AM", Message = "Uneventful night. Guest in 201 reported fridge issue at 11 PM, logged WO. All doors secured, parking lot rounds completed every 2 hours.", Priority = "normal", CreatedBy = "Alex Thompson", IsRead = true, ReadBy = "Kevin Johnson", ReadAt = today.AddHours(-6), CreatedAt = today.AddHours(-7) },
            new PassLog { HotelId = element.Id, ShiftFrom = "AM", ShiftTo = "PM", Message = "Camera #3 still down. Guest complaint from 108 resolved - room re-cleaned. 6 check-ins expected this afternoon including a group of 4.", Priority = "normal", CreatedBy = "Kevin Johnson", CreatedAt = today.AddHours(-1) }
        );

        // ── DAILY CHECKS ──
        var aloftChecks = new (string item, string category, bool completed, string? completedBy)[]
        {
            ("Check pool chemical levels", "pool", true, "David Reyes"),
            ("Test pool temperature", "pool", true, "David Reyes"),
            ("Inspect pool area furniture", "pool", false, null),
            ("Vacuum lobby carpet", "lobby", true, "Lupita Ramirez"),
            ("Clean lobby glass doors", "lobby", true, "Lupita Ramirez"),
            ("Check lobby flowers/plants", "lobby", true, "Rosa Garcia"),
            ("Wipe front desk surfaces", "lobby", true, "Ana Torres"),
            ("Test all elevators", "elevator", true, "Roberto Morales"),
            ("Check elevator emergency phones", "elevator", false, null),
            ("Check all exit signs illuminated", "security", true, "David Reyes"),
            ("Test emergency lighting", "security", true, "David Reyes"),
            ("Walk perimeter of property", "security", true, "David Reyes"),
            ("Check parking lot lights", "security", false, null),
            ("Test fire panel", "fire", true, "Roberto Morales"),
            ("Check fire extinguisher tags", "fire", false, null),
            ("Inspect sprinkler heads (random 5)", "fire", false, null),
            ("Check parking lot cleanliness", "parking", true, "Miguel Castillo"),
            ("Verify handicap spots marked", "parking", true, "Miguel Castillo"),
            ("Restock housekeeping carts", "housekeeping", true, "Rosa Garcia"),
            ("Check laundry room equipment", "housekeeping", true, "Elena Vasquez"),
            ("Inspect supply closets", "housekeeping", false, null),
            ("Check ice machines all floors", "general", true, "Miguel Castillo"),
            ("Inspect vending machines", "general", true, "Carlos Hernandez"),
            ("Check business center printer", "general", false, null),
            ("Test WiFi in common areas", "general", true, "Roberto Morales"),
            ("Check AED batteries", "general", false, null),
            ("Review comment cards", "general", false, null),
        };
        foreach (var (item, category, completed, completedBy) in aloftChecks)
        {
            db.DailyChecks.Add(new DailyCheck { HotelId = aloft.Id, CheckItem = item, Category = category, IsCompleted = completed, CompletedBy = completedBy, Date = todayStr, CreatedAt = today.AddHours(-8) });
        }
        // Element checks (fewer)
        var elementChecks = new (string item, string category, bool completed, string? completedBy)[]
        {
            ("Check pool chemical levels", "pool", true, "Alex Thompson"),
            ("Test pool temperature", "pool", true, "Alex Thompson"),
            ("Vacuum lobby carpet", "lobby", true, "Guadalupe Mendoza"),
            ("Clean lobby glass doors", "lobby", true, "Guadalupe Mendoza"),
            ("Test all elevators", "elevator", true, "Fernando Ortiz"),
            ("Check all exit signs illuminated", "security", true, "Alex Thompson"),
            ("Walk perimeter of property", "security", false, null),
            ("Test fire panel", "fire", true, "Fernando Ortiz"),
            ("Check fire extinguisher tags", "fire", false, null),
            ("Restock housekeeping carts", "housekeeping", true, "Carmen Delgado"),
            ("Check laundry room equipment", "housekeeping", false, null),
            ("Check ice machines all floors", "general", true, "Jorge Sandoval"),
            ("Test WiFi in common areas", "general", true, "Fernando Ortiz"),
        };
        foreach (var (item, category, completed, completedBy) in elementChecks)
        {
            db.DailyChecks.Add(new DailyCheck { HotelId = element.Id, CheckItem = item, Category = category, IsCompleted = completed, CompletedBy = completedBy, Date = todayStr, CreatedAt = today.AddHours(-7) });
        }

        // ── INVENTORY ──
        // ALOFT - Market
        var aloftMarket = new InventoryCategory { Name = "Front Desk Market", Type = "market", HotelId = aloft.Id };
        var aloftCleaning = new InventoryCategory { Name = "Cleaning Supplies", Type = "cleaning", HotelId = aloft.Id };
        var aloftMaint = new InventoryCategory { Name = "Maintenance Parts", Type = "maintenance", HotelId = aloft.Id };
        db.InventoryCategories.AddRange(aloftMarket, aloftCleaning, aloftMaint);
        // ELEMENT
        var elemMarket = new InventoryCategory { Name = "Front Desk Market", Type = "market", HotelId = element.Id };
        var elemCleaning = new InventoryCategory { Name = "Cleaning Supplies", Type = "cleaning", HotelId = element.Id };
        var elemMaint = new InventoryCategory { Name = "Maintenance Parts", Type = "maintenance", HotelId = element.Id };
        db.InventoryCategories.AddRange(elemMarket, elemCleaning, elemMaint);
        await db.SaveChangesAsync();

        // Aloft market items
        var marketItems = new (string name, int qty, string unit, int min, decimal cost)[]
        {
            ("Coca-Cola 20oz", 48, "bottles", 12, 1.50m),
            ("Dasani Water 16oz", 72, "bottles", 24, 0.75m),
            ("Lay's Chips Classic", 24, "bags", 8, 1.25m),
            ("Snickers Bar", 18, "bars", 6, 1.50m),
            ("Pringles Original", 15, "cans", 6, 2.00m),
            ("Red Bull 8oz", 20, "cans", 10, 2.50m),
            ("Tylenol 2-pack", 30, "packs", 10, 1.00m),
            ("Toothbrush Kit", 25, "kits", 8, 2.00m),
            ("Razor Kit", 20, "kits", 8, 1.75m),
            ("Phone Charger Lightning", 5, "units", 3, 8.00m),
            ("Phone Charger USB-C", 5, "units", 3, 8.00m),
            ("Colgate Toothpaste", 15, "tubes", 5, 1.50m),
        };
        foreach (var (name, qty, unit, min, cost) in marketItems)
        {
            db.InventoryItems.Add(new InventoryItem { CategoryId = aloftMarket.Id, HotelId = aloft.Id, Name = name, Quantity = qty, Unit = unit, MinStock = min, Cost = cost, Location = "Front Desk Cabinet" });
        }

        // Aloft cleaning items
        var cleaningItems = new (string name, int qty, string unit, int min)[]
        {
            ("All-Purpose Cleaner (gallon)", 8, "gallons", 3),
            ("Glass Cleaner (spray)", 12, "bottles", 4),
            ("Toilet Bowl Cleaner", 15, "bottles", 5),
            ("Bleach (gallon)", 6, "gallons", 2),
            ("Microfiber Cloths", 50, "cloths", 20),
            ("Trash Bags 13gal", 200, "bags", 50),
            ("Trash Bags 33gal", 100, "bags", 30),
            ("Vacuum Bags", 8, "bags", 3),
            ("Mop Heads", 6, "units", 2),
            ("Latex Gloves (box)", 10, "boxes", 3),
            ("King Sheet Sets", 8, "sets", 10),
            ("Queen Sheet Sets", 15, "sets", 10),
            ("Bath Towels", 45, "towels", 20),
            ("Hand Towels", 60, "towels", 25),
            ("Shampoo Bottles", 120, "bottles", 40),
            ("Conditioner Bottles", 120, "bottles", 40),
            ("Body Wash Bottles", 120, "bottles", 40),
            ("Toilet Paper Rolls", 200, "rolls", 80),
        };
        foreach (var (name, qty, unit, min) in cleaningItems)
        {
            db.InventoryItems.Add(new InventoryItem { CategoryId = aloftCleaning.Id, HotelId = aloft.Id, Name = name, Quantity = qty, Unit = unit, MinStock = min, Location = "HK Storage Room" });
        }

        // Aloft maintenance items
        var maintItems = new (string name, int qty, string unit, int min)[]
        {
            ("Light Bulbs LED A19", 25, "bulbs", 10),
            ("Light Bulbs LED BR30", 15, "bulbs", 5),
            ("Toilet Flappers", 8, "units", 3),
            ("Faucet Washers (assorted)", 20, "units", 5),
            ("HVAC Filters 20x25x1", 12, "filters", 4),
            ("Paint - Eggshell White (gallon)", 3, "gallons", 1),
            ("Caulk Tubes (white)", 6, "tubes", 2),
            ("Smoke Detector Batteries 9V", 20, "batteries", 8),
            ("Deadbolt Lock Sets", 3, "units", 1),
            ("Key Card Lock Batteries", 30, "packs", 10),
            ("Plumber's Tape", 10, "rolls", 3),
            ("WD-40", 4, "cans", 2),
        };
        foreach (var (name, qty, unit, min) in maintItems)
        {
            db.InventoryItems.Add(new InventoryItem { CategoryId = aloftMaint.Id, HotelId = aloft.Id, Name = name, Quantity = qty, Unit = unit, MinStock = min, Location = "Maintenance Shop" });
        }

        // Element - simplified inventory
        foreach (var (name, qty, unit, min, cost) in marketItems.Take(8))
            db.InventoryItems.Add(new InventoryItem { CategoryId = elemMarket.Id, HotelId = element.Id, Name = name, Quantity = qty - 5, Unit = unit, MinStock = min, Cost = cost, Location = "Front Desk" });
        foreach (var (name, qty, unit, min) in cleaningItems.Take(12))
            db.InventoryItems.Add(new InventoryItem { CategoryId = elemCleaning.Id, HotelId = element.Id, Name = name, Quantity = qty - 3, Unit = unit, MinStock = min, Location = "HK Closet" });
        foreach (var (name, qty, unit, min) in maintItems.Take(8))
            db.InventoryItems.Add(new InventoryItem { CategoryId = elemMaint.Id, HotelId = element.Id, Name = name, Quantity = qty - 2, Unit = unit, MinStock = min, Location = "Maint Room" });

        await db.SaveChangesAsync();

        // Add some inventory transactions
        var aloftItems = await db.InventoryItems.Where(i => i.HotelId == aloft.Id).ToListAsync();
        var cokeItem = aloftItems.First(i => i.Name.Contains("Coca-Cola"));
        db.InventoryTransactions.AddRange(
            new InventoryTransaction { ItemId = cokeItem.Id, Quantity = 48, Type = "in", Notes = "Weekly Coca-Cola delivery", CreatedBy = "Maria Lopez", CreatedAt = twoDaysAgo },
            new InventoryTransaction { ItemId = cokeItem.Id, Quantity = 6, Type = "out", Notes = "Sold at front desk", CreatedBy = "Carlos Hernandez", CreatedAt = yesterday },
            new InventoryTransaction { ItemId = cokeItem.Id, Quantity = 4, Type = "out", Notes = "Sold at front desk", CreatedBy = "Jessica Nguyen", CreatedAt = today.AddHours(-3) }
        );
        var sheetsItem = aloftItems.First(i => i.Name.Contains("King Sheet"));
        db.InventoryTransactions.AddRange(
            new InventoryTransaction { ItemId = sheetsItem.Id, Quantity = 2, Type = "out", Notes = "Stained sheets replaced rooms 204, 310", CreatedBy = "Rosa Garcia", CreatedAt = yesterday },
            new InventoryTransaction { ItemId = sheetsItem.Id, Quantity = 1, Type = "out", Notes = "Replaced torn sheet room 402", CreatedBy = "Elena Vasquez", CreatedAt = today.AddHours(-4) }
        );

        // ── SCHEDULES (this week Mon-Sun) ──
        var monday = today.Date.AddDays(-(int)today.DayOfWeek + 1);
        if (monday > today.Date) monday = monday.AddDays(-7);

        // Aloft Front Desk schedules
        for (int d = 0; d < 7; d++)
        {
            var day = monday.AddDays(d);
            if (d < 5) // weekdays
            {
                db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Front Desk"].Id, EmployeeId = carlos.Id, Date = day, StartTime = new TimeSpan(7, 0, 0), EndTime = new TimeSpan(15, 0, 0), CreatedBy = "Maria Lopez" });
                db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Front Desk"].Id, EmployeeId = jessica.Id, Date = day, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(23, 0, 0), CreatedBy = "Maria Lopez" });
            }
            else // weekends: swap
            {
                db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Front Desk"].Id, EmployeeId = jessica.Id, Date = day, StartTime = new TimeSpan(7, 0, 0), EndTime = new TimeSpan(15, 0, 0), CreatedBy = "Maria Lopez" });
                db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Front Desk"].Id, EmployeeId = carlos.Id, Date = day, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(23, 0, 0), CreatedBy = "Maria Lopez" });
            }
        }
        // Aloft HK schedules
        for (int d = 0; d < 7; d++)
        {
            var day = monday.AddDays(d);
            if (d < 5)
            {
                db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Housekeeping"].Id, EmployeeId = lupita.Id, Date = day, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), CreatedBy = "Rosa Garcia" });
                db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Housekeeping"].Id, EmployeeId = ana.Id, Date = day, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), CreatedBy = "Rosa Garcia" });
                db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Housekeeping"].Id, EmployeeId = elena.Id, Date = day, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0), CreatedBy = "Rosa Garcia" });
            }
            else if (d == 5) // Sat: lupita + elena
            {
                db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Housekeeping"].Id, EmployeeId = lupita.Id, Date = day, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), CreatedBy = "Rosa Garcia" });
                db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Housekeeping"].Id, EmployeeId = elena.Id, Date = day, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), CreatedBy = "Rosa Garcia" });
            }
            else // Sun: ana + elena
            {
                db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Housekeeping"].Id, EmployeeId = ana.Id, Date = day, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0), CreatedBy = "Rosa Garcia" });
                db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Housekeeping"].Id, EmployeeId = elena.Id, Date = day, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0), CreatedBy = "Rosa Garcia" });
            }
        }
        // Aloft Maintenance
        for (int d = 0; d < 6; d++) // Mon-Sat
        {
            db.Schedules.Add(new Schedule { HotelId = aloft.Id, DepartmentId = aloftDepts["Maintenance"].Id, EmployeeId = miguel.Id, Date = monday.AddDays(d), StartTime = new TimeSpan(7, 0, 0), EndTime = new TimeSpan(15, 30, 0), CreatedBy = "Roberto Morales" });
        }
        // Element FD schedules
        for (int d = 0; d < 7; d++)
        {
            var day = monday.AddDays(d);
            db.Schedules.Add(new Schedule { HotelId = element.Id, DepartmentId = elementDepts["Front Desk"].Id, EmployeeId = kevin.Id, Date = day, StartTime = new TimeSpan(7, 0, 0), EndTime = new TimeSpan(15, 0, 0), CreatedBy = "Patricia Flores" });
            db.Schedules.Add(new Schedule { HotelId = element.Id, DepartmentId = elementDepts["Front Desk"].Id, EmployeeId = samantha.Id, Date = day, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(23, 0, 0), CreatedBy = "Patricia Flores" });
        }

        // ── MEETINGS ──
        var aloftMeeting1 = new Meeting { HotelId = aloft.Id, Title = "Weekly Staff Meeting", Description = "Review weekly operations, upcoming events, brand inspection prep", Date = monday.AddDays(7), Time = new TimeSpan(9, 0, 0), CreatedBy = "Jaime Guerrero", Notes = "Discuss Marriott inspection checklist. Assign responsibilities." };
        var aloftMeeting2 = new Meeting { HotelId = aloft.Id, Title = "Housekeeping Deep Clean Review", Description = "Review deep cleaning schedule and quality standards", Date = monday.AddDays(2), Time = new TimeSpan(8, 0, 0), DepartmentId = aloftDepts["Housekeeping"].Id, CreatedBy = "Rosa Garcia", Notes = "Floor 3 complete. Floor 4 scheduled for Thursday." };
        var elemMeeting1 = new Meeting { HotelId = element.Id, Title = "Monthly Safety Meeting", Description = "Fire drill review, security protocol updates", Date = monday.AddDays(8), Time = new TimeSpan(10, 0, 0), CreatedBy = "Daniel Rivera" };
        db.Meetings.AddRange(aloftMeeting1, aloftMeeting2, elemMeeting1);
        await db.SaveChangesAsync();

        // Attendees
        db.MeetingAttendees.AddRange(
            new MeetingAttendee { MeetingId = aloftMeeting1.Id, UserId = jaime.Id, Attended = false },
            new MeetingAttendee { MeetingId = aloftMeeting1.Id, UserId = sofia.Id, Attended = false },
            new MeetingAttendee { MeetingId = aloftMeeting1.Id, UserId = maria.Id, Attended = false },
            new MeetingAttendee { MeetingId = aloftMeeting1.Id, UserId = rosa.Id, Attended = false },
            new MeetingAttendee { MeetingId = aloftMeeting1.Id, UserId = roberto.Id, Attended = false },
            new MeetingAttendee { MeetingId = aloftMeeting2.Id, UserId = rosa.Id, Attended = true },
            new MeetingAttendee { MeetingId = aloftMeeting2.Id, UserId = lupita.Id, Attended = true },
            new MeetingAttendee { MeetingId = aloftMeeting2.Id, UserId = ana.Id, Attended = true },
            new MeetingAttendee { MeetingId = aloftMeeting2.Id, UserId = elena.Id, Attended = false },
            new MeetingAttendee { MeetingId = elemMeeting1.Id, UserId = daniel.Id, Attended = false },
            new MeetingAttendee { MeetingId = elemMeeting1.Id, UserId = patricia.Id, Attended = false },
            new MeetingAttendee { MeetingId = elemMeeting1.Id, UserId = fernando.Id, Attended = false },
            new MeetingAttendee { MeetingId = elemMeeting1.Id, UserId = alex.Id, Attended = false }
        );

        // ── EVALUATIONS ──
        db.Evaluations.AddRange(
            new Evaluation { HotelId = aloft.Id, EmployeeId = carlos.Id, EvaluatorId = maria.Id, Rating = 4, Comments = "Carlos is very reliable and handles guest issues professionally. Could improve on upselling rooms.", Period = "Q1 2026", CreatedAt = today.AddDays(-15) },
            new Evaluation { HotelId = aloft.Id, EmployeeId = jessica.Id, EvaluatorId = maria.Id, Rating = 5, Comments = "Outstanding performance. Jessica consistently goes above and beyond for guests. Night audit accuracy is 100%.", Period = "Q1 2026", CreatedAt = today.AddDays(-15) },
            new Evaluation { HotelId = aloft.Id, EmployeeId = lupita.Id, EvaluatorId = rosa.Id, Rating = 5, Comments = "Lupita is our fastest and most thorough housekeeper. Rooms are always perfect. Great attitude.", Period = "Q1 2026", CreatedAt = today.AddDays(-10) },
            new Evaluation { HotelId = aloft.Id, EmployeeId = ana.Id, EvaluatorId = rosa.Id, Rating = 3, Comments = "Ana needs to improve on attention to detail. Found uncleaned items in 3 rooms during inspection. Working on improvement plan.", Period = "Q1 2026", CreatedAt = today.AddDays(-10) },
            new Evaluation { HotelId = aloft.Id, EmployeeId = elena.Id, EvaluatorId = rosa.Id, Rating = 4, Comments = "Elena does great work and is always willing to take extra rooms. Slightly slow but very thorough.", Period = "Q1 2026", CreatedAt = today.AddDays(-10) },
            new Evaluation { HotelId = aloft.Id, EmployeeId = miguel.Id, EvaluatorId = roberto.Id, Rating = 4, Comments = "Miguel handles most repairs efficiently. Good problem-solving skills. Need to improve documentation of work completed.", Period = "Q1 2026", CreatedAt = today.AddDays(-12) },
            new Evaluation { HotelId = element.Id, EmployeeId = kevin.Id, EvaluatorId = patricia.Id, Rating = 4, Comments = "Kevin is professional and knowledgeable. Handles check-ins efficiently. Good with Marriott Bonvoy program.", Period = "Q1 2026", CreatedAt = today.AddDays(-8) },
            new Evaluation { HotelId = element.Id, EmployeeId = guadalupe.Id, EvaluatorId = carmen.Id, Rating = 5, Comments = "Guadalupe is exceptional. Rooms are spotless, guests frequently mention room cleanliness in reviews.", Period = "Q1 2026", CreatedAt = today.AddDays(-7) }
        );

        // ── HK STAR RATINGS ──
        db.HousekeepingRatings.AddRange(
            new HousekeepingRating { HotelId = aloft.Id, HousekeeperId = lupita.Id, RatedById = rosa.Id, Stars = 5, Room = "201", Comments = "Perfect room, everything spotless", Date = today.AddDays(-1) },
            new HousekeepingRating { HotelId = aloft.Id, HousekeeperId = lupita.Id, RatedById = rosa.Id, Stars = 5, Room = "203", Comments = "Excellent as always", Date = today.AddDays(-1) },
            new HousekeepingRating { HotelId = aloft.Id, HousekeeperId = lupita.Id, RatedById = rosa.Id, Stars = 4, Room = "205", Comments = "Good, minor dust on nightstand", Date = today },
            new HousekeepingRating { HotelId = aloft.Id, HousekeeperId = ana.Id, RatedById = rosa.Id, Stars = 3, Room = "301", Comments = "Hair found in shower drain, bathroom mirror had streaks", Date = today.AddDays(-1) },
            new HousekeepingRating { HotelId = aloft.Id, HousekeeperId = ana.Id, RatedById = rosa.Id, Stars = 2, Room = "303", Comments = "Trash under bed not picked up, toilet paper not folded", Date = today },
            new HousekeepingRating { HotelId = aloft.Id, HousekeeperId = ana.Id, RatedById = rosa.Id, Stars = 4, Room = "305", Comments = "Much improved after feedback, good job", Date = today },
            new HousekeepingRating { HotelId = aloft.Id, HousekeeperId = elena.Id, RatedById = rosa.Id, Stars = 4, Room = "401", Comments = "Very thorough, took extra time but room is great", Date = today.AddDays(-1) },
            new HousekeepingRating { HotelId = aloft.Id, HousekeeperId = elena.Id, RatedById = rosa.Id, Stars = 5, Room = "403", Comments = "Perfect!", Date = today },
            new HousekeepingRating { HotelId = element.Id, HousekeeperId = guadalupe.Id, RatedById = carmen.Id, Stars = 5, Room = "101", Comments = "Flawless room", Date = today },
            new HousekeepingRating { HotelId = element.Id, HousekeeperId = guadalupe.Id, RatedById = carmen.Id, Stars = 5, Room = "103", Comments = "Guest complimented room cleanliness at checkout", Date = today.AddDays(-1) },
            new HousekeepingRating { HotelId = element.Id, HousekeeperId = isabela.Id, RatedById = carmen.Id, Stars = 4, Room = "201", Comments = "Good work, minor issues with bed making technique", Date = today },
            new HousekeepingRating { HotelId = element.Id, HousekeeperId = isabela.Id, RatedById = carmen.Id, Stars = 3, Room = "205", Comments = "Needs to pay more attention to bathroom fixtures", Date = today.AddDays(-1) }
        );

        // ── LOST & FOUND ──
        db.LostFoundItems.AddRange(
            new LostFound { HotelId = aloft.Id, ItemDescription = "Black iPhone 15 Pro in leather case", Location = "Room 302", FoundBy = "Lupita Ramirez", StorageLocation = "Front Desk Safe", Status = "found", CreatedAt = today.AddDays(-2) },
            new LostFound { HotelId = aloft.Id, ItemDescription = "Ray-Ban sunglasses, brown aviator", Location = "Pool Area", FoundBy = "David Reyes", StorageLocation = "Lost & Found Cabinet", Status = "found", CreatedAt = today.AddDays(-5) },
            new LostFound { HotelId = aloft.Id, ItemDescription = "Silver wedding ring", Location = "Room 205 bathroom", FoundBy = "Ana Torres", StorageLocation = "Front Desk Safe", Status = "claimed", ClaimedBy = "Carlos Hernandez", GuestName = "Jennifer Park", ClaimedAt = yesterday, CreatedAt = today.AddDays(-3) },
            new LostFound { HotelId = aloft.Id, ItemDescription = "Blue North Face backpack with laptop inside", Location = "Lobby couch", FoundBy = "Carlos Hernandez", StorageLocation = "Back Office", Status = "found", CreatedAt = today.AddDays(-1) },
            new LostFound { HotelId = aloft.Id, ItemDescription = "Children's stuffed bear, brown", Location = "Room 108", FoundBy = "Elena Vasquez", StorageLocation = "Lost & Found Cabinet", Status = "found", CreatedAt = today.AddDays(-45) },
            new LostFound { HotelId = element.Id, ItemDescription = "Apple AirPods Pro in white case", Location = "Room 201", FoundBy = "Guadalupe Mendoza", StorageLocation = "Front Desk", Status = "found", CreatedAt = today.AddDays(-1) },
            new LostFound { HotelId = element.Id, ItemDescription = "Prescription glasses in red case", Location = "Gym", FoundBy = "Alex Thompson", StorageLocation = "Front Desk", Status = "found", CreatedAt = today.AddDays(-3) }
        );

        // ── PREVENTIVE MAINTENANCE ──
        db.PreventiveMaintenances.AddRange(
            new PreventiveMaintenance { HotelId = aloft.Id, Title = "HVAC Filter Replacement - All Floors", Description = "Replace HVAC filters in all rooms and common areas", Frequency = "quarterly", Status = "scheduled", AssignedTo = "Roberto Morales", NextDue = today.AddDays(15), LastCompleted = today.AddDays(-75) },
            new PreventiveMaintenance { HotelId = aloft.Id, Title = "Elevator Annual Inspection", Description = "Annual state-required elevator inspection and certification", Frequency = "yearly", Status = "scheduled", AssignedTo = "Roberto Morales", NextDue = today.AddDays(45) },
            new PreventiveMaintenance { HotelId = aloft.Id, Title = "Fire Extinguisher Inspection", Description = "Monthly visual inspection of all fire extinguishers", Frequency = "monthly", Status = "completed", AssignedTo = "Miguel Castillo", NextDue = today.AddDays(25), LastCompleted = today.AddDays(-5) },
            new PreventiveMaintenance { HotelId = aloft.Id, Title = "Pool Chemical Balance", Description = "Weekly pool chemistry check and adjustment", Frequency = "weekly", Status = "scheduled", AssignedTo = "Roberto Morales", NextDue = today.AddDays(2), LastCompleted = today.AddDays(-5) },
            new PreventiveMaintenance { HotelId = aloft.Id, Title = "Generator Load Test", Description = "Monthly generator test under load for 30 minutes", Frequency = "monthly", Status = "scheduled", AssignedTo = "Roberto Morales", NextDue = today.AddDays(-3) },
            new PreventiveMaintenance { HotelId = aloft.Id, Title = "Pest Control Treatment", Description = "Quarterly pest control service - all floors and exterior", Frequency = "quarterly", Status = "scheduled", AssignedTo = "Miguel Castillo", NextDue = today.AddDays(10) },
            new PreventiveMaintenance { HotelId = element.Id, Title = "HVAC Filter Replacement", Description = "Replace all HVAC filters", Frequency = "quarterly", Status = "scheduled", AssignedTo = "Fernando Ortiz", NextDue = today.AddDays(20) },
            new PreventiveMaintenance { HotelId = element.Id, Title = "Fire Alarm System Test", Description = "Test all fire alarm pull stations and smoke detectors", Frequency = "monthly", Status = "scheduled", AssignedTo = "Fernando Ortiz", NextDue = today.AddDays(-1) },
            new PreventiveMaintenance { HotelId = element.Id, Title = "Water Heater Flush", Description = "Flush and inspect commercial water heaters", Frequency = "quarterly", Status = "completed", AssignedTo = "Jorge Sandoval", NextDue = today.AddDays(80), LastCompleted = today.AddDays(-10) }
        );

        // ── READINGS ──
        db.Readings.AddRange(
            new Reading { HotelId = aloft.Id, Type = "boiler", Value = 182, Unit = "F", Location = "Boiler Room", RecordedBy = "Roberto Morales", Notes = "Normal operating temp", CreatedAt = today.AddHours(-6) },
            new Reading { HotelId = aloft.Id, Type = "boiler", Value = 180, Unit = "F", Location = "Boiler Room", RecordedBy = "Miguel Castillo", CreatedAt = yesterday.AddHours(-6) },
            new Reading { HotelId = aloft.Id, Type = "pool-chlorine", Value = 2.5m, Unit = "ppm", Location = "Pool", RecordedBy = "David Reyes", Notes = "Within range (1-3 ppm)", CreatedAt = today.AddHours(-5) },
            new Reading { HotelId = aloft.Id, Type = "pool-ph", Value = 7.4m, Unit = "pH", Location = "Pool", RecordedBy = "David Reyes", Notes = "Ideal range", CreatedAt = today.AddHours(-5) },
            new Reading { HotelId = aloft.Id, Type = "water", Value = 12450, Unit = "gallons", Location = "Main Meter", RecordedBy = "Roberto Morales", CreatedAt = today.AddHours(-7) },
            new Reading { HotelId = aloft.Id, Type = "electric", Value = 890, Unit = "kWh", Location = "Main Panel", RecordedBy = "Roberto Morales", CreatedAt = today.AddHours(-7) },
            new Reading { HotelId = aloft.Id, Type = "gas", Value = 45, Unit = "therms", Location = "Gas Meter", RecordedBy = "Roberto Morales", CreatedAt = today.AddHours(-7) },
            new Reading { HotelId = element.Id, Type = "boiler", Value = 178, Unit = "F", Location = "Mechanical Room", RecordedBy = "Fernando Ortiz", CreatedAt = today.AddHours(-5) },
            new Reading { HotelId = element.Id, Type = "pool-chlorine", Value = 2.8m, Unit = "ppm", Location = "Pool", RecordedBy = "Alex Thompson", Notes = "Slightly high, adjusting", CreatedAt = today.AddHours(-4) },
            new Reading { HotelId = element.Id, Type = "pool-ph", Value = 7.6m, Unit = "pH", Location = "Pool", RecordedBy = "Alex Thompson", CreatedAt = today.AddHours(-4) }
        );

        // ── ASSETS ──
        db.Assets.AddRange(
            new Asset { HotelId = aloft.Id, Name = "Carrier HVAC Unit - Lobby", Category = "HVAC", SerialNumber = "CAR-2022-0451", Location = "Roof", Condition = "good", WarrantyExpiry = today.AddMonths(6), PurchaseDate = today.AddYears(-3), PurchaseCost = 12500 },
            new Asset { HotelId = aloft.Id, Name = "Otis Elevator #1", Category = "Elevator", SerialNumber = "OTIS-2019-7823", Location = "Main Lobby", Condition = "good", PurchaseDate = today.AddYears(-6) },
            new Asset { HotelId = aloft.Id, Name = "Otis Elevator #2", Category = "Elevator", SerialNumber = "OTIS-2019-7824", Location = "East Wing", Condition = "good", PurchaseDate = today.AddYears(-6) },
            new Asset { HotelId = aloft.Id, Name = "Samsung 55\" TV (x40)", Category = "IT", SerialNumber = "BATCH-SAM-2023", Location = "All Rooms", Condition = "good", WarrantyExpiry = today.AddMonths(8), PurchaseDate = today.AddYears(-2), PurchaseCost = 16000 },
            new Asset { HotelId = aloft.Id, Name = "Generac Generator 150kW", Category = "Electrical", SerialNumber = "GEN-2021-5567", Location = "Generator Room", Condition = "excellent", PurchaseDate = today.AddYears(-4), PurchaseCost = 35000 },
            new Asset { HotelId = aloft.Id, Name = "Commercial Washer - Speed Queen", Category = "Laundry", SerialNumber = "SQ-2022-1198", Location = "Laundry Room", Condition = "good", WarrantyExpiry = today.AddDays(15), PurchaseDate = today.AddYears(-3), PurchaseCost = 4500 },
            new Asset { HotelId = aloft.Id, Name = "Commercial Dryer - Speed Queen", Category = "Laundry", SerialNumber = "SQ-2022-1199", Location = "Laundry Room", Condition = "fair", WarrantyExpiry = today.AddDays(-30), PurchaseDate = today.AddYears(-3), PurchaseCost = 3800 },
            new Asset { HotelId = aloft.Id, Name = "Pool Heater - Pentair", Category = "Plumbing", SerialNumber = "PEN-2023-3341", Location = "Pool Equipment Room", Condition = "fair", PurchaseDate = today.AddYears(-2), PurchaseCost = 5200, },
            new Asset { HotelId = aloft.Id, Name = "Ice Machine Floor 1 - Scotsman", Category = "Kitchen", SerialNumber = "SCO-2024-0087", Location = "Floor 1 Vending", Condition = "excellent", WarrantyExpiry = today.AddYears(1), PurchaseDate = today.AddYears(-1), PurchaseCost = 2800 },
            new Asset { HotelId = element.Id, Name = "Carrier HVAC Unit - Main", Category = "HVAC", SerialNumber = "CAR-2023-0892", Location = "Roof", Condition = "excellent", WarrantyExpiry = today.AddYears(1), PurchaseDate = today.AddYears(-2), PurchaseCost = 15000 },
            new Asset { HotelId = element.Id, Name = "Otis Elevator", Category = "Elevator", SerialNumber = "OTIS-2020-9012", Location = "Main Lobby", Condition = "good", PurchaseDate = today.AddYears(-5) },
            new Asset { HotelId = element.Id, Name = "LG 50\" TV (x24)", Category = "IT", SerialNumber = "BATCH-LG-2024", Location = "All Rooms", Condition = "excellent", WarrantyExpiry = today.AddYears(2), PurchaseDate = today.AddYears(-1), PurchaseCost = 8400 },
            new Asset { HotelId = element.Id, Name = "Treadmill - Life Fitness", Category = "Furniture", SerialNumber = "LF-2023-4456", Location = "Gym", Condition = "poor", PurchaseDate = today.AddYears(-2), PurchaseCost = 3200 }
        );

        // ── VENDORS ──
        db.Vendors.AddRange(
            new Vendor { HotelId = aloft.Id, Name = "Austin Linen Supply", Service = "Linen & Towels", Phone = "(512) 555-3001", Email = "orders@austinlinen.com", Notes = "Weekly delivery Tuesdays. Min order $500." },
            new Vendor { HotelId = aloft.Id, Name = "Lone Star Pest Control", Service = "Pest Control", Phone = "(512) 555-3002", Email = "service@lonestarpest.com", Notes = "Quarterly service. Contact: Mike Johnson" },
            new Vendor { HotelId = aloft.Id, Name = "TX Elevator Services", Service = "Elevator Maintenance", Phone = "(512) 555-3003", Email = "dispatch@txelevator.com", Notes = "24/7 emergency: (512) 555-3099" },
            new Vendor { HotelId = aloft.Id, Name = "Hill Country HVAC", Service = "HVAC Service", Phone = "(512) 555-3004", Email = "service@hcountyhvac.com" },
            new Vendor { HotelId = aloft.Id, Name = "Sysco Central Texas", Service = "Food & Beverage Supply", Phone = "(512) 555-3005", Email = "orders@syscoctx.com", Notes = "Delivery Mon/Wed/Fri. Account #AT-49281" },
            new Vendor { HotelId = aloft.Id, Name = "Sparkling Clean Supply", Service = "Cleaning Supplies", Phone = "(512) 555-3006", Email = "orders@sparklingclean.com" },
            new Vendor { HotelId = element.Id, Name = "Austin Linen Supply", Service = "Linen & Towels", Phone = "(512) 555-3001", Email = "orders@austinlinen.com", Notes = "Weekly delivery Thursdays" },
            new Vendor { HotelId = element.Id, Name = "TX Elevator Services", Service = "Elevator Maintenance", Phone = "(512) 555-3003", Email = "dispatch@txelevator.com" },
            new Vendor { HotelId = element.Id, Name = "ProFit Gym Equipment", Service = "Gym Equipment Repair", Phone = "(512) 555-3010", Email = "service@profitgym.com", Notes = "Treadmill belt on order" }
        );

        // ── EXPENSES ──
        db.Expenses.AddRange(
            new Expense { HotelId = aloft.Id, Description = "Weekly linen service", Amount = 850, Category = "Housekeeping", Date = today.AddDays(-7), CreatedBy = "Rosa Garcia" },
            new Expense { HotelId = aloft.Id, Description = "HVAC filter order (48 units)", Amount = 576, Category = "Maintenance", Date = today.AddDays(-10), CreatedBy = "Roberto Morales" },
            new Expense { HotelId = aloft.Id, Description = "Pool chemicals - chlorine & pH balancer", Amount = 245, Category = "Maintenance", Date = today.AddDays(-5), CreatedBy = "Roberto Morales" },
            new Expense { HotelId = aloft.Id, Description = "Front desk market restock", Amount = 320, Category = "Front Desk", Date = today.AddDays(-3), CreatedBy = "Maria Lopez" },
            new Expense { HotelId = aloft.Id, Description = "Pest control quarterly service", Amount = 450, Category = "Maintenance", Date = today.AddDays(-12), CreatedBy = "Roberto Morales" },
            new Expense { HotelId = aloft.Id, Description = "Cleaning supplies monthly order", Amount = 680, Category = "Housekeeping", Date = today.AddDays(-8), CreatedBy = "Rosa Garcia" },
            new Expense { HotelId = aloft.Id, Description = "Light bulbs and electrical supplies", Amount = 185, Category = "Maintenance", Date = today.AddDays(-4), CreatedBy = "Miguel Castillo" },
            new Expense { HotelId = aloft.Id, Description = "Staff uniforms (3 new hires)", Amount = 450, Category = "Payroll", Date = today.AddDays(-15), CreatedBy = "Sofia Martinez" },
            new Expense { HotelId = aloft.Id, Description = "Monthly water bill", Amount = 1200, Category = "Utilities", Date = today.AddDays(-2), CreatedBy = "Jaime Guerrero" },
            new Expense { HotelId = aloft.Id, Description = "Monthly electric bill", Amount = 3450, Category = "Utilities", Date = today.AddDays(-2), CreatedBy = "Jaime Guerrero" },
            new Expense { HotelId = aloft.Id, Description = "Monthly gas bill", Amount = 680, Category = "Utilities", Date = today.AddDays(-2), CreatedBy = "Jaime Guerrero" },
            new Expense { HotelId = element.Id, Description = "Weekly linen service", Amount = 620, Category = "Housekeeping", Date = today.AddDays(-7), CreatedBy = "Carmen Delgado" },
            new Expense { HotelId = element.Id, Description = "Treadmill repair parts", Amount = 350, Category = "Maintenance", Date = today.AddDays(-3), CreatedBy = "Fernando Ortiz" },
            new Expense { HotelId = element.Id, Description = "Cleaning supplies", Amount = 420, Category = "Housekeeping", Date = today.AddDays(-6), CreatedBy = "Carmen Delgado" },
            new Expense { HotelId = element.Id, Description = "Monthly electric bill", Amount = 2100, Category = "Utilities", Date = today.AddDays(-2), CreatedBy = "Daniel Rivera" }
        );

        // ── BUDGET ITEMS ──
        var currentMonth = today.ToString("yyyy-MM");
        db.BudgetItems.AddRange(
            new BudgetItem { HotelId = aloft.Id, Category = "Maintenance", PlannedAmount = 3000, Period = currentMonth },
            new BudgetItem { HotelId = aloft.Id, Category = "Housekeeping", PlannedAmount = 2500, Period = currentMonth },
            new BudgetItem { HotelId = aloft.Id, Category = "Front Desk", PlannedAmount = 500, Period = currentMonth },
            new BudgetItem { HotelId = aloft.Id, Category = "Utilities", PlannedAmount = 5500, Period = currentMonth },
            new BudgetItem { HotelId = aloft.Id, Category = "Payroll", PlannedAmount = 45000, Period = currentMonth },
            new BudgetItem { HotelId = aloft.Id, Category = "Food & Beverage", PlannedAmount = 2000, Period = currentMonth },
            new BudgetItem { HotelId = aloft.Id, Category = "Marketing", PlannedAmount = 1500, Period = currentMonth },
            new BudgetItem { HotelId = element.Id, Category = "Maintenance", PlannedAmount = 2000, Period = currentMonth },
            new BudgetItem { HotelId = element.Id, Category = "Housekeeping", PlannedAmount = 1800, Period = currentMonth },
            new BudgetItem { HotelId = element.Id, Category = "Utilities", PlannedAmount = 3500, Period = currentMonth }
        );

        // ── MESSAGES ──
        db.Messages.AddRange(
            new Message { HotelId = aloft.Id, Subject = "Brand Inspection Next Tuesday", Body = "Team, Marriott brand inspection is scheduled for next Tuesday at 10 AM. Please review all brand standards checklists for your department. Everything needs to be perfect. Let me know if you need anything.", FromUserId = jaime.Id, IsAnnouncement = true, CreatedAt = today.AddDays(-1) },
            new Message { HotelId = aloft.Id, Subject = "Floor 3 Deep Clean Complete", Body = "All rooms on floor 3 have been deep cleaned and inspected. One mattress in 308 needs replacement - please order. Floor 4 scheduled for Thursday.", FromUserId = rosa.Id, ToUserId = sofia.Id, CreatedAt = today.AddHours(-5) },
            new Message { HotelId = aloft.Id, Subject = "AC Issue Room 302 - Urgent", Body = "Guest is very unhappy about the AC noise. I've logged a work order but he might ask for compensation. Heads up.", FromUserId = carlos.Id, ToUserId = maria.Id, CreatedAt = today.AddHours(-2) },
            new Message { HotelId = aloft.Id, Subject = "Pool Heater Status", Body = "The pool heater thermostat is reading low. I've adjusted it but if the temp doesn't come up by tomorrow we may need to call Hill Country HVAC.", FromUserId = roberto.Id, ToUserId = sofia.Id, CreatedAt = today.AddHours(-4) },
            new Message { HotelId = aloft.Id, Subject = "Schedule Change Request", Body = "Hi Rosa, I need to swap my Saturday shift with Elena if possible. I have a family event. Thanks!", FromUserId = ana.Id, ToUserId = rosa.Id, CreatedAt = today.AddHours(-6) },
            new Message { HotelId = element.Id, Subject = "Safety Meeting Next Week", Body = "Monthly safety meeting scheduled for next Monday at 10 AM. Mandatory for all department managers. We'll review fire drill results and update emergency procedures.", FromUserId = daniel.Id, IsAnnouncement = true, CreatedAt = today.AddDays(-2) },
            new Message { HotelId = element.Id, Subject = "Gym Equipment Update", Body = "Treadmill parts have been ordered from ProFit. ETA is 2 business days. I've placed an out-of-order sign. The other equipment is all functioning.", FromUserId = fernando.Id, ToUserId = daniel.Id, CreatedAt = today.AddHours(-3) },
            new Message { HotelId = element.Id, Subject = "Low Sheet Inventory", Body = "We're down to 8 king-size sheet sets. I've placed an order with Austin Linen but the delivery isn't until Thursday. We should be fine if we manage laundry carefully.", FromUserId = carmen.Id, ToUserId = daniel.Id, CreatedAt = today.AddHours(-5) }
        );

        await db.SaveChangesAsync();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapGet("/health", async (ApplicationDbContext db) =>
{
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", error = ex.Message }, statusCode: 503);
    }
});

app.Run();
