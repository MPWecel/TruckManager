using TruckManager.Application.Abstractions;

namespace TruckManager.UnitTests.TestHelpers;

internal sealed class FakeCurrentUserService(Guid? userId = null) : ICurrentUserService
{
    public Guid? UserId { get; } = userId;
    public bool HasPermission(string permission) => true;

    public static FakeCurrentUserService Anonymous() => new(null);
    public static FakeCurrentUserService WithUser(Guid userId) => new(userId);
}
