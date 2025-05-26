namespace Client.Domain.Entities;
public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string NomeSocial { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public DateOnly DataNascimento { get; set; }
    public DateTime DataCadastro { get; set; }
    public bool Alergia { get; set; }
    public string Observacao { get; set; } = string.Empty;
    public string Sexo { get; set; } = string.Empty;

    public Cliente() { }

    public Cliente(string nome, string nomeSocial, string email, string telefone, DateOnly dataNascimento, DateTime dataCadastro, bool alergia, string observacao, string sexo)
    {
        Nome = nome;
        NomeSocial = nomeSocial;
        Email = email;
        Telefone = telefone;
        DataNascimento = dataNascimento;
        DataCadastro = dataCadastro;
        Alergia = alergia;
        Observacao = observacao;
        Sexo = sexo;
    }
}