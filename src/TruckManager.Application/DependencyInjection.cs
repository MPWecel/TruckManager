using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Cqrs;

namespace TruckManager.Application;

// Phase 5 / Section A  Single-call composition for the Application layer: registers the two dispatchers, every command/query handler discovered in this assembly via the CQRS marker interfaces, and every FluentValidator.
//
// Pipeline behaviors (ValidationBehavior, UnitOfWorkBehavior) are NOT registered here - they're added in Section B alongside the IUnitOfWork abstraction, and 'EfUnitOfWork' stays in Infrastructure
// (so Api/Program.cs calls AddTruckManagerApplication first, then AddTruckManagerInfrastructure for the EF-backed services).
//
// Discovery is by closed-generic interface match (no string-based heuristics):
// the scanner walks every concrete public type in this assembly, finds every implemented closed `ICommandHandler<>`, `ICommandHandler<,>`, `IQueryHandler<,>` interface, and registers the type against each.
// Adding a new handler is a single-file drop.
public static class DependencyInjection
{
    public static IServiceCollection AddTruckManagerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Assembly applicationAssembly = typeof(DependencyInjection).Assembly;

        // Dispatchers — scoped so they pick up the per-request scope (the same scope the pipeline behaviors and handlers live in).
        services.AddScoped<ICommandDispatcher, CommandDispatcher>()
                .AddScoped<IQueryDispatcher, QueryDispatcher>();

        // Handlers — assembly scan over the open-generic CQRS interfaces.
        RegisterClosedGenericImplementations(services, applicationAssembly, typeof(ICommandHandler<>));
        RegisterClosedGenericImplementations(services, applicationAssembly, typeof(ICommandHandler<,>));
        RegisterClosedGenericImplementations(services, applicationAssembly, typeof(IQueryHandler<,>));

        // FluentValidators — assembly scan via the official FluentValidation extension.
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    // Walks the assembly for every concrete public type, finds the closed-generic versions of `openInterfaceType` it implements, and registers the concrete type against each closed interface as scoped.
    private static void RegisterClosedGenericImplementations(
                                                                IServiceCollection services,
                                                                Assembly assembly,
                                                                Type openInterfaceType
                                                            )
    {
        foreach (Type concreteType in assembly.GetTypes())
        {
            if (IsAbstractOrInterfaceOrNonPublic(concreteType))
                continue;

            foreach (Type implementedInterface in concreteType.GetInterfaces())
            {
                if (!implementedInterface.IsGenericType || IsInterfaceTypeMismatched(openInterfaceType, implementedInterface))
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
