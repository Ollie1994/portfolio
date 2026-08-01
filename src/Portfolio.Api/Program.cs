using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Portfolio.Api.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Stateless and cheap to construct; scoped keeps the ILogger category correct
// per invocation without holding state between them.
builder.Services.AddScoped<ContactService>();

builder.Build().Run();
