using FileRepo.Core.Common;
using Microsoft.AspNetCore.Mvc;
namespace FileRepo.Web.Extensions;
public static class AppResultExtensions
{
    public static IActionResult ToActionResult(this AppResult result, ControllerBase c, int successStatus = 200)
    {
        if (result.IsSuccess) return c.StatusCode(successStatus);
        return result.Status switch { ResultStatus.Invalid => c.BadRequest(new ValidationProblemDetails(result.ValidationErrors.GroupBy(x => x.Field).ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray()))), ResultStatus.NotFound => c.NotFound(new ProblemDetails { Detail = result.Messages.FirstOrDefault() }), ResultStatus.Conflict => c.Conflict(new ProblemDetails { Detail = result.Messages.FirstOrDefault() }), ResultStatus.PayloadTooLarge => c.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: result.Messages.FirstOrDefault()), _ => c.Problem() };
    }
    public static IActionResult ToActionResult<T>(this AppResult<T> result, ControllerBase c, Func<T, IActionResult> success)
    {
        if (result.IsSuccess && result.Value is not null) return success(result.Value);
        return ((AppResult)result).ToActionResult(c);
    }
}
