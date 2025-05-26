namespace Client.Domain.Entities;

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public string NomeSocial { get; set; }
    public string Email { get; set; }
    public string Telefone { get; set; } 
    public DateOnly DataNascimento { get; set; }
    public DateTime DataCadastro { get; set; }
    public bool Alergia { get; set; }
    public string Observacao { get; set; }
    public string Sexo { get; set; }
}
