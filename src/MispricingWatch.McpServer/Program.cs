using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MispricingWatch.McpServer;

var builder = Host.CreateApplicationBuilder(args);

// stdout is the protocol channel, so every log line goes to stderr.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<WorldService>();
builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();

var app = builder.Build();
app.Services.GetRequiredService<WorldService>();   // build the world at startup, not on the first call
await app.RunAsync();
