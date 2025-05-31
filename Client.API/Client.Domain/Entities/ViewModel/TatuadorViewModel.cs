namespace Client.Domain.Entities.ViewModel;

public record class TatuadorViewModel(
    int Id,
    string Nome,
    string NomeArtistico,
    DateOnly DataNascimento,
    string Email,
    string Telefone,
    int AnoExperiencia,
    string Especialidade,
    string Portifolio,
    bool Ativo,
    string Observacao
)
{
    public TatuadorViewModel(Tatuador tatuador) : this(
        tatuador.Id,
        tatuador.Nome,
        tatuador.NomeArtistico,
        tatuador.DataNascimento,
        tatuador.Email,
        tatuador.Telefone,
        tatuador.AnoExperiencia,
        tatuador.Especialidade,
        tatuador.Portifolio,
        tatuador.Ativo,
        tatuador.Observacao
    )
    { }
}
