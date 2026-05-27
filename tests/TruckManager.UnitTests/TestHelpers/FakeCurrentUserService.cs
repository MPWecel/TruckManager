using TruckManager.Application.Abstractions;

namespace TruckManager.UnitTests.TestHelpers;

internal sealed class FakeCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; }
    public bool HasPermission(string permission) => true;

    public FakeCurrentUserService(Guid? userId = null) => UserId = userId;

    public static FakeCurrentUserService Anonymous() => new(null);
    public static FakeCurrentUserService WithUser(Guid userId) => new(userId);
}
