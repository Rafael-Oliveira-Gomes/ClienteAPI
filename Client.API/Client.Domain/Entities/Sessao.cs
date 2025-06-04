namespace Client.Domain.Entities;

public record class Sessao
{
    public int Id { get; init; }
    public Tatuador tatuador { get; init; }
    public Cliente cliente { get; init; }
    public DateTime DataHora { get; init; }
    public float Duracao { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Descricao { get; init; } = string.Empty;
    public string Local { get; init; } = string.Empty;
    public string Observacoes { get; init; } = string.Empty;
    public string Cuidados { get; init; } = string.Empty;
}
