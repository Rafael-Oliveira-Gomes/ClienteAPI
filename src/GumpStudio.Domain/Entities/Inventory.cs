using GumpStudio.Domain.Common;
using GumpStudio.Domain.Enums;

namespace GumpStudio.Domain.Entities;

public sealed class Product : AuditableEntity
{
    private Product() { }

    public string Name { get; private set; } = "";
    public UnitOfMeasure Unit { get; private set; }
    public string? ImagePath { get; private set; }
    public string? Code { get; private set; }
    public decimal PurchasePrice { get; private set; }
    public decimal? SalePrice { get; private set; }
    public decimal MinimumStock { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Product Create(string name, UnitOfMeasure unit, string? code, decimal purchasePrice,
        decimal? salePrice, decimal minimumStock)
    {
        var p = new Product();
        p.Update(name, unit, code, purchasePrice, salePrice, minimumStock);
        return p;
    }

    public void Update(string name, UnitOfMeasure unit, string? code, decimal purchasePrice,
        decimal? salePrice, decimal minimumStock)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Nome é obrigatório.");
        if (!Enum.IsDefined(unit)) throw new DomainException("Unidade de medida inválida.");
        if (purchasePrice < 0 || salePrice < 0) throw new DomainException("Preços não podem ser negativos.");
        if (minimumStock < 0) throw new DomainException("Estoque mínimo não pode ser negativo.");
        Name = name.Trim(); Unit = unit; Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        PurchasePrice = purchasePrice; SalePrice = salePrice; MinimumStock = minimumStock;
    }

    public void SetImage(string? path) => ImagePath = path;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}

public sealed class Stock : AuditableEntity
{
    private Stock() { }

    public string Name { get; private set; } = "";
    public bool IsMain { get; private set; }
    public Guid? OwnerArtistId { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Token de concorrência: toda movimentação "toca" o estoque, então duas
    /// movimentações simultâneas não conseguem ambas validar o mesmo saldo (RN01).
    /// </summary>
    public Guid RowVersion { get; private set; } = Guid.NewGuid();

    public static Stock Create(string name, bool isMain, Guid? ownerArtistId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Nome do estoque é obrigatório.");
        return new Stock { Name = name.Trim(), IsMain = isMain, OwnerArtistId = ownerArtistId };
    }

    public void Update(string name, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Nome do estoque é obrigatório.");
        Name = name.Trim(); IsActive = isActive;
    }

    public void Touch() => RowVersion = Guid.NewGuid();
}

/// <summary>
/// Movimentação imutável (RN08/RN09, seções 32-33). Erros são corrigidos com nova
/// movimentação de correção; nunca com UPDATE/DELETE.
/// </summary>
public sealed class StockMovement : AuditableEntity
{
    private StockMovement() { }

    public Guid ProductId { get; private set; }
    public Guid StockId { get; private set; }
    public StockMovementType Type { get; private set; }
    /// <summary>Sempre positiva; o sentido vem do <see cref="Type"/>.</summary>
    public decimal Quantity { get; private set; }
    /// <summary>Quantidade com sinal (+ entrada / − saída). Persistida para somar saldo no banco.</summary>
    public decimal QuantityDelta { get; private set; }
    public decimal? UnitCost { get; private set; }
    public decimal? UnitSalePrice { get; private set; }
    public Guid? SessionId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public Guid? ReversesMovementId { get; private set; }
    public string? Reason { get; private set; }
    public string? Notes { get; private set; }
    public Guid UserId { get; private set; }
    public string? IpAddress { get; private set; }
    public string Origin { get; private set; } = "api";

    private static StockMovement Build(StockMovementType type, Guid productId, Guid stockId, decimal quantity,
        Guid userId, string? ip)
    {
        if (quantity <= 0) throw new DomainException("A quantidade deve ser maior que zero.");
        if (userId == Guid.Empty) throw new DomainException("Toda movimentação deve registrar o usuário responsável.");
        var sign = type is StockMovementType.Entry or StockMovementType.CorrectionEntry ? 1 : -1;
        return new StockMovement
        {
            Type = type, ProductId = productId, StockId = stockId, Quantity = quantity,
            QuantityDelta = sign * quantity, UserId = userId, IpAddress = ip
        };
    }

    public static StockMovement Entry(Guid productId, Guid stockId, decimal quantity, decimal unitCost,
        decimal? unitSalePrice, Guid? invoiceId, string? notes, Guid userId, string? ip)
    {
        if (unitCost < 0) throw new DomainException("O valor pago não pode ser negativo.");
        var m = Build(StockMovementType.Entry, productId, stockId, quantity, userId, ip);
        m.UnitCost = unitCost; m.UnitSalePrice = unitSalePrice; m.InvoiceId = invoiceId; m.Notes = notes;
        return m;
    }

    /// <summary>RN02, RN03, RN04, RN05.</summary>
    public static StockMovement Usage(Guid productId, Guid stockId, Guid? sessionId, decimal quantity,
        decimal? unitCost, decimal? unitSalePrice, string? notes, Guid userId, string? ip)
    {
        if (sessionId is null || sessionId == Guid.Empty)
            throw new DomainException("O uso de produto deve estar vinculado a uma sessão.");
        if (unitCost is null || unitCost < 0)
            throw new DomainException("O valor pago por unidade é obrigatório no uso.");
        var m = Build(StockMovementType.Usage, productId, stockId, quantity, userId, ip);
        m.SessionId = sessionId; m.UnitCost = unitCost; m.UnitSalePrice = unitSalePrice; m.Notes = notes;
        return m;
    }

