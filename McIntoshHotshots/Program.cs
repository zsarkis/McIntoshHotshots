using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using McIntoshHotshots.Components;
using McIntoshHotshots.db;
using McIntoshHotshots.Factory;
using McIntoshHotshots.Repo;
using McIntoshHotshots.Services;
using Dapper;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages(); // Add Razor Pages services
builder.Services.AddControllers(); // Add API controllers

// Configure the database context
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
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
builder.Services.AddScoped<ILegRepo, LegRepo>();
builder.Services.AddScoped<ILegDetailRepo, LegDetailRepo>();
builder.Services.AddScoped<IStatsRepo, StatsRepo>();

builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IDartConnectReportParsingService, DartConnectReportParsingService>();
builder.Services.AddScoped<IEloCalculationService, EloCalculationService>();
builder.Services.AddScoped<ILiveMatchService, LiveMatchService>();
builder.Services.AddScoped<IUserPerformanceService, UserPerformanceService>();
builder.Services.AddScoped<IPromptBuilderService, PromptBuilderService>();
builder.Services.AddScoped<IToolDefinitionService, ToolDefinitionService>();
builder.Services.AddScoped<ICoachingService, CoachingService>();
builder.Services.AddScoped<CoachingDebugService>();

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

// Add HttpClient registration to the services container
builder.Services.AddHttpClient("OpenAI", client => 
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

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

// Map API controllers
app.MapControllers();

// Map Razor Pages for Identity UI
app.MapRazorPages();

app.Run();
