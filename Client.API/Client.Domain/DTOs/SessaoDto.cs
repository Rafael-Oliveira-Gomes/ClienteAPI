namespace Client.Domain.DTOs;

public class SessaoDto
{
    public int ClienteId { get; init; }
    public DateTime DataAgendamento { get; init; }
    public string DescricaoTatuagem { get; init; } = string.Empty;
    public float Duracao { get; init; }
    public string LocalizacaoCorpo { get; init; } = string.Empty;
    public string Observacoes { get; init; } = string.Empty;
    public decimal Preco { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Cuidados { get; init; } = string.Empty;
    public int TatuadorId { get; init; }
}
