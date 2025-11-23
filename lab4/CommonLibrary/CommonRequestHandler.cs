using CommonLibrary;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

public static class CommonRequestHandler
{
    public static WebApplication RegisterGetRequest(
        WebApplication application,
        IKeyValueStorage storage)
    {
        application.MapGet("/get/{key}", (string key) =>
        {
            var value = storage.Get(key);
            return value is null
                ? Results.NotFound()
                : Results.Ok(value);
        });

        return application;
    }

    public static WebApplication RegisterGetAllRequest(
        WebApplication application,
        IKeyValueStorage storage)
    {
        application.MapGet("/getall", () => Results.Ok(storage.GetAll()));
        return application;
    }
}
