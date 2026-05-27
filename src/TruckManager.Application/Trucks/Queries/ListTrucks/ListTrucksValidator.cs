using FluentValidation;

namespace TruckManager.Application.Trucks.Queries.ListTrucks;

public sealed class ListTrucksValidator : AbstractValidator<ListTrucksQuery>
{
    private const int MaxPageSize = 100;

    public ListTrucksValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1)
                                .LessThanOrEqualTo(MaxPageSize);

        When(
                x => x.StatusFilter.HasValue,
                () => RuleFor(x => x.StatusFilter!.Value).IsInEnum()
            );
    }
}
