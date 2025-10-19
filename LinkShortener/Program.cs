using LinkShortener.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Show a friendly error page for non-success status codes
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseRouting();

// Ensure authentication middleware is registered before authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

// Create admin and user roles in database with error handling
var logger = app.Services.GetRequiredService<ILogger<Program>>();
try
{
    using (var scope = app.Services.CreateScope())
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var roles = new[] { "Administrator", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    logger.LogWarning("Failed to create role {Role}: {Errors}", role, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred while creating roles during startup.");
}

try
{
    using (var scope = app.Services.CreateScope())
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        string email = "admin@admin.com";
        string password = "Administrator!376";

        if (await userManager.FindByEmailAsync(email) == null)
        {
            var user = new IdentityUser();
            user.UserName = email;
            user.Email = email;

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                logger.LogWarning("Failed to create admin user {Email}: {Errors}", email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }
            else
            {
                var addRoleResult = await userManager.AddToRoleAsync(user, "Administrator");
                if (!addRoleResult.Succeeded)
                {
                    logger.LogWarning("Failed to add admin user to role: {Errors}", string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred while creating the admin user during startup.");
}

app.Run();
