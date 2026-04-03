namespace Core.Interfaces.Data
{
    /// <summary>
    /// Contratto per una factory che crea istanze di IProtocolRepository
    /// </summary>
    public interface IProtocolRepositoryFactory
    {
        // Crea un'istanza di IProtocolRepository per un dato recipientId
        IProtocolRepository Create(uint recipientId);
    }
}
