namespace TransactionProcessor.Api.Middleware;

/// <summary>
/// Middleware to add security headers to all responses
/// Implements OWASP security best practices and prevents common attacks
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    /// <summary>
    /// Constructor
    /// </summary>
    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers to response
        // CSP is customized in AddSecurityHeaders to allow Swagger UI to function
        AddSecurityHeaders(context, context.Response);

        await _next(context);
    }

    /// <summary>
    /// Adds security headers to HTTP response
    /// </summary>
    /// <remarks>
    /// Security Headers Added:
    /// - Strict-Transport-Security: Enforces HTTPS
    /// - X-Content-Type-Options: Prevents MIME type sniffing
    /// - X-Frame-Options: Prevents clickjacking (OWASP A04)
    /// - X-XSS-Protection: Browser XSS protection (legacy, for older browsers)
    /// - Content-Security-Policy: Prevents injection attacks (OWASP A03, A07)
    /// - Referrer-Policy: Controls referrer information
    /// - Permissions-Policy: Controls browser features
    /// </remarks>
    private void AddSecurityHeaders(HttpContext context, HttpResponse response)
    {
        // HTTPS enforcement with HSTS
        // max-age=31536000 (1 year), include subdomains, preload list
        response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");

        // Prevent MIME type sniffing (X-Content-Type-Options: nosniff)
        // Forces browser to respect Content-Type header
        response.Headers.Append("X-Content-Type-Options", "nosniff");

        // Prevent clickjacking (OWASP A04 - Insecure Deserialization / A06 - Broken Access Control)
        // DENY: Page cannot be displayed in a frame
        response.Headers.Append("X-Frame-Options", "DENY");

        // Legacy XSS protection header (for older browsers)
        response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // Content Security Policy (CSP) - Prevent injection attacks
        // OWASP A03 (Injection), A07 (Identification and Authentication Failures)
        // Restrictive policy: only allow resources from same origin
        // Note: 'unsafe-inline' is required for Swagger UI which uses inline scripts
        // This is acceptable for Swagger UI as it's a development/documentation tool
        var path = context.Request.Path.Value ?? string.Empty;
        var isSwaggerPath = path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
        
        if (isSwaggerPath)
        {
            // More permissive CSP for Swagger UI to allow inline scripts
            response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: https:; " +
                "font-src 'self'; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'");
        }
        else
        {
            // Restrictive CSP for API endpoints
            response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self'; " +
                "img-src 'self' data: https:; " +
                "font-src 'self'; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'");
        }

        // Referrer Policy - Control referrer information
        response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // Permissions Policy (formerly Feature Policy)
        // Disable potentially dangerous features
        response.Headers.Append("Permissions-Policy",
            "accelerometer=(), " +
            "camera=(), " +
            "geolocation=(), " +
            "gyroscope=(), " +
            "magnetometer=(), " +
            "microphone=(), " +
            "payment=(), " +
            "usb=()");
    }
}

/// <summary>
/// Extension methods for security headers middleware
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// Adds security headers middleware to the request pipeline
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