    public static StockMovement Damaged(Guid productId, Guid stockId, decimal quantity, string reason,
        string? notes, Guid userId, string? ip)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Informe o motivo.");
        var m = Build(StockMovementType.Damaged, productId, stockId, quantity, userId, ip);
        m.Reason = reason.Trim(); m.Notes = notes;
        return m;
    }

    /// <summary>RN06: no MVP o ajuste de inventário só reduz estoque (a quantidade é sempre uma redução).</summary>
    public static StockMovement InventoryAdjustment(Guid productId, Guid stockId, decimal quantityToReduce,
        string reason, string? notes, Guid userId, string? ip)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Informe o motivo.");
        var m = Build(StockMovementType.InventoryAdjustment, productId, stockId, quantityToReduce, userId, ip);
        m.Reason = reason.Trim(); m.Notes = notes;
        return m;
    }

    /// <summary>Movimentação de correção que desfaz uma movimentação anterior (RN09).</summary>
    public static StockMovement CreateCorrection(StockMovement original, string reason, Guid userId, string? ip)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Informe o motivo da correção.");
        if (original.ReversesMovementId is not null)
            throw new DomainException("Não é possível corrigir uma movimentação de correção.");
        var type = original.QuantityDelta > 0 ? StockMovementType.CorrectionExit : StockMovementType.CorrectionEntry;
        var m = Build(type, original.ProductId, original.StockId, original.Quantity, userId, ip);
        m.ReversesMovementId = original.Id; m.UnitCost = original.UnitCost; m.UnitSalePrice = original.UnitSalePrice;
        m.SessionId = original.SessionId; m.InvoiceId = original.InvoiceId; m.Reason = reason.Trim();
        return m;
    }
}

public sealed class Invoice : AuditableEntity
{
    private Invoice() { }

    public string Number { get; private set; } = "";
    public string? Series { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public string Supplier { get; private set; } = "";
    public decimal TotalValue { get; private set; }
    public string? FileStorageKey { get; private set; }
    public string? FileName { get; private set; }
    public string? FileContentType { get; private set; }

    public static Invoice Create(string number, string? series, DateOnly issueDate, string supplier, decimal totalValue)
    {
        if (string.IsNullOrWhiteSpace(number)) throw new DomainException("Número da nota é obrigatório.");
        if (string.IsNullOrWhiteSpace(supplier)) throw new DomainException("Fornecedor é obrigatório.");
        if (totalValue < 0) throw new DomainException("Valor total inválido.");
        return new Invoice { Number = number.Trim(), Series = series, IssueDate = issueDate, Supplier = supplier.Trim(), TotalValue = totalValue };
    }

    public void AttachFile(string storageKey, string fileName, string contentType)
    {
        FileStorageKey = storageKey; FileName = fileName; FileContentType = contentType;
    }
}

/// <summary>Lançamento imutável. Estorno é um novo lançamento com valor negativo.</summary>
public sealed class FinancialTransaction : AuditableEntity
{
    private FinancialTransaction() { }

    public TransactionType Type { get; private set; }
    public string Description { get; private set; } = "";
    public decimal Amount { get; private set; }
    public DateOnly Date { get; private set; }
    public string Category { get; private set; } = "";
    public Guid? SessionId { get; private set; }
    public Guid? ArtistId { get; private set; }
    public Guid? ClientId { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ReversesTransactionId { get; private set; }

    public static FinancialTransaction Create(TransactionType type, string description, decimal amount, DateOnly date,
        string category, Guid? sessionId, Guid? artistId, Guid? clientId, string? notes)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Descrição é obrigatória.");
        if (string.IsNullOrWhiteSpace(category)) throw new DomainException("Categoria é obrigatória.");
        if (amount <= 0) throw new DomainException("O valor do lançamento deve ser maior que zero.");
        return new FinancialTransaction
        {
            Type = type, Description = description.Trim(), Amount = amount, Date = date, Category = category.Trim(),
            SessionId = sessionId, ArtistId = artistId, ClientId = clientId, Notes = notes
        };
    }

    public static FinancialTransaction Reverse(FinancialTransaction original, DateOnly date, string? notes)
    {
        if (original.ReversesTransactionId is not null)
            throw new DomainException("Não é possível estornar um estorno.");
        return new FinancialTransaction
        {
            Type = original.Type, Description = $"Estorno: {original.Description}", Amount = -original.Amount,
            Date = date, Category = original.Category, SessionId = original.SessionId, ArtistId = original.ArtistId,
            ClientId = original.ClientId, Notes = notes, ReversesTransactionId = original.Id
        };
    }
}

/// <summary>Conexão (única) do estúdio com o Google Agenda. O refresh token fica protegido (Data Protection).</summary>
public sealed class StudioGoogleConnection : Entity
{
    private StudioGoogleConnection() { }

    public string GoogleAccountEmail { get; private set; } = "";
    public string ProtectedRefreshToken { get; private set; } = "";
    public string CalendarId { get; private set; } = "primary";
    public DateTimeOffset ConnectedAt { get; private set; }
    public Guid ConnectedBy { get; private set; }

    public static StudioGoogleConnection Create(string email, string protectedRefreshToken, string calendarId,
        DateTimeOffset now, Guid connectedBy) =>
        new() { GoogleAccountEmail = email, ProtectedRefreshToken = protectedRefreshToken,
                CalendarId = calendarId, ConnectedAt = now, ConnectedBy = connectedBy };

    public void Reconnect(string email, string protectedRefreshToken, DateTimeOffset now, Guid connectedBy)
    {
        GoogleAccountEmail = email; ProtectedRefreshToken = protectedRefreshToken;
        ConnectedAt = now; ConnectedBy = connectedBy;
    }
}
