using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Recrutement_api.Middleware
{
    public class TenantLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var tenantId = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();

            using (LogContext.PushProperty("TenantId", tenantId ?? "Unknown"))
            {
                await _next(context);
            }
        }
    }
}
