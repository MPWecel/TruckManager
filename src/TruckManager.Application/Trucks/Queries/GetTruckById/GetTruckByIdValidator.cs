using FluentValidation;

namespace TruckManager.Application.Trucks.Queries.GetTruckById;

public sealed class GetTruckByIdValidator : AbstractValidator<GetTruckByIdQuery>
{
    public GetTruckByIdValidator() => RuleFor(x => x.TruckId).NotEmpty();
}
