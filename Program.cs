using Music2._0.Services;
using YoutubeExplode;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.Configure<AudiusOptions>(
    builder.Configuration.GetSection(AudiusOptions.SectionName));
builder.Services.Configure<OpenSubsonicOptions>(
    builder.Configuration.GetSection(OpenSubsonicOptions.SectionName));

builder.Services.AddHttpClient<AudiusService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
})
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

builder.Services.AddHttpClient<OpenSubsonicService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

builder.Services.AddHttpClient<SyncedLyricsService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddSingleton<YoutubeClient>();
builder.Services.AddScoped<YouTubeMusicService>();
builder.Services.AddScoped<MusicService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
