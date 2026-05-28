using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

using TruckManager.Common.Constants;
using TruckManager.Common.Results;
using TruckManager.Domain.Enums;
using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Trucks.Commands.ChangeTruckStatus;
using TruckManager.Application.Trucks.Commands.CreateTruck;
using TruckManager.Application.Trucks.Commands.DeleteTruck;
using TruckManager.Application.Trucks.Commands.UpdateTruck;
using TruckManager.Application.Trucks.DTOs;
using TruckManager.Application.Trucks.Queries.GetTruckById;
using TruckManager.Application.Trucks.Queries.ListTrucks;
using TruckManager.Api.Extensions;
using TruckManager.Api.Trucks.Requests;

namespace TruckManager.Api.Trucks;

// Phase 6 / Section E.   REST surface for the Truck aggregate.
//
// Six actions per architecture §14, all mediated by the Phase 5 dispatchers — this controller is intentionally a thin translation layer (HTTP <-> command/query).
// All business logic lives in handlers + the aggregate. Result<T> failures are mapped to ProblemDetails in-band by ApiResultExtensions (Section C);
// unhandled exceptions are caught by GlobalExceptionHandler (Section B) and never surface as a 200/204.
//
// V1 TenantId: every command site uses Tenants.DefaultTenantId until Phase 9 wires real claims (per decision #8; the exact source — interface member vs separate context vs middleware-read — is the open Phase 9 decision).
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/trucks")]
public sealed class TrucksController : ControllerBase
{
    private readonly ICommandDispatcher _commands;
    private readonly IQueryDispatcher   _queries;

    public TrucksController(ICommandDispatcher commands, IQueryDispatcher queries)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(queries);

        _commands   = commands;
        _queries    = queries;
    }

    // ---- Commands ---------------------------------------------------------------------

    // POST /api/v1/trucks
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTruck([FromBody] CreateTruckRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        CreateTruckCommand command = new(
                                            // [Phase 9: source TenantId from authenticated request context]
                                            TenantId: Tenants.DefaultTenantId,
                                            Code: request.Code,
                                            Name: request.Name,
                                            Description: request.Description,
                                            InitialStatus: request.InitialStatus
                                        );

        Result<TruckId> result = await _commands.SendAsync(command, cancellationToken);

        return result.ToCreatedResult(
                                         actionName: nameof(GetTruckById),
                                         routeValues: new { id = result.Value?.Value }
                                     );
    }

    // PUT /api/v1/trucks/{id}
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTruck([FromRoute] Guid id, [FromBody] UpdateTruckRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        UpdateTruckCommand command = new(
                                            TruckId: id,
                                            Name: request.Name,
                                            Description: request.Description
                                        );

        Result result = await _commands.SendAsync(command, cancellationToken);

        return result.ToNoContentResult();
    }

    // PATCH /api/v1/trucks/{id}/status
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeTruckStatus([FromRoute] Guid id, [FromBody] ChangeTruckStatusRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ChangeTruckStatusCommand command = new(
                                                  TruckId: id,
                                                  NewStatus: request.NewStatus
                                              );

        Result result = await _commands.SendAsync(command, cancellationToken);

        return result.ToNoContentResult();
    }

    // DELETE /api/v1/trucks/{id}
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteTruck([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        DeleteTruckCommand command = new(TruckId: id);

        Result result = await _commands.SendAsync(command, cancellationToken);

        return result.ToNoContentResult();
    }

    // ---- Queries ----------------------------------------------------------------------

    // GET /api/v1/trucks?page=1&pageSize=50&status=2
    [HttpGet]
    [ProducesResponseType<PagedListDto<TruckSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListTrucks([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] ETruckStatus? status = null, CancellationToken cancellationToken = default)
    {
        ListTrucksQuery query = new(
                                       Page: page,
                                       PageSize: pageSize,
                                       StatusFilter: status
                                   );

        Result<PagedListDto<TruckSummaryDto>> result = await _queries.SendAsync(query, cancellationToken);

        return result.ToOkResult();
    }

    // GET /api/v1/trucks/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TruckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTruckById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        GetTruckByIdQuery query = new(TruckId: id);

        Result<TruckDto> result = await _queries.SendAsync(query, cancellationToken);

        return result.ToOkResult();
    }
}
