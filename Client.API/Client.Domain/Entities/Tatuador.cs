namespace Client.Domain.Entities;
public class Tatuador
{
    public int Id { get; set; }
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

    public Tatuador() { }

    public Tatuador(string nome, string nomeArtistico, DateOnly dataNascimento, string email, string telefone, int anoExperiencia, string especialidade, string portifolio, bool ativo, string observacao)
    {
        Nome = nome;
        NomeArtistico = nomeArtistico;
        DataNascimento = dataNascimento;
        Email = email;
        Telefone = telefone;
        AnoExperiencia = anoExperiencia;
        Especialidade = especialidade;
        Portifolio = portifolio;
        Ativo = ativo;
        Observacao = observacao;
    }
}
