using KulturHub.Api.Endpoints;
using KulturHub.Api.Extensions;
using KulturHub.Application;
using KulturHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddKulturHubJson()
    .AddKulturHubOpenApi()
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddKulturHubAuth(builder.Configuration)
    .AddKulturHubCors(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseKulturHubExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

app.Run();
