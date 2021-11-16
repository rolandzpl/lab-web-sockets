public class HandleWebSocketMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<HandleWebSocketMiddleware> logger;

    public HandleWebSocketMiddleware(RequestDelegate next, ILogger<HandleWebSocketMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest)
        {

        }
        else
        {
            await next.Invoke(context);
        }
    }
}