using ALAN.Shared.Services.Memory;
using ALAN.Web.Hubs;
using ALAN.Web.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Register memory services (same as Agent for shared state)
var storageConnectionString = builder.Configuration["AzureStorage:ConnectionString"]
    ?? builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
    ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

if (!string.IsNullOrEmpty(storageConnectionString))
{
    builder.Services.AddSingleton<ILongTermMemoryService>(sp =>
        new AzureBlobLongTermMemoryService(
            storageConnectionString,
            sp.GetRequiredService<ILogger<AzureBlobLongTermMemoryService>>()));
}
else
{
    builder.Services.AddSingleton<ILongTermMemoryService, InMemoryLongTermMemoryService>();
}

builder.Services.AddSingleton<IShortTermMemoryService, InMemoryShortTermMemoryService>();

builder.Services.AddSingleton<AgentStateService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentStateService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHub<AgentHub>("/agenthub");

app.Run();
