using Microsoft.AspNetCore.Mvc;

namespace RestaurantOS.Api.Controllers;

internal static class ApiProblem
{
    public static ObjectResult Create(int statusCode, string code, string detail) =>
        new(new ProblemDetails
        {
            Status = statusCode,
            Title = code,
            Detail = detail,
            Extensions = { ["code"] = code },
        })
        {
            StatusCode = statusCode,
        };
}
