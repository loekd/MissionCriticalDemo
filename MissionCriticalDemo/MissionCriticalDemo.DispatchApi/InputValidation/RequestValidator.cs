using FluentValidation;
using MissionCriticalDemo.Shared.Contracts;

namespace MissionCriticalDemo.DispatchApi.InputValidation
{
    /// <summary>
    /// Validates user input for <see cref="Request"/>
    /// </summary>
    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.RequestId).NotEmpty();
            RuleFor(x => x.AmountInGWh).ExclusiveBetween(0, 100);
        }
    }
}
