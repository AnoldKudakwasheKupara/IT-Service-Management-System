using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Enums;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Efm;
using IT_Service_Management_System.Models.Itsm;
using IT_Service_Management_System.Services.Efm;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// Defensive, idempotent demo-data "top-up" seeder. On every startup it ensures roughly
    /// <c>Target</c> rows exist for each major entity so all list/report pages show realistic
    /// data. Each entity is seeded in its own try/catch (a single bad block can never stop the
    /// app booting) and SaveChanges runs after every block. Values are deterministic
    /// (fixed-seed Random) for reproducibility.
    /// </summary>
    public class DemoDataSeeder
    {
        private const int Target = 13;

        private readonly ApplicationDbContext _db;
        private readonly IDocumentStorage _storage;
        private readonly ILogger<DemoDataSeeder> _logger;

        // Fixed seed → reproducible data across restarts.
        private readonly Random _rng = new(12345);

        public DemoDataSeeder(ApplicationDbContext db, IDocumentStorage storage, ILogger<DemoDataSeeder> logger)
        {
            _db = db;
            _storage = storage;
            _logger = logger;
        }

        /// <summary>How many rows to add to reach <paramref name="target"/> given the current count.</summary>
        private int Need<T>(int have, int target = Target) => Math.Max(0, target - have);

        private static readonly string[] FirstNames =
        {
            "Jane", "John", "Tinashe", "Rutendo", "Michael", "Sarah", "David", "Chipo",
            "Emily", "Farai", "Grace", "Peter", "Nomsa", "Brian", "Linda", "Tafadzwa",
            "Kelvin", "Ruvarashe", "Simba", "Anita", "Prince", "Blessing", "Kudzai", "Ngoni"
        };

        private static readonly string[] LastNames =
        {
            "Doe", "Moyo", "Ncube", "Smith", "Chboth", "Marufu", "Sibanda", "Dube",
            "Johnson", "Mutasa", "Banda", "Nyathi", "Williams", "Chirwa", "Gumbo", "Mpofu",
            "Zulu", "Katsande", "Mahaso", "Chikwava", "Nkomo", "Phiri", "Madziva", "Tapfuma"
        };

        public async Task SeedAsync()
        {
            _logger.LogInformation("DemoDataSeeder starting.");

            var departmentIds = await SeedDepartmentsAsync();
            var userIds = await SeedUsersAsync(departmentIds);
            await SeedActivitiesAsync(userIds);
            var assetIds = await SeedAssetsAsync(userIds);
            await SeedPaymentsAsync();
            await SeedSslCertificatesAsync();
            await SeedMaintenanceRecordsAsync(userIds);
            var ciIds = await SeedConfigurationItemsAsync(userIds, assetIds);
            var problemIds = await SeedProblemsAsync(userIds, ciIds);
            await SeedChangeRequestsAsync(userIds, ciIds, problemIds);
            await SeedTicketsAsync(userIds);
            await SeedEmployeeDocumentsAsync(userIds);

            _logger.LogInformation("DemoDataSeeder finished.");
        }

        // ── 1. Departments ─────────────────────────────────────────────────────────
        private async Task<List<int>> SeedDepartmentsAsync()
        {
            try
            {
                var wanted = new (string Name, string Desc)[]
                {
                    ("IT", "Information Technology infrastructure and support"),
                    ("HR", "Human Resources and people operations"),
                    ("Finance", "Finance, accounting and payroll"),
                    ("Operations", "Business operations and logistics"),
                    ("Development", "Software engineering and development"),
                    ("Support", "Customer and internal service desk"),
                    ("Security", "Information and physical security"),
                    ("Facilities", "Facilities and building management"),
                    ("Procurement", "Purchasing and vendor management"),
                    ("Marketing", "Marketing and communications"),
                };

                var existing = await _db.Departments.Select(d => d.Name).ToListAsync();
                foreach (var (name, desc) in wanted)
                {
                    if (!existing.Contains(name))
                    {
                        _db.Departments.Add(new Department
                        {
                            Name = name,
                            Description = desc,
                            CreatedAt = DateTime.Now.AddDays(-_rng.Next(200, 400))
                        });
                    }
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding Departments failed.");
            }

            return await _db.Departments.Select(d => d.Id).ToListAsync();
        }

        // ── 2. Users ───────────────────────────────────────────────────────────────
        private async Task<List<int>> SeedUsersAsync(List<int> departmentIds)
        {
            try
            {
                var have = await _db.Users.CountAsync();
                var need = Need<User>(have);
                if (need > 0)
                {
                    var passwordHash = PasswordHasher.HashPassword("Demo@123");
                    var roles = new[]
                    {
                        UserRole.Admin, UserRole.SystemsAdmin, UserRole.HR, UserRole.Finance,
                        UserRole.Development, UserRole.Employee, UserRole.Employee, UserRole.Employee
                    };

                    for (int i = 0; i < need; i++)
                    {
                        var first = FirstNames[i % FirstNames.Length];
                        var last = LastNames[(i * 3 + 1) % LastNames.Length];
                        var email = $"demo.{first.ToLowerInvariant()}.{last.ToLowerInvariant()}{i}@axis.local";

                        if (await _db.Users.AnyAsync(u => u.Email == email))
                            continue;

                        _db.Users.Add(new User
                        {
                            FirstName = first,
                            LastName = last,
                            Email = email,
                            PasswordHash = passwordHash,
                            IsActive = true,
                            PasswordChangedAt = DateTime.Now,
                            Role = roles[i % roles.Length],
                            DepartmentId = departmentIds.Count > 0 ? departmentIds[i % departmentIds.Count] : (int?)null,
                            CreatedAt = DateTime.Now.AddDays(-_rng.Next(1, 365))
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding Users failed.");
            }

            return await _db.Users.Select(u => u.Id).ToListAsync();
        }

        // ── 3. Activity categories + activities ──────────────────────────────────────
        private async Task SeedActivitiesAsync(List<int> userIds)
        {
            var categoryIds = new List<int>();
            try
            {
                var wantedCats = new[] { "Development", "Support", "Meeting", "Maintenance", "Research", "Administration" };
                var existingCats = await _db.ActivityCategories.Select(c => c.Name).ToListAsync();
                foreach (var name in wantedCats)
                {
                    if (!existingCats.Contains(name))
                        _db.ActivityCategories.Add(new ActivityCategory { Name = name });
                }
                await _db.SaveChangesAsync();
                categoryIds = await _db.ActivityCategories.Select(c => c.Id).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding ActivityCategories failed.");
            }

            try
            {
                var have = await _db.Activities.CountAsync();
                var need = Need<Activity>(have);
                if (need > 0 && categoryIds.Count > 0)
                {
                    var titles = new[]
                    {
                        "Deployed release build", "Resolved user ticket", "Sprint planning meeting",
                        "Server patching", "Database backup verification", "Reviewed pull request",
                        "Investigated network latency", "Onboarded new hire", "Updated documentation",
                        "Ran security scan", "Configured firewall rules", "Assisted with laptop setup",
                        "Monthly report preparation"
                    };

                    for (int i = 0; i < need; i++)
                    {
                        var start = DateTime.Now.AddDays(-_rng.Next(1, 60)).AddHours(-_rng.Next(0, 6));
                        var end = start.AddMinutes(_rng.Next(30, 240));
                        _db.Activities.Add(new Activity
                        {
                            UserId = userIds.Count > 0 ? userIds[i % userIds.Count].ToString() : "0",
                            Title = titles[i % titles.Length],
                            Description = "Demo activity record for reporting.",
                            StartTime = start,
                            EndTime = end,
                            CategoryId = categoryIds[i % categoryIds.Count],
                            CreatedAt = start
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding Activities failed.");
            }
        }

        // ── 4. Assets ────────────────────────────────────────────────────────────────
        private async Task<List<int>> SeedAssetsAsync(List<int> userIds)
        {
            try
            {
                var have = await _db.Assets.CountAsync();
                var need = Need<Asset>(have);
                if (need > 0)
                {
                    var items = new[]
                    {
                        "Dell Latitude 5540 Laptop", "HP EliteDesk 800 Desktop", "Lenovo ThinkPad X1",
                        "Cisco Catalyst 2960 Switch", "APC Smart-UPS 1500", "Samsung 27\" Monitor",
                        "iPhone 14", "Logitech MX Keyboard", "Canon imageRUNNER Printer",
                        "Ubiquiti UniFi AP", "Dell PowerEdge R750 Server", "HP ProDesk Mini",
                        "MacBook Pro 14\""
                    };
                    var statuses = new[] { "Issued", "Available", "In Repair" };
                    var conditions = new[] { "New", "Good", "Fair", "Needs Repair" };
                    var actions = new[] { "Issued", "Returned", "Received", "Repaired" };

                    for (int i = 0; i < need; i++)
                    {
                        var status = statuses[i % statuses.Length];
                        int? assignedTo = status == "Issued" && userIds.Count > 0
                            ? userIds[i % userIds.Count]
                            : (int?)null;

                        _db.Assets.Add(new Asset
                        {
                            Date = DateTime.Now.AddDays(-_rng.Next(1, 300)),
                            UserId = assignedTo,
                            AssetTag = $"AXIS-{1000 + i:D4}",
                            ItemName = items[i % items.Length],
                            SerialNumber = $"SN{_rng.Next(100000, 999999)}-{i}",
                            ActionType = actions[i % actions.Length],
                            Condition = conditions[i % conditions.Length],
                            IssuedBy = "IT Store Room",
                            Remarks = "Demo asset record.",
                            PurchaseDate = DateTime.Now.AddDays(-_rng.Next(100, 900)),
                            PurchaseCost = 250m + _rng.Next(0, 3000),
                            Status = status,
                            EventType = actions[i % actions.Length]
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding Assets failed.");
            }

            return await _db.Assets.Select(a => a.Id).ToListAsync();
        }

        // ── 5. Payments ──────────────────────────────────────────────────────────────
        private async Task SeedPaymentsAsync()
        {
            try
            {
                // Payment requires a PaymentSchedule FK — ensure one exists.
                var scheduleId = await _db.PaymentSchedules.Select(p => p.Id).FirstOrDefaultAsync();
                if (scheduleId == 0)
                {
                    var schedule = new PaymentSchedule
                    {
                        ServiceName = "General Services",
                        Amount = 1200m,
                        PaymentDate = DateTime.Now.AddDays(-30),
                        Frequency = PaymentFrequency.Monthly,
                        Departments = "IT,Finance",
                        NextRunDate = DateTime.Now.AddDays(30),
                        IsActive = true
                    };
                    _db.PaymentSchedules.Add(schedule);
                    await _db.SaveChangesAsync();
                    scheduleId = schedule.Id;
                }

                var have = await _db.Payments.CountAsync();
                var need = Need<Payment>(have);
                if (need > 0)
                {
                    var services = new[]
                    {
                        "Microsoft 365 Licences", "Domain Renewal", "Cloud Hosting (Azure)",
                        "Internet Leased Line", "Antivirus Subscription", "Backup Service",
                        "SSL Certificates", "Firewall Support", "Email Gateway", "CRM Licence",
                        "Payroll Software", "VoIP Service", "Cloud Backup"
                    };

                    for (int i = 0; i < need; i++)
                    {
                        // Mix of statuses: overdue (past due, unpaid), paid, pending (upcoming).
                        string status;
                        DateTime dueDate;
                        DateTime? paidDate;
                        int bucket = i % 3;
                        if (bucket == 0)
                        {
                            status = "Overdue";
                            dueDate = DateTime.Now.AddDays(-_rng.Next(5, 60));
                            paidDate = null;
                        }
                        else if (bucket == 1)
                        {
                            status = "Paid";
                            dueDate = DateTime.Now.AddDays(-_rng.Next(1, 40));
                            paidDate = dueDate.AddDays(-_rng.Next(0, 5));
                        }
                        else
                        {
                            status = "Pending";
                            dueDate = DateTime.Now.AddDays(_rng.Next(3, 45));
                            paidDate = null;
                        }

                        _db.Payments.Add(new Payment
                        {
                            ServiceName = services[i % services.Length],
                            Amount = 100m + _rng.Next(50, 5000),
                            DueDate = dueDate,
                            PaidDate = paidDate,
                            Status = status,
                            PaymentScheduleId = scheduleId
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding Payments failed.");
            }
        }

        // ── 6. SSL certificates ──────────────────────────────────────────────────────
        private async Task SeedSslCertificatesAsync()
        {
            try
            {
                var have = await _db.SSLCertificates.CountAsync();
                var need = Need<SSLCertificate>(have);
                if (need > 0)
                {
                    var systems = new[]
                    {
                        ("Corporate Website", "www.axis.local"),
                        ("Customer Portal", "portal.axis.local"),
                        ("Email Gateway", "mail.axis.local"),
                        ("VPN Gateway", "vpn.axis.local"),
                        ("Intranet", "intranet.axis.local"),
                        ("API Gateway", "api.axis.local"),
                        ("Payroll App", "payroll.axis.local"),
                        ("Support Desk", "support.axis.local"),
                        ("File Share", "files.axis.local"),
                        ("Monitoring", "monitor.axis.local"),
                        ("Wiki", "wiki.axis.local"),
                        ("Dev Portal", "dev.axis.local"),
                        ("Backup Console", "backup.axis.local"),
                    };

                    for (int i = 0; i < need; i++)
                    {
                        // Spread expiry: expired, ≤30d, ≤90d, healthy.
                        int bucket = i % 4;
                        DateTime expiry = bucket switch
                        {
                            0 => DateTime.Now.AddDays(-_rng.Next(1, 40)),   // expired
                            1 => DateTime.Now.AddDays(_rng.Next(1, 30)),    // ≤30 days
                            2 => DateTime.Now.AddDays(_rng.Next(31, 90)),   // ≤90 days
                            _ => DateTime.Now.AddDays(_rng.Next(120, 730)), // healthy
                        };

                        var (name, url) = systems[i % systems.Length];
                        _db.SSLCertificates.Add(new SSLCertificate
                        {
                            SystemName = name,
                            URL = $"https://{url}",
                            ExpiryDate = expiry,
                            IsRenewed = bucket == 3,
                            LastRenewedDate = bucket == 3 ? DateTime.Now.AddDays(-_rng.Next(30, 300)) : (DateTime?)null
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding SSLCertificates failed.");
            }
        }

        // ── 7. Maintenance records ───────────────────────────────────────────────────
        private async Task SeedMaintenanceRecordsAsync(List<int> userIds)
        {
            try
            {
                var have = await _db.MaintenanceRecords.CountAsync();
                var need = Need<MaintenanceRecord>(have);
                if (need > 0)
                {
                    var assetNames = await _db.Assets.Select(a => a.ItemName).Take(20).ToListAsync();
                    if (assetNames.Count == 0)
                        assetNames = new List<string> { "Dell Latitude Laptop", "HP Desktop", "Cisco Switch" };

                    var employeeNames = await _db.Users
                        .Select(u => u.FirstName + " " + u.LastName).Take(20).ToListAsync();
                    if (employeeNames.Count == 0)
                        employeeNames = new List<string> { "IT Technician" };

                    var types = Enum.GetValues<MaintenanceType>();
                    var problems = new[]
                    {
                        "Slow performance", "Overheating", "Broken screen", "Software update required",
                        "Failed hard drive", "Network connectivity issue", "Battery replacement",
                        "Routine inspection", "OS reinstall", "Fan replacement", "RAM upgrade",
                        "Keyboard fault", "Preventive service"
                    };

                    for (int i = 0; i < need; i++)
                    {
                        var maintDate = DateTime.Now.AddDays(-_rng.Next(1, 180));
                        _db.MaintenanceRecords.Add(new MaintenanceRecord
                        {
                            AssetName = assetNames[i % assetNames.Count],
                            EmployeeName = employeeNames[i % employeeNames.Count],
                            MaintenanceDate = maintDate,
                            MaintenanceType = types[i % types.Length],
                            ProblemDescription = problems[i % problems.Length],
                            WorkDone = "Diagnosed and resolved the reported issue.",
                            PartsReplaced = i % 3 == 0 ? "Hard drive" : null,
                            SoftwareInstalled = i % 4 == 0 ? "Windows updates" : null,
                            Comments = "Demo maintenance record.",
                            NextMaintenanceDate = i % 2 == 0 ? maintDate.AddDays(_rng.Next(30, 180)) : (DateTime?)null,
                            CreatedAt = maintDate
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding MaintenanceRecords failed.");
            }
        }

        // ── 8. Configuration items (CMDB) ────────────────────────────────────────────
        private async Task<List<int>> SeedConfigurationItemsAsync(List<int> userIds, List<int> assetIds)
        {
            try
            {
                var have = await _db.ConfigurationItems.CountAsync();
                var need = Need<ConfigurationItem>(have);
                if (need > 0)
                {
                    var cis = new (string Name, CiType Type, CiCriticality Crit, CiEnvironment Env)[]
                    {
                        ("PROD-DB-01", CiType.Database, CiCriticality.Critical, CiEnvironment.Production),
                        ("Payroll App", CiType.Application, CiCriticality.High, CiEnvironment.Production),
                        ("Core Switch", CiType.NetworkDevice, CiCriticality.Critical, CiEnvironment.Production),
                        ("Web Server 01", CiType.Server, CiCriticality.High, CiEnvironment.Production),
                        ("Email Service", CiType.Service, CiCriticality.High, CiEnvironment.Production),
                        ("CRM Application", CiType.Application, CiCriticality.Medium, CiEnvironment.Production),
                        ("Backup Server", CiType.Server, CiCriticality.Medium, CiEnvironment.Production),
                        ("Staging DB", CiType.Database, CiCriticality.Low, CiEnvironment.Staging),
                        ("VPN Gateway", CiType.NetworkDevice, CiCriticality.High, CiEnvironment.Production),
                        ("Dev Workstation", CiType.Workstation, CiCriticality.Low, CiEnvironment.Development),
                        ("Cloud Storage", CiType.CloudResource, CiCriticality.Medium, CiEnvironment.Production),
                        ("File Server", CiType.Server, CiCriticality.Medium, CiEnvironment.Production),
                        ("Test API", CiType.Application, CiCriticality.Low, CiEnvironment.Test),
                    };
                    var statuses = new[] { CiStatus.Active, CiStatus.Active, CiStatus.UnderMaintenance, CiStatus.Inactive };

                    for (int i = 0; i < need; i++)
                    {
                        var (name, type, crit, env) = cis[i % cis.Length];
                        _db.ConfigurationItems.Add(new ConfigurationItem
                        {
                            Name = i < cis.Length ? name : $"{name} #{i}",
                            Type = type,
                            Status = statuses[i % statuses.Length],
                            Criticality = crit,
                            Environment = env,
                            Description = "Demo configuration item.",
                            Location = "Data Centre A",
                            Vendor = "Various",
                            Version = $"{1 + i % 5}.{i % 10}",
                            IpOrHostname = $"10.0.{i}.{_rng.Next(2, 254)}",
                            OwnerId = userIds.Count > 0 ? userIds[i % userIds.Count] : (int?)null,
                            AssetId = assetIds.Count > 0 && i % 2 == 0 ? assetIds[i % assetIds.Count] : (int?)null,
                            CreatedAt = DateTime.Now.AddDays(-_rng.Next(10, 400))
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding ConfigurationItems failed.");
            }

            return await _db.ConfigurationItems.Select(c => c.Id).ToListAsync();
        }

        // ── 9. Problems ──────────────────────────────────────────────────────────────
        private async Task<List<int>> SeedProblemsAsync(List<int> userIds, List<int> ciIds)
        {
            try
            {
                var have = await _db.Problems.CountAsync();
                var need = Need<Problem>(have);
                if (need > 0 && userIds.Count > 0)
                {
                    var titles = new[]
                    {
                        "Recurring email delivery failures", "Intermittent VPN drops",
                        "Payroll app slow at month-end", "Database deadlocks under load",
                        "Printer offline in Finance", "Wi-Fi coverage gaps on 3rd floor",
                        "Backup jobs failing overnight", "High CPU on web server",
                        "Login timeouts during peak", "Disk space exhaustion on file server",
                        "SSL handshake errors", "DNS resolution delays", "Frequent password lockouts"
                    };
                    var statuses = Enum.GetValues<ProblemStatus>();
                    var priorities = Enum.GetValues<TicketPriority>();

                    for (int i = 0; i < need; i++)
                    {
                        var status = statuses[i % statuses.Length];
                        var created = DateTime.Now.AddDays(-_rng.Next(5, 200));
                        bool resolved = status == ProblemStatus.Resolved || status == ProblemStatus.Closed;

                        _db.Problems.Add(new Problem
                        {
                            Title = titles[i % titles.Length],
                            Description = "Demo problem record capturing the underlying cause of related incidents.",
                            Status = status,
                            Priority = priorities[i % priorities.Length],
                            RootCause = i % 2 == 0 ? "Misconfigured connection pool exhausting resources." : null,
                            Workaround = i % 3 == 0 ? "Restart the affected service to restore capacity." : null,
                            ConfigurationItemId = ciIds.Count > 0 ? ciIds[i % ciIds.Count] : (int?)null,
                            AssignedToId = userIds[(i + 1) % userIds.Count],
                            CreatedById = userIds[i % userIds.Count],
                            CreatedAt = created,
                            ResolvedAt = resolved ? created.AddDays(_rng.Next(1, 20)) : (DateTime?)null,
                            ClosedAt = status == ProblemStatus.Closed ? created.AddDays(_rng.Next(20, 40)) : (DateTime?)null
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding Problems failed.");
            }

            return await _db.Problems.Select(p => p.Id).ToListAsync();
        }

        // ── 10. Change requests ──────────────────────────────────────────────────────
        private async Task SeedChangeRequestsAsync(List<int> userIds, List<int> ciIds, List<int> problemIds)
        {
            try
            {
                var have = await _db.ChangeRequests.CountAsync();
                var need = Need<ChangeRequest>(have);
                if (need > 0 && userIds.Count > 0)
                {
                    var titles = new[]
                    {
                        "Upgrade database server to latest patch", "Migrate email to new gateway",
                        "Replace core switch firmware", "Deploy new CRM release",
                        "Rotate SSL certificates", "Increase file server storage",
                        "Enable MFA for all staff", "Network segmentation rollout",
                        "Backup schedule optimization", "Payroll app version upgrade",
                        "Firewall rule cleanup", "OS patching across servers",
                        "VPN client update"
                    };
                    var types = Enum.GetValues<ChangeType>();
                    var risks = Enum.GetValues<ChangeRisk>();
                    var impacts = Enum.GetValues<ChangeImpact>();
                    // Include closed + implemented states so the change success-rate KPI is non-zero.
                    var statuses = new[]
                    {
                        ChangeStatus.Draft, ChangeStatus.SubmittedForApproval, ChangeStatus.Approved,
                        ChangeStatus.Scheduled, ChangeStatus.InProgress, ChangeStatus.Implemented,
                        ChangeStatus.Closed, ChangeStatus.Failed
                    };

                    for (int i = 0; i < need; i++)
                    {
                        var status = statuses[i % statuses.Length];
                        var created = DateTime.Now.AddDays(-_rng.Next(3, 200));
                        bool approved = status is ChangeStatus.Approved or ChangeStatus.Scheduled
                            or ChangeStatus.InProgress or ChangeStatus.Implemented or ChangeStatus.Closed;
                        bool closed = status is ChangeStatus.Closed or ChangeStatus.Failed;
                        // A few upcoming windows, most in the recent past.
                        var schedStart = i % 3 == 0
                            ? DateTime.Now.AddDays(_rng.Next(1, 20))
                            : created.AddDays(_rng.Next(1, 15));

                        _db.ChangeRequests.Add(new ChangeRequest
                        {
                            Title = titles[i % titles.Length],
                            Description = "Demo change request with implementation and backout plans.",
                            Type = types[i % types.Length],
                            Status = status,
                            Risk = risks[i % risks.Length],
                            Impact = impacts[i % impacts.Length],
                            ImplementationPlan = "Apply the change during the approved maintenance window.",
                            BackoutPlan = "Restore from snapshot if verification fails.",
                            TestPlan = "Smoke-test core functions post-change.",
                            ScheduledStart = schedStart,
                            ScheduledEnd = schedStart.AddHours(_rng.Next(1, 6)),
                            ConfigurationItemId = ciIds.Count > 0 ? ciIds[i % ciIds.Count] : (int?)null,
                            ProblemId = problemIds.Count > 0 && i % 3 == 0 ? problemIds[i % problemIds.Count] : (int?)null,
                            AssignedToId = userIds[(i + 2) % userIds.Count],
                            CreatedById = userIds[i % userIds.Count],
                            ApprovedById = approved ? userIds[(i + 1) % userIds.Count] : (int?)null,
                            ApprovedAt = approved ? created.AddDays(1) : (DateTime?)null,
                            ApprovalNotes = approved ? "Approved by CAB." : null,
                            CreatedAt = created,
                            ClosedAt = closed ? created.AddDays(_rng.Next(2, 30)) : (DateTime?)null,
                            ImplementedSuccessfully = closed
                                ? status == ChangeStatus.Closed  // Closed = success, Failed = false
                                : (bool?)null
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding ChangeRequests failed.");
            }
        }

        // ── 11. Tickets (topped up to ~20 for meaningful SLA/agent/trend reports) ─────
        private async Task SeedTicketsAsync(List<int> userIds)
        {
            const int ticketTarget = 20;
            try
            {
                var have = await _db.Tickets.CountAsync();
                var need = Need<Ticket>(have, ticketTarget);
                if (need > 0 && userIds.Count > 0)
                {
                    var staffIds = await _db.Users
                        .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.SystemsAdmin
                            || u.Role == UserRole.Development)
                        .Select(u => u.Id).ToListAsync();
                    if (staffIds.Count == 0) staffIds = userIds;

                    var categories = new[] { "Network", "Hardware", "Software", "Access", "Email" };
                    var titles = new[]
                    {
                        "Cannot connect to VPN", "Laptop won't power on", "Application crashes on launch",
                        "Password reset request", "Not receiving emails", "Printer not responding",
                        "Slow internet connection", "Need software installed", "Account locked out",
                        "Blue screen error", "Shared drive inaccessible", "Monitor flickering",
                        "Two-factor code not arriving"
                    };

                    // Response/resolution targets in minutes by priority.
                    (int resp, int res) TargetFor(TicketPriority p) => p switch
                    {
                        TicketPriority.Critical => (30, 240),
                        TicketPriority.High => (60, 480),
                        TicketPriority.Medium => (240, 1440),
                        _ => (480, 4320),
                    };
                    var priorities = Enum.GetValues<TicketPriority>();

                    for (int i = 0; i < need; i++)
                    {
                        var priority = priorities[i % priorities.Length];
                        var (respMin, resMin) = TargetFor(priority);

                        // Spread creation across the last 12 months for a trend curve.
                        var created = DateTime.Now.AddDays(-_rng.Next(0, 365)).AddHours(-_rng.Next(0, 24));
                        var responseDue = created.AddMinutes(respMin);
                        var due = created.AddMinutes(resMin);

                        bool assigned = _rng.Next(100) < 70; // ~70% assigned
                        int? assignedTo = assigned ? staffIds[_rng.Next(staffIds.Count)] : (int?)null;

                        bool resolve = assigned && _rng.Next(100) < 80; // most assigned tickets get resolved
                        bool breach = _rng.Next(100) < 25;              // ~25% breach the resolution SLA

                        TicketStatus status;
                        DateTime? firstResponded = null, resolvedAt = null, closedAt = null, updatedAt = null;
                        int? satisfaction = null;
                        string? satisfactionComment = null;

                        if (resolve)
                        {
                            firstResponded = created.AddMinutes(_rng.Next(5, Math.Max(6, respMin - 5)));
                            // Resolution span: within SLA normally, beyond it for breaches.
                            var resolutionSpan = breach
                                ? resMin + _rng.Next(30, 600)
                                : _rng.Next(respMin, Math.Max(respMin + 1, resMin));
                            resolvedAt = created.AddMinutes(resolutionSpan);
                            bool closedToo = _rng.Next(100) < 60;
                            closedAt = closedToo ? resolvedAt.Value.AddHours(_rng.Next(1, 48)) : (DateTime?)null;
                            status = closedToo ? TicketStatus.Closed : TicketStatus.Resolved;
                            updatedAt = closedAt ?? resolvedAt;

                            // ~half of resolved tickets get a CSAT rating.
                            if (_rng.Next(100) < 50)
                            {
                                satisfaction = _rng.Next(3, 6); // 3–5
                                satisfactionComment = satisfaction >= 5
                                    ? "Quick and helpful resolution."
                                    : "Resolved satisfactorily.";
                            }
                        }
                        else
                        {
                            // Open / in-progress. A few open+overdue (breaching) by using an old created date.
                            if (assigned)
                            {
                                status = TicketStatus.InProgress;
                                firstResponded = created.AddMinutes(_rng.Next(5, Math.Max(6, respMin)));
                                updatedAt = firstResponded;
                            }
                            else
                            {
                                status = TicketStatus.Open;
                            }
                        }

                        _db.Tickets.Add(new Ticket
                        {
                            Title = titles[i % titles.Length],
                            Description = "Demo ticket created for reporting and SLA dashboards.",
                            Category = categories[i % categories.Length],
                            Status = status,
                            Priority = priority,
                            CreatedAt = created,
                            UpdatedAt = updatedAt,
                            FirstRespondedAt = firstResponded,
                            ResolvedAt = resolvedAt,
                            ClosedAt = closedAt,
                            DueAt = due,
                            ResponseDueAt = responseDue,
                            CreatedById = userIds[i % userIds.Count],
                            AssignedToId = assignedTo,
                            SatisfactionRating = satisfaction,
                            SatisfactionComment = satisfactionComment
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding Tickets failed.");
            }
        }

        // ── 12. Employee documents (+ versions; touches storage) ─────────────────────
        private async Task SeedEmployeeDocumentsAsync(List<int> userIds)
        {
            try
            {
                var have = await _db.EmployeeDocuments.CountAsync();
                var need = Need<EmployeeDocument>(have);
                if (need <= 0) return;

                var employeeIds = await _db.Users.Select(u => u.Id).ToListAsync();
                if (employeeIds.Count == 0) return;

                var folderIds = await _db.DocumentFolders.Select(f => f.Id).ToListAsync();
                var categoryIds = await _db.DocumentCategories.Select(c => c.Id).ToListAsync();
                if (folderIds.Count == 0 || categoryIds.Count == 0) return;

                var titles = new[]
                {
                    "Passport Scan", "Employment Contract", "Degree Certificate", "National ID",
                    "Offer Letter", "Medical Aid Card", "Tax Certificate", "Training Certificate",
                    "Performance Review 2025", "Driver's License", "NDA", "CV", "Police Clearance"
                };
                var confidentiality = new[]
                {
                    ConfidentialityLevel.Internal, ConfidentialityLevel.Confidential,
                    ConfidentialityLevel.Restricted, ConfidentialityLevel.Confidential
                };

                for (int i = 0; i < need; i++)
                {
                    try
                    {
                        var employeeId = employeeIds[i % employeeIds.Count];
                        var creator = await _db.Users.FirstOrDefaultAsync(u => u.Id == employeeId);
                        var creatorName = creator != null ? $"{creator.FirstName} {creator.LastName}" : "HR Admin";
                        var created = DateTime.Now.AddDays(-_rng.Next(5, 365));

                        // Some expiring soon / expired for the dashboard.
                        DateTime? expiry = (i % 3) switch
                        {
                            0 => DateTime.Now.AddDays(_rng.Next(5, 25)),   // expiring soon
                            1 => DateTime.Now.AddDays(-_rng.Next(1, 30)),  // expired
                            _ => (DateTime?)null,
                        };

                        var bytes = System.Text.Encoding.UTF8.GetBytes($"Demo document {i} — Axis IT");
                        StoredFileResult stored;
                        using (var ms = new MemoryStream(bytes))
                        {
                            stored = await _storage.SaveAsync(ms, $"demo-doc-{i}.txt", "text/plain");
                        }

                        var doc = new EmployeeDocument
                        {
                            EmployeeId = employeeId,
                            FolderId = folderIds[i % folderIds.Count],
                            CategoryId = categoryIds[i % categoryIds.Count],
                            Title = titles[i % titles.Length],
                            Description = "Demo employee document for the EFM dashboard and search.",
                            ConfidentialityLevel = confidentiality[i % confidentiality.Length],
                            Status = DocumentStatus.Active,
                            IssueDate = created.AddDays(-_rng.Next(30, 400)),
                            ExpiryDate = expiry,
                            CreatedAt = created,
                            CreatedById = employeeId,
                            CreatedByName = creatorName
                        };
                        _db.EmployeeDocuments.Add(doc);
                        await _db.SaveChangesAsync(); // get doc.Id

                        var version = new DocumentVersion
                        {
                            EmployeeDocumentId = doc.Id,
                            VersionNumber = 1,
                            FileName = $"demo-doc-{i}.txt",
                            StoredKey = stored.StoredKey,
                            StorageProvider = _storage.ProviderType,
                            ContentType = stored.ContentType,
                            FileSizeBytes = stored.SizeBytes,
                            Sha256 = stored.Sha256,
                            IsCurrent = true,
                            UploadedAt = created,
                            UploadedById = employeeId,
                            UploadedByName = creatorName
                        };
                        _db.DocumentVersions.Add(version);
                        await _db.SaveChangesAsync(); // get version.Id

                        doc.CurrentVersionId = version.Id;
                        await _db.SaveChangesAsync();
                    }
                    catch (Exception exInner)
                    {
                        _logger.LogWarning(exInner, "Seeding EmployeeDocument #{Index} failed.", i);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seeding EmployeeDocuments failed.");
            }
        }
    }
}
