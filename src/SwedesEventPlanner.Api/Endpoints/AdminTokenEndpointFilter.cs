using System.Security.Cryptography;
using System.Text;

namespace SwedesEventPlanner.Api.Endpoints;

internal sealed class AdminTokenEndpointFilter(IConfiguration configuration) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var configuredToken = configuration["Admin:Token"] ?? configuration["ADMIN_TOKEN"];

        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            return TypedResults.Problem(
                title: "Admin token is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var suppliedToken = GetSuppliedToken(context.HttpContext.Request);

        if (!TokenEquals(configuredToken, suppliedToken))
        {
            return TypedResults.Problem(
                title: "Admin token is required.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }

    private static string? GetSuppliedToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";

        if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return authorization[bearerPrefix.Length..].Trim();
        }

        return request.Headers["X-Admin-Token"].ToString();
    }

    private static bool TokenEquals(string expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);

        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
