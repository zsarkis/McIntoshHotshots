using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using McIntoshHotshots.Components;
using McIntoshHotshots.db;
using McIntoshHotshots.Factory;
using McIntoshHotshots.Repo;
using McIntoshHotshots.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages(); // Add Razor Pages services

// Configure the database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    return new DbConnectionFactory(connectionString);
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin")); // Adjust as per your requirements
});

builder.Services.AddScoped<IPlayerRepo, PlayerRepo>();
builder.Services.AddScoped<ITournamentRepo, TournamentRepo>();
builder.Services.AddScoped<IMatchSummaryRepo, MatchSummaryRepo>();

builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IDartConnectReportParsingService, DartConnectReportParsingService>();

// Add Identity services
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false; // Enforce email confirmation
    })
    .AddRoles<IdentityRole>() // Enable role support
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add authentication and authorization
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

// Seed roles and users
using (var scope = app.Services.CreateScope())
{
    var scopedServices = scope.ServiceProvider;

    var userManager = scopedServices.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();

    var adminUser = await userManager.FindByEmailAsync("zacherysarkis@gmail.com");
    if (adminUser != null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
    {
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Enable authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map Razor Pages for Identity UI
app.MapRazorPages();

app.Run();
