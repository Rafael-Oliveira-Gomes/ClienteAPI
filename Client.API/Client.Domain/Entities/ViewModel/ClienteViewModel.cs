namespace Client.Domain.Entities.ViewModel;

public record class ClienteViewModel(
    int Id,
    string Nome,
    string NomeSocial,
    string Email,
    string Telefone,
    DateOnly DataNascimento,
    DateTime DataCadastro,
    bool Alergia,
    string Observacao,
    string Sexo
)
{
    public ClienteViewModel(Cliente cliente) : this(
        cliente.Id,
        cliente.Nome,
        cliente.NomeSocial,
        cliente.Email,
        cliente.Telefone,
        cliente.DataNascimento,
        cliente.DataCadastro,
        cliente.Alergia,
        cliente.Observacao,
        cliente.Sexo
    )
    { }
}
