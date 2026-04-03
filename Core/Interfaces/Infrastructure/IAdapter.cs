namespace Core.Interfaces.Infrastructure
{
    /// <summary>
    /// Contratto base per gli adattatori hardware.
    /// Definisce le operazioni comuni condivise dai canali (CAN, BLE, Seriale).
    /// </summary>
    public interface IAdapter : IAsyncDisposable
    {
        bool IsConnected { get; }

        event EventHandler<bool> ConnectionStatusChanged;

        Task<bool> ConnectAsync(string config, CancellationToken cancellationToken = default);
        Task DisconnectAsync(CancellationToken cancellationToken = default);
    }
}
