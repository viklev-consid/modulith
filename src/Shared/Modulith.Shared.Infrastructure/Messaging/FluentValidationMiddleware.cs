using FluentValidation;
using Wolverine;

namespace Modulith.Shared.Infrastructure.Messaging;

public sealed class FluentValidationMiddleware(IServiceProvider serviceProvider)
{
    public async Task<HandlerContinuation> BeforeAsync(
        Envelope envelope,
        CancellationToken cancellationToken)
    {
        if (envelope.Message is not { } message)
        {
            return HandlerContinuation.Continue;
        }

        var validatorType = typeof(IValidator<>).MakeGenericType(message.GetType());
        if (serviceProvider.GetService(validatorType) is not IValidator validator)
        {
            return HandlerContinuation.Continue;
        }

        var context = new ValidationContext<object>(message);
        var result = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);

        if (result.IsValid)
        {
            return HandlerContinuation.Continue;
        }

        throw new ValidationException(result.Errors);
    }
}
