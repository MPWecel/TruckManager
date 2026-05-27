using FluentValidation;

using TruckManager.Domain.ValueObjects;

namespace TruckManager.Application.Trucks.Commands.CreateTruck;

public sealed class CreateTruckValidator : AbstractValidator<CreateTruckCommand>
{
    public CreateTruckValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();

        // VO normalizes to uppercase before validating, so allow lowercase here.
        const string truckCodeValidationRegexp = @"^[A-Za-z0-9]+$";
        const string truckCodeValidationErrorMessage = "Truck code must contain only letters and digits.";
        RuleFor(x => x.Code).NotEmpty()
                            .MaximumLength(TruckCode.MaxLength)
                            .Matches(truckCodeValidationRegexp)
                            .WithMessage(truckCodeValidationErrorMessage);

        RuleFor(x => x.Name).NotEmpty()
                            .MaximumLength(TruckName.MaxLength);

        When(
                x => x.Description is not null, 
                () => RuleFor(x => x.Description!).MaximumLength(TruckDescription.MaxLength)
            );

        RuleFor(x => x.InitialStatus).IsInEnum();
    }
}
