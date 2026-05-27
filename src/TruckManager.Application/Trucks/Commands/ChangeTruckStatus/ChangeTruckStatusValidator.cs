using FluentValidation;

namespace TruckManager.Application.Trucks.Commands.ChangeTruckStatus;

public sealed class ChangeTruckStatusValidator : AbstractValidator<ChangeTruckStatusCommand>
{
    public ChangeTruckStatusValidator()
    {
        RuleFor(x => x.TruckId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}
