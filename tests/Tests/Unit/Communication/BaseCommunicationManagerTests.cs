using System.Reflection;
using Communication;

namespace Tests.Unit.Communication
{
    /// <summary>
    /// Unit tests per BaseCommunicationManager.
    /// Concretizza la classe in modo minimale e usa reflection per accedere ai membri non pubblici.
    /// </summary>
    public class BaseCommunicationManagerTests
    {
        // Minima implementazione concreta di BaseCommunicationManager
        private class TestManager : BaseCommunicationManager
        {
            public override int MaxPacketSize => 8;
            public override Task<bool> ConnectAsync(string config, CancellationToken cancellationToken = default) => Task.FromResult(true);
            public override Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public override Task<bool> SendAsync(byte[] data, uint? arbitrationId = null) => Task.FromResult(true);
            public override ValueTask DisposeAsync() => new();
        }

        private readonly TestManager _manager = new();

        // Il metodo deve aggiornare IsConnected e sollevare l'evento
        [Fact]
        public void RaiseConnectionChanged_UpdatesIsConnectedAndRaisesEvent()
        {
            // Arrange
            bool eventRaised = false;
            _manager.ConnectionStatusChanged += (_, connected) => eventRaised = connected;

            // Usa reflection per accedere al metodo protected
            MethodInfo? method = typeof(BaseCommunicationManager).GetMethod("RaiseConnectionChanged",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            Assert.NotNull(method);

            // Act
            method.Invoke(_manager, [true]);

            // Assert
            Assert.True(_manager.IsConnected);
            Assert.True(eventRaised);
        }

        // Il metodo deve sollevare l'evento con i dati corretti
        [Fact]
        public void RaisePacketReceived_RaisesEvent()
        {
            // Arrange
            byte[]? received = null;
            _manager.PacketReceived += (_, data) => received = data;

            // Usa reflection per accedere al metodo protected
            MethodInfo? method = typeof(BaseCommunicationManager).GetMethod("RaisePacketReceived",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            Assert.NotNull(method);

            byte[] testData = [1, 2];

            // Act
            method.Invoke(_manager, [testData]);

            // Assert
            Assert.Equal(testData, received);
        }
    }
}
