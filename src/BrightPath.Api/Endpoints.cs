namespace BrightPath.Api;

public static class Endpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/bookings", (CreateBookingRequest req, BookingService svc) =>
            Run(async () =>
            {
                var booking = await svc.Book(req);
                return Results.Created($"/bookings/{booking.Id}", booking);
            }));

        app.MapPost("/bookings/{id}/move", (string id, MoveRequest req, BookingService svc) =>
            Run(async () => Results.Ok(await svc.Move(id, req))));

        app.MapPost("/bookings/{id}/cancel", (string id, CancelRequest req, BookingService svc) =>
            Run(async () => Results.Ok(await svc.Cancel(id, req))));

        app.MapGet("/bookings", (string? date, string? tutorId, BookingService svc) =>
            Run(async () => Results.Ok(await svc.List(date, tutorId))));

        app.MapGet("/bookings/{id}/changes", (string id, BookingService svc) =>
            Run(async () => Results.Ok(await svc.Changes(id))));
    }

    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (RuleException e)
        {
            return Results.Json(new { code = e.Code, rule = e.Rule, message = e.Message },
                statusCode: 409);
        }
        catch (BadInputException e)
        {
            return Results.Json(new { e.Message }, statusCode: 422);
        }
        catch (NotFoundException) { return Results.NotFound(); }
    }
}
