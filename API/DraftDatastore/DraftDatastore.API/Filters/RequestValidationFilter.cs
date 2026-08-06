using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DraftDatastore.API.Filters;

public sealed class RequestValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failures = new Dictionary<string, string[]>();
        foreach (var argument in context.ActionArguments.Values.Where(x => x is not null))
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(argument!.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator) continue;
            var validationContextType = typeof(ValidationContext<>).MakeGenericType(argument.GetType());
            var validationContext = (IValidationContext)Activator.CreateInstance(validationContextType, argument)!;
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                failures[failure.PropertyName] = [failure.ErrorMessage];
            }
        }

        if (failures.Count == 0) { await next(); return; }
        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(failures) { Title = "One or more validation errors occurred.", Status = StatusCodes.Status400BadRequest });
    }
}
