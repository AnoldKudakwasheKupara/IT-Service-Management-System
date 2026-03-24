using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Hubs;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSignalR();

builder.Services.AddScoped<EmailService>();

builder.Services.AddSession();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<AuditService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapHub<ChatHub>("/chathub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    context.Database.Migrate();

    if (!context.Users.Any())
    {
        context.Users.Add(new User
        {
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@test.com",
            PasswordHash = "123456",
            IsActive = true,
            Role = Ticket.UserRole.Admin,
            CreatedAt = DateTime.Now
        });

        context.SaveChanges();
    }
}

app.Run();