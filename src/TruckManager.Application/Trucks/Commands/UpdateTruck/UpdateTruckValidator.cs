using FluentValidation;

using TruckManager.Domain.ValueObjects;

namespace TruckManager.Application.Trucks.Commands.UpdateTruck;

public sealed class UpdateTruckValidator : AbstractValidator<UpdateTruckCommand>
{
    public UpdateTruckValidator()
    {
        RuleFor(x => x.TruckId).NotEmpty();

        When(
                x => x.Name is not null, 
                () => RuleFor(x => x.Name!).NotEmpty()
                                           .MaximumLength(TruckName.MaxLength)
            );

        When(
                x => x.Description is not null, 
                () => RuleFor(x => x.Description!).MaximumLength(TruckDescription.MaxLength)
            );
    }
}
