using KulturHub.Api.Endpoints.Admin.Invitations;
using KulturHub.Api.Endpoints.Platform.Auth;
using KulturHub.Api.Endpoints.Public;
using KulturHub.Api.Extensions;
using KulturHub.Application;
using KulturHub.Infrastructure;
using Scalar.AspNetCore;

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
{
    app.MapOpenApi("/openapi/{documentName}.json");
    app.MapScalarApiReference(options =>
    {
        options
            .AddDocument("public",   "KulturHub Public API",   "/openapi/public.json")
            .AddDocument("platform", "KulturHub Platform API", "/openapi/platform.json")
            .AddDocument("admin",    "KulturHub Admin API",    "/openapi/admin.json");
    });
}

app.UseKulturHubExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapInvitationEndpoints();
app.MapAuthEndpoints();

app.Run();
