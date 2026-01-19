using TransactionProcessor.Api.Middleware;

namespace TransactionProcessor.Api.Extensions;

/// <summary>
/// Extension methods for registering custom JWT validation middleware
/// </summary>
public static class JwtValidationMiddlewareExtensions
{
    /// <summary>
    /// Adds custom JWT validation middleware to the pipeline.
    /// This middleware provides enhanced JWT validation with detailed logging
    /// and security checks per technical-decisions.md.
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <returns>Application builder for chaining</returns>
    /// <remarks>
    /// NOTE: This middleware is OPTIONAL if you're using ASP.NET Core's built-in
    /// JWT Bearer authentication (configured in Program.cs). The built-in handler
    /// already performs all required validations.
    /// 
    /// Use this middleware if you need:
    /// - Custom validation logic beyond the built-in handler
    /// - Enhanced logging for security auditing
    /// - Additional claim validation or transformation
    /// 
    /// Placement: Add AFTER UseAuthentication() and BEFORE protected endpoints.
    /// </remarks>
    public static IApplicationBuilder UseJwtValidation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<JwtValidationMiddleware>();
    }
}
