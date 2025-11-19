using Microsoft.AspNetCore.Builder;

namespace CommonLibrary;

public static class CommonRequestHandler
{
    public static WebApplication RegisterGetRequest(WebApplication application, IKeyValueStorage storage)
    {
        application.MapGet("/get/{key}", (string key) => storage.Get(key));
        return application;
    }
}