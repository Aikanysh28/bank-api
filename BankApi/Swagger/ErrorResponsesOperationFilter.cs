using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BankApi.Swagger;

/// <summary>Documents the problem-details error responses of every operation.</summary>
public class ErrorResponsesOperationFilter : IOperationFilter
{
    private static readonly Dictionary<int, string> Descriptions = new()
    {
        [StatusCodes.Status400BadRequest] = "Input validation failed",
        [StatusCodes.Status401Unauthorized] = "Missing or invalid credentials",
        [StatusCodes.Status403Forbidden] = "The user's role is not allowed to perform the operation",
        [StatusCodes.Status404NotFound] = "Resource not found",
        [StatusCodes.Status409Conflict] = "Uniqueness violation or concurrent modification",
        [StatusCodes.Status422UnprocessableEntity] = "Business rule violation"
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var description = context.ApiDescription;
        var metadata = description.ActionDescriptor.EndpointMetadata;
        var statusCodes = new SortedSet<int>();

        if (description.ParameterDescriptions.Any(p => p.Source == BindingSource.Body || p.Source == BindingSource.Query))
            statusCodes.Add(StatusCodes.Status400BadRequest);

        if (description.ParameterDescriptions.Any(p => p.Source == BindingSource.Path))
            statusCodes.Add(StatusCodes.Status404NotFound);

        if (!metadata.OfType<IAllowAnonymous>().Any())
        {
            statusCodes.Add(StatusCodes.Status401Unauthorized);
            if (metadata.OfType<IAuthorizeData>().Any(a => !string.IsNullOrEmpty(a.Roles)))
                statusCodes.Add(StatusCodes.Status403Forbidden);
        }

        foreach (var attribute in metadata.OfType<ProducesErrorsAttribute>())
            statusCodes.UnionWith(attribute.StatusCodes);

        foreach (var statusCode in statusCodes)
        {
            var type = statusCode == StatusCodes.Status400BadRequest ? typeof(ValidationProblemDetails) : typeof(ProblemDetails);
            operation.Responses.TryAdd(statusCode.ToString(), new OpenApiResponse
            {
                Description = Descriptions.GetValueOrDefault(statusCode, "Error"),
                Content =
                {
                    ["application/problem+json"] = new OpenApiMediaType
                    {
                        Schema = context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository)
                    }
                }
            });
        }
    }
}
