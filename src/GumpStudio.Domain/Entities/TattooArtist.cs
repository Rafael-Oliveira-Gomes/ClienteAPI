using GumpStudio.Domain.Common;
using GumpStudio.Domain.Enums;

namespace GumpStudio.Domain.Entities;

public sealed class TattooArtist : AuditableEntity
{
    private readonly List<ArtistCompensation> _compensations = [];
    private readonly List<ArtistStockAccess> _stockAccesses = [];

    private TattooArtist() { }

    public Guid UserId { get; private set; }
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string? Cpf { get; private set; }
    public string? Phone { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public string? PhotoPath { get; private set; }
    public ArtistType Type { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<ArtistCompensation> Compensations => _compensations;
    public IReadOnlyCollection<ArtistStockAccess> StockAccesses => _stockAccesses;

    public static TattooArtist Create(Guid userId, string name, string email, ArtistType type,
        DateOnly startDate, DateOnly? endDate, ArtistCompensation compensation,
        string? cpf = null, string? phone = null, DateOnly? birthDate = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Nome é obrigatório.");
        ArgumentNullException.ThrowIfNull(compensation);

        var artist = new TattooArtist
        {
            UserId = userId, Name = name.Trim(), Email = email.Trim().ToLowerInvariant(),
            Cpf = cpf, Phone = phone, BirthDate = birthDate, Type = type, StartDate = startDate
        };
        artist.SetPeriod(type, startDate, endDate);
        artist.ChangeCompensation(compensation);
        return artist;
    }

    public void UpdateProfile(string name, string? cpf, string? phone, DateOnly? birthDate)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Nome é obrigatório.");
        Name = name.Trim(); Cpf = cpf; Phone = phone; BirthDate = birthDate;
    }

    /// <summary>Rotativo exige data final (seção 3.3).</summary>
    public void SetPeriod(ArtistType type, DateOnly startDate, DateOnly? endDate)
    {
        if (type == ArtistType.Rotating && endDate is null)
            throw new DomainException("Tatuador rotativo exige data final de acesso.");
        if (endDate.HasValue && endDate < startDate)
            throw new DomainException("A data final não pode ser anterior à inicial.");
        Type = type; StartDate = startDate;
        EndDate = type == ArtistType.Rotating ? endDate : null;
    }

    public void SetPhoto(string? path) => PhotoPath = path;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    /// <summary>Troca a regra de remuneração sem alterar o cadastro (seção 5).</summary>
    public void ChangeCompensation(ArtistCompensation next)
    {
        var current = _compensations.FirstOrDefault(c => c.ValidTo is null);
        if (current is not null)
        {
            if (next.ValidFrom <= current.ValidFrom)
                throw new DomainException("A nova remuneração deve começar após a vigente.");
            current.Close(next.ValidFrom.AddDays(-1));
        }
        _compensations.Add(next);
    }

    public ArtistCompensation? CompensationOn(DateOnly date) =>
        _compensations.Where(c => c.ValidFrom <= date && (c.ValidTo is null || c.ValidTo >= date))
                      .OrderByDescending(c => c.ValidFrom).FirstOrDefault();

    public void SetStockAccess(IEnumerable<Guid> stockIds)
    {
        var wanted = stockIds.ToHashSet();
        _stockAccesses.RemoveAll(a => !wanted.Contains(a.StockId));
        foreach (var id in wanted.Where(id => _stockAccesses.All(a => a.StockId != id)))
            _stockAccesses.Add(new ArtistStockAccess(Id, id));
    }

    public bool CanUseStock(Guid stockId) => _stockAccesses.Any(a => a.StockId == stockId);
}

public sealed class ArtistCompensation : Entity
{
    private ArtistCompensation() { }

    public Guid ArtistId { get; private set; }
    public CompensationType Type { get; private set; }
    public decimal? MonthlyAmount { get; private set; }
    public decimal? DailyAmount { get; private set; }
    public decimal? ArtistPercentage { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }

    public static ArtistCompensation Create(CompensationType type, decimal? monthly, decimal? daily,
        decimal? artistPercentage, DateOnly validFrom)
    {
        var c = new ArtistCompensation { Type = type, ValidFrom = validFrom };
        switch (type)
        {
            case CompensationType.MonthlyFixed:
                if (monthly is null or <= 0) throw new DomainException("Informe o valor mensal.");
                c.MonthlyAmount = monthly; break;
            case CompensationType.Daily:
                if (daily is null or <= 0) throw new DomainException("Informe o valor diário.");
                c.DailyAmount = daily; break;
            case CompensationType.Percentage:
                if (artistPercentage is null or <= 0 or > 100)
                    throw new DomainException("O percentual do tatuador deve estar entre 0 e 100.");
                c.ArtistPercentage = artistPercentage; break;
            default: throw new DomainException("Modelo de remuneração inválido.");
        }
        return c;
    }

    internal void Close(DateOnly validTo) => ValidTo = validTo;

    /// <summary>Parte do tatuador sobre o valor da sessão (apenas modelo percentual).</summary>
    public decimal ArtistShareOf(decimal sessionValue) =>
        Type == CompensationType.Percentage
            ? Math.Round(sessionValue * ArtistPercentage!.Value / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

    public decimal StudioShareOf(decimal sessionValue) =>
        Type == CompensationType.Percentage ? sessionValue - ArtistShareOf(sessionValue) : sessionValue;
}

public sealed class ArtistStockAccess
{
    private ArtistStockAccess() { }
    public ArtistStockAccess(Guid artistId, Guid stockId) { ArtistId = artistId; StockId = stockId; }
    public Guid ArtistId { get; private set; }
    public Guid StockId { get; private set; }
}
