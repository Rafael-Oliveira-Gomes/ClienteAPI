using Client.Domain.Enums;

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
    string Observacao,
    TipoTatuador TipoTatuador,
    StatusTatuador StatusTatuador
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
        tatuador.Observacao,
        tatuador.TipoTatuador,
        tatuador.StatusTatuador
    )
    { }
}
