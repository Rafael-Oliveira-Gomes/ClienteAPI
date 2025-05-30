namespace Client.Domain.DTOs;

public class TatuadorDto
{
    public string Nome { get; set; } = string.Empty;
    public string NomeArtistico { get; set; } = string.Empty;
    public DateOnly DataNascimento { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public int AnoExperiencia { get; set; }
    public string Especialidade { get; set; } = string.Empty;
    public string Portifolio { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public string Observacao { get; set; } = string.Empty;
}
