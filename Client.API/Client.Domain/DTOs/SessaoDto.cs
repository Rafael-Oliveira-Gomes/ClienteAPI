namespace Client.Domain.DTOs;

public class SessaoDto
{
    public int ClienteId { get; set; }
    public int TatuadorId { get; set; }
    public DateTime DataAgendamento { get; set; }
    public float Duracao { get; set; }
    public string DescricaoTatuagem { get; set; } = string.Empty;
    public string LocalizacaoCorpo { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Cuidados { get; set; } = string.Empty;
    public string Observacoes { get; set; } = string.Empty;
}