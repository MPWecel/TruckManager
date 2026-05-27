using FluentValidation;

namespace TruckManager.Application.Trucks.Commands.DeleteTruck;

public sealed class DeleteTruckValidator : AbstractValidator<DeleteTruckCommand>
{
    public DeleteTruckValidator() => RuleFor(x => x.TruckId).NotEmpty();
}
