using System.Diagnostics;

namespace wander_wallet_chat.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestStartTime = DateTime.UtcNow;
             
            var traceId = GetTraceId(context);
            var spanId = Activity.Current?.SpanId.ToString() ?? "unknown";
             
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = context.TraceIdentifier,
                ["TraceId"] = traceId,
                ["SpanId"] = spanId,
                ["Method"] = context.Request.Method,
                ["Path"] = context.Request.Path.Value ?? "",
                ["QueryString"] = context.Request.QueryString.Value ?? "",
                ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
                ["Origin"] = context.Request.Headers.Origin.ToString(),
                ["ContentType"] = context.Request.ContentType ?? "",
                ["ContentLength"] = context.Request.ContentLength,
                ["RemoteIP"] = GetClientIP(context),
                ["RequestTime"] = requestStartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            });
             
            _logger.LogInformation("Incoming request: {Method} {Path}",
                context.Request.Method,
                context.Request.Path.Value);
             
            if (!string.IsNullOrEmpty(traceId))
            {
                context.Response.Headers.Append("X-Cloud-Trace-Context", traceId);
            }

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during request processing");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                 
                var logLevel = context.Response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;
                _logger.Log(logLevel,
                    "Request completed: {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
                 
                _logger.LogInformation("Request metrics: {Metrics}", new
                {
                    StatusCode = context.Response.StatusCode,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                    ResponseLength = context.Response.ContentLength,
                    IsSuccess = context.Response.StatusCode < 400,
                    EndpointCategory = GetEndpointCategory(context.Request.Path.Value ?? "")
                });
            }
        }

        private static string GetTraceId(HttpContext context)
        { 
            var traceHeader = context.Request.Headers["X-Cloud-Trace-Context"].FirstOrDefault();
            if (!string.IsNullOrEmpty(traceHeader))
            {
                var parts = traceHeader.Split('/');
                if (parts.Length > 0)
                { 
                    var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "unknown-project";
                    return $"projects/{projectId}/traces/{parts[0]}";
                }
            }

            return Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        }

        private static string GetClientIP(HttpContext context)
        { 
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIP = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIP))
            {
                return realIP;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private static string GetEndpointCategory(string path)
        {
            if (path.StartsWith("/chat"))
                return "chat";
            if (path.StartsWith("/health"))
                return "health";

            return "other";
        }
    }
}