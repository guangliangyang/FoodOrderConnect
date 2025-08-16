using AutoMapper;
using BidOne.InternalSystemApi.Data;
using BidOne.InternalSystemApi.Data.Entities;
using BidOne.InternalSystemApi.Mappings;
using Microsoft.EntityFrameworkCore;

namespace BidOne.InternalSystemApi.Services;

public class InventoryService : IInventoryService
{
    private readonly BidOneDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(BidOneDbContext dbContext, IMapper mapper, ILogger<InventoryService> logger)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<InventoryReservationResult> ReserveInventoryAsync(IEnumerable<InventoryReservationRequest> requests, CancellationToken cancellationToken = default)
    {
        var requestList = requests.ToList();
        var result = new InventoryReservationResult { IsSuccessful = true };

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            foreach (var request in requestList)
            {
                var inventory = await _dbContext.Inventory
                    .FirstOrDefaultAsync(i => i.ProductId == request.ProductId, cancellationToken);

                if (inventory == null)
                {
                    result.IsSuccessful = false;
                    result.FailureReason = $"Product {request.ProductId} not found in inventory";
                    result.UnavailableProducts.Add(request.ProductId);
                    continue;
                }

                if (inventory.AvailableQuantity < request.Quantity)
                {
                    result.IsSuccessful = false;
                    result.FailureReason = $"Insufficient inventory for product {request.ProductId}. Available: {inventory.AvailableQuantity}, Requested: {request.Quantity}";
                    result.UnavailableProducts.Add(request.ProductId);
                    continue;
                }

                // Reserve the inventory
                inventory.QuantityReserved += request.Quantity;
                inventory.LastUpdated = DateTime.UtcNow;
                inventory.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation("Reserved {Quantity} units of product {ProductId} for order {OrderId}", 
                    request.Quantity, request.ProductId, request.OrderId);
            }

            if (result.IsSuccessful)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Successfully reserved inventory for {Count} products", requestList.Count);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning("Inventory reservation failed: {Reason}", result.FailureReason);
            }

            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error during inventory reservation");
            throw;
        }
    }

    public async Task<bool> ReleaseReservationAsync(string orderId, CancellationToken cancellationToken = default)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // Find order items to release reservations
            var orderItems = await _dbContext.OrderItems
                .Where(oi => oi.OrderId == orderId)
                .ToListAsync(cancellationToken);

            foreach (var orderItem in orderItems)
            {
                var inventory = await _dbContext.Inventory
                    .FirstOrDefaultAsync(i => i.ProductId == orderItem.ProductId, cancellationToken);

                if (inventory != null)
                {
                    inventory.QuantityReserved = Math.Max(0, inventory.QuantityReserved - orderItem.Quantity);
                    inventory.LastUpdated = DateTime.UtcNow;
                    inventory.UpdatedAt = DateTime.UtcNow;

                    _logger.LogInformation("Released {Quantity} units of product {ProductId} from order {OrderId}", 
                        orderItem.Quantity, orderItem.ProductId, orderId);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully released inventory reservations for order {OrderId}", orderId);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error releasing inventory reservations for order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<Inventory?> GetInventoryAsync(string productId, CancellationToken cancellationToken = default)
    {
        var inventoryEntity = await _dbContext.Inventory
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.ProductId == productId, cancellationToken);

        return inventoryEntity != null ? _mapper.Map<Inventory>(inventoryEntity) : null;
    }

    public async Task<IEnumerable<Inventory>> GetLowStockItemsAsync(CancellationToken cancellationToken = default)
    {
        var lowStockEntities = await _dbContext.Inventory
            .Include(i => i.Product)
            .Where(i => i.AvailableQuantity <= i.ReorderLevel)
            .ToListAsync(cancellationToken);

        return _mapper.Map<IEnumerable<Inventory>>(lowStockEntities);
    }

    public async Task<bool> UpdateInventoryAsync(string productId, int quantityChange, string reason, CancellationToken cancellationToken = default)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var inventory = await _dbContext.Inventory
                .FirstOrDefaultAsync(i => i.ProductId == productId, cancellationToken);

            if (inventory == null)
            {
                _logger.LogWarning("Product {ProductId} not found in inventory", productId);
                return false;
            }

            var newQuantity = inventory.QuantityOnHand + quantityChange;
            if (newQuantity < 0)
            {
                _logger.LogWarning("Inventory update would result in negative quantity for product {ProductId}. Current: {Current}, Change: {Change}", 
                    productId, inventory.QuantityOnHand, quantityChange);
                return false;
            }

            inventory.QuantityOnHand = newQuantity;
            inventory.LastUpdated = DateTime.UtcNow;
            inventory.UpdatedAt = DateTime.UtcNow;

            // Log the inventory change
            await _dbContext.OrderEvents.AddAsync(new OrderEventEntity
            {
                Id = Guid.NewGuid(),
                OrderId = "INVENTORY-UPDATE",
                EventType = "InventoryAdjustment",
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ProductId = productId,
                    QuantityChange = quantityChange,
                    NewQuantity = newQuantity,
                    Reason = reason
                }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            }, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Updated inventory for product {ProductId}. Change: {Change}, New Quantity: {NewQuantity}, Reason: {Reason}", 
                productId, quantityChange, newQuantity, reason);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error updating inventory for product {ProductId}", productId);
            throw;
        }
    }
}