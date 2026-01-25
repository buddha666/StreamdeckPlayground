using StreamdeckPlayground.Web.Components;
using StreamdeckPlayground.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register our ProgressService as a singleton
// Singleton ensures ONE shared instance across the entire application
// This is critical so all consumers (UI, API) see the same state
builder.Services.AddSingleton<ProgressService>();

// Add controllers for REST API endpoints
// This enables the /api/progress endpoints
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

// Map API controllers for REST endpoints
// This enables routes like /api/progress, /api/progress/inc, etc.
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
