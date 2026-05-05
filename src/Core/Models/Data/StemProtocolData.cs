using System.Collections.Immutable;

namespace Core.Models.Data
{
    // Dati protocollo da Excel

    public record StemProtocolData
    {
        public ImmutableList<StemRowData> Addresses { get; init; } = [];
        public ImmutableList<StemCommandData> Commands { get; init; } = [];
        public ImmutableList<StemVariableData> Variables { get; init; } = [];
    }
}
