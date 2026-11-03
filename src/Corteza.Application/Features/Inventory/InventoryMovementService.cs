using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Models;
using Corteza.Application.Common.Validation;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Inventory;

public sealed class InventoryMovementService(IAppDbContext db, ICurrentUser currentUser)
{
    /// <summary>
    /// Registra una entrada o salida y actualiza la existencia en una sola transacción.
    /// El UPDATE es atómico (stock = stock ± cantidad), así dos registros al mismo tiempo no se pisan.
    /// Una salida nunca deja el stock en negativo (regla del Sprint 2: "Stock insuficiente").
    /// </summary>
    public async Task<MovementDto> RegisterAsync(MovementCreateRequest request, CancellationToken ct)
    {
        if (request.Type == MovementType.Adjustment)
            throw new BusinessRuleException("Los ajustes de inventario todavía no están habilitados. Registra una entrada o una salida.");

        var userId = currentUser.RequireUserId();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var movementDate = request.MovementDate ?? today;
        if (movementDate > today)
            throw new BusinessRuleException("La fecha del movimiento no puede ser futura.");

        var supply = await db.Supplies.AsNoTracking()
            .Where(s => s.Id == request.SupplyId)
            .Select(s => new { s.Id, s.Name, s.UnitLabel, s.IsActive, s.CurrentStock })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFoundException.For("el insumo", request.SupplyId);

        if (!supply.IsActive)
            throw new BusinessRuleException($"El insumo \"{supply.Name}\" está inactivo.");

        if (request.SupplierId is { } supplierId && !await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
            throw new BusinessRuleException("El proveedor elegido no existe.");

        var quantity = request.Quantity;
        var now = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        int affected = request.Type == MovementType.In
            ? await db.Supplies
                .Where(s => s.Id == supply.Id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.CurrentStock, s => s.CurrentStock + quantity)
                    .SetProperty(s => s.UpdatedAt, (DateTime?)now), ct)
            : await db.Supplies
                .Where(s => s.Id == supply.Id && s.CurrentStock >= quantity)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.CurrentStock, s => s.CurrentStock - quantity)
                    .SetProperty(s => s.UpdatedAt, (DateTime?)now), ct);

        if (affected == 0)
        {
            var available = await db.Supplies.Where(s => s.Id == supply.Id).Select(s => s.CurrentStock).FirstAsync(ct);
            throw new BusinessRuleException(
                $"Stock insuficiente: hay {available:0.##} {supply.UnitLabel} de {supply.Name} y se intentan sacar {quantity:0.##}.");
        }

        var stockAfter = await db.Supplies.Where(s => s.Id == supply.Id).Select(s => s.CurrentStock).FirstAsync(ct);

        var movement = new InventoryMovement
        {
            SupplyId = supply.Id,
            Type = request.Type,
            Quantity = quantity,
            StockAfter = stockAfter,
            SupplierId = request.Type == MovementType.In ? request.SupplierId : null,
            MovementDate = movementDate,
            Location = Normalize.OptionalText(request.Location),
            ExpirationDate = request.ExpirationDate,
            Notes = Normalize.OptionalText(request.Notes),
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        db.InventoryMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetAsync(movement.Id, ct);
    }

    public async Task<PagedResult<MovementDto>> ListBySupplyAsync(int supplyId, MovementQuery q, CancellationToken ct)
    {
        if (!await db.Supplies.AnyAsync(s => s.Id == supplyId, ct))
            throw NotFoundException.For("el insumo", supplyId);

        var query = db.InventoryMovements.AsNoTracking().Where(m => m.SupplyId == supplyId);
        var total = await query.CountAsync(ct);
        var items = await Project(query.OrderByDescending(m => m.CreatedAt).Skip(q.Skip).Take(q.PageSize)).ToListAsync(ct);
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
