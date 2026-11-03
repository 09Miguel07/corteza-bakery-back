using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Models;
using Corteza.Application.Common.Time;
using Corteza.Application.Common.Validation;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Inventory;

public sealed class InventoryMovementService(IAppDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    /// <summary>
    /// Registra una entrada o salida. La regla ("Stock insuficiente") vive en <see cref="Supply"/>;
    /// aquí solo se carga, se delega y se guarda. Si otro usuario movió el mismo insumo al mismo tiempo,
    /// el RowVersion hace fallar el guardado (409) en vez de pisar el stock.
    /// </summary>
    public async Task<MovementDto> RegisterAsync(MovementCreateRequest request, CancellationToken ct)
    {
        if (request.Type == MovementType.Adjustment)
            throw new BusinessRuleException("Los ajustes de inventario todavía no están habilitados. Registra una entrada o una salida.");

        var userId = currentUser.RequireUserId();
        var today = time.ColombiaToday();
        var movementDate = request.MovementDate ?? today;
        if (movementDate > today)
            throw new BusinessRuleException("La fecha del movimiento no puede ser futura.");

        var supply = await db.Supplies.FirstOrDefaultAsync(s => s.Id == request.SupplyId, ct)
            ?? throw NotFoundException.For("el insumo", request.SupplyId);

        if (!supply.IsActive)
            throw new BusinessRuleException($"El insumo \"{supply.Name}\" está inactivo.");

        if (request.SupplierId is { } supplierId && !await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
            throw new BusinessRuleException("El proveedor elegido no existe.");

        var movement = request.Type == MovementType.In
            ? supply.RegisterEntry(request.Quantity, userId, movementDate, request.SupplierId,
                location: Normalize.OptionalText(request.Location),
                expirationDate: request.ExpirationDate,
                notes: Normalize.OptionalText(request.Notes))
            : supply.RegisterExit(request.Quantity, userId, movementDate, Normalize.OptionalText(request.Notes));

        supply.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await GetAsync(movement.Id, ct);
    }

    public async Task<PagedResult<MovementDto>> ListBySupplyAsync(int supplyId, MovementQuery q, CancellationToken ct)
    {
        if (!await db.Supplies.AnyAsync(s => s.Id == supplyId, ct))
            throw NotFoundException.For("el insumo", supplyId);

        var query = db.InventoryMovements.AsNoTracking().Where(m => m.SupplyId == supplyId);
        var total = await query.CountAsync(ct);
        var items = await Project(query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Skip(q.Skip).Take(q.PageSize)).ToListAsync(ct);
        return new PagedResult<MovementDto>(items, total, q.Page, q.PageSize);
    }

    private async Task<MovementDto> GetAsync(long id, CancellationToken ct) =>
        await Project(db.InventoryMovements.AsNoTracking().Where(m => m.Id == id)).FirstAsync(ct);

    private static IQueryable<MovementDto> Project(IQueryable<InventoryMovement> query) =>
        query.Select(m => new MovementDto(
            m.Id, m.SupplyId, m.Supply.Name, m.Type, m.Quantity, m.StockAfter,
            m.SupplierId, m.Supplier != null ? m.Supplier.Name : null,
            m.MovementDate, m.Location, m.ExpirationDate, m.Notes,
            m.CreatedBy.Employee != null ? m.CreatedBy.Employee.FirstNames + " " + m.CreatedBy.Employee.LastNames : m.CreatedBy.Username,
            m.CreatedAt));
}
