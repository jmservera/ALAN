using ALAN.Shared.Services.Memory;
using ALAN.Shared.Services.Queue;
using ALAN.Shared.Models;
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

// Register memory services (same as Agent for shared state) - Azure Storage is required
var storageConnectionString = builder.Configuration["AzureStorage:ConnectionString"]
    ?? builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
    ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

if (string.IsNullOrEmpty(storageConnectionString))
{
    throw new InvalidOperationException("Azure Storage connection string is required. Set AZURE_STORAGE_CONNECTION_STRING environment variable or AzureStorage:ConnectionString in appsettings.json");
}

builder.Services.AddSingleton<ILongTermMemoryService>(sp =>
    new AzureBlobLongTermMemoryService(
        storageConnectionString,
        sp.GetRequiredService<ILogger<AzureBlobLongTermMemoryService>>()));

builder.Services.AddSingleton<IShortTermMemoryService>(sp =>
    new AzureBlobShortTermMemoryService(
        storageConnectionString,
        sp.GetRequiredService<ILogger<AzureBlobShortTermMemoryService>>()));

// Register queue services for human interaction
builder.Services.AddSingleton<IMessageQueue<HumanInput>>(sp =>
    new AzureStorageQueueService<HumanInput>(
        storageConnectionString,
        "human-inputs",
        sp.GetRequiredService<ILogger<AzureStorageQueueService<HumanInput>>>()));

builder.Services.AddSingleton<IMessageQueue<ALAN.Shared.Models.ChatResponse>>(sp =>
    new AzureStorageQueueService<ALAN.Shared.Models.ChatResponse>(
        storageConnectionString,
        "chat-responses",
        sp.GetRequiredService<ILogger<AzureStorageQueueService<ALAN.Shared.Models.ChatResponse>>>()));

// Chat request queue is the same as human input queue since chat requests are HumanInput type
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMessageQueue<HumanInput>>());

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
