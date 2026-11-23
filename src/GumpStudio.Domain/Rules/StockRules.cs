using GumpStudio.Domain.Common;

namespace GumpStudio.Domain.Rules;

public static class StockRules
{
    /// <summary>RN01: uma saída nunca pode deixar o estoque negativo.</summary>
    public static void EnsureSufficientBalance(decimal currentBalance, decimal quantityToRemove)
    {
        if (quantityToRemove > currentBalance)
            throw new DomainException(
                $"Saldo insuficiente no estoque: disponível {currentBalance:0.###}, solicitado {quantityToRemove:0.###}.");
    }

    /// <summary>Seção 13: alerta quando o saldo está abaixo do mínimo configurado.</summary>
    public static bool IsBelowMinimum(decimal balance, decimal minimumStock) =>
        minimumStock > 0 && balance < minimumStock;
}
