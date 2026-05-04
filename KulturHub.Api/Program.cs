using FluentValidation;
using KulturHub.Api.Endpoints;
using KulturHub.Application;
using KulturHub.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Supabase:DiscoveryUrl"]
            ?? throw new InvalidOperationException("Supabase:DiscoveryUrl is not configured.");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience    = "authenticated",
            ValidateLifetime = true,
            ClockSkew        = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var ex = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;

        if (ex is ValidationException validationEx)
        {
            var errors = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "One or more validation errors occurred.",
                status = 400,
                errors
            });
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
    });
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapEventEndpoints();
app.MapAuthEndpoints();
app.MapOrganisationEndpoints();

app.Run();
