using FluentValidation;
using KulturHub.Api.Requests;
using KulturHub.Application;
using KulturHub.Application.Features.Events;
using KulturHub.Application.Features.Events.CreateEvent;
using KulturHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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

app.MapPost("/events", async (CreateEventRequest req, IEventService eventService, CancellationToken ct) =>
{
    var result = await eventService.CreateEventAsync(
        new CreateEventInput(req.Title, req.StartTime, req.EndTime, req.Address, req.Description),
        ct);

    return result.Match(
        id => Results.Created($"/events/{id}", new { id }),
        errors => Results.Problem(
            title: errors[0].Description,
            statusCode: StatusCodes.Status500InternalServerError));
});

app.Run();
