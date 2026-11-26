using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Portfolio.Api.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Stateless, so any lifetime is safe. Scoped matches the function class that uses
// it, which the worker creates once per invocation.
builder.Services.AddScoped<ContactService>();

builder.Build().Run();
