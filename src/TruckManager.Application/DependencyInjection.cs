using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Behaviors;
using TruckManager.Application.Cqrs;

namespace TruckManager.Application;

// Phase 5   Single-call composition for the Application layer: registers the two dispatchers, pipeline behaviors, every command/query handler discovered in this assembly via the CQRS marker interfaces, and every FluentValidator.
//
// EfUnitOfWork (the IUnitOfWork implementation) stays in Infrastructure so Api/Program.cs calls AddTruckManagerApplication BEFORE AddTruckManagerInfrastructure.
//
// Handler discovery is by closed-generic interface match (no string-based heuristics):
// the scanner walks every concrete public type in this assembly, finds every implemented closed ICommandHandler<>, ICommandHandler<,>, IQueryHandler<,> interface, and registers each.
// Adding a new handler is a single-file drop.
public static class DependencyInjection
{
    public static IServiceCollection AddTruckManagerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Assembly applicationAssembly = typeof(DependencyInjection).Assembly;

        // Dispatchers — scoped so they pick up the per-request scope.
        services.AddScoped<ICommandDispatcher, CommandDispatcher>()
                .AddScoped<IQueryDispatcher, QueryDispatcher>();

        // Pipeline behaviors — open-generic registration; resolved at SendAsync time.
        // [Phase 7: add LoggingBehavior<,> here, before ValidationBehavior, so it wraps the full pipeline.]
        // ValidationBehavior runs on both command and query pipelines.
        // UnitOfWorkBehavior is constrained to IBaseCommand — query types are skipped automatically.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        // Handlers — assembly scan over the open-generic CQRS interfaces.
        RegisterClosedGenericImplementations(services, applicationAssembly, typeof(ICommandHandler<>));
        RegisterClosedGenericImplementations(services, applicationAssembly, typeof(ICommandHandler<,>));
        RegisterClosedGenericImplementations(services, applicationAssembly, typeof(IQueryHandler<,>));

        // FluentValidators — assembly scan via the FluentValidation DI extension.
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    // Walks the assembly for every concrete public type, finds the closed-generic versions of `openInterfaceType` it implements, and registers the concrete type against each closed interface as scoped.
    private static void RegisterClosedGenericImplementations(IServiceCollection services, Assembly assembly, Type openInterfaceType)
    {
        foreach (Type concreteType in assembly.GetTypes())
        {
            if (IsAbstractOrInterfaceOrNonPublic(concreteType))
                continue;

            foreach (Type implementedInterface in concreteType.GetInterfaces())
            {
                bool isNonGenericOrInterfaceTypeMisMatch = !implementedInterface.IsGenericType || IsInterfaceTypeMismatched(openInterfaceType, implementedInterface);
                if (isNonGenericOrInterfaceTypeMisMatch)
                    continue;

                services.AddScoped(implementedInterface, concreteType);
            }
        }
    }

    private static bool IsAbstractOrInterfaceOrNonPublic(Type concreteType) 
        => concreteType.IsAbstract || concreteType.IsInterface || !concreteType.IsPublic;

    private static bool IsInterfaceTypeMismatched(Type openInterfaceType, Type implementedInterface) 
        => implementedInterface.GetGenericTypeDefinition() != openInterfaceType;
}
