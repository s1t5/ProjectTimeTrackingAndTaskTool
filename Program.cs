using Microsoft.EntityFrameworkCore;
using ProjektZeiterfassung.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Datenbank-Kontext konfigurieren
builder.Services.AddDbContext<ProjektDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ProjektDbConnection")));

// Antiforgery-Token hinzuf?gen
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Zeiterfassung}/{action=Index}/{id?}");
app.Run();