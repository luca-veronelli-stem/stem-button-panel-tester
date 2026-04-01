using Core.Enums;
using Core.Interfaces.Communication;
using Core.Models;
using Core.Models.Communication;
using Core.Results;
using Moq;
using Services;

namespace Tests.Unit.Services
{
    /// <summary>
    /// Test unitari per la classe CommunicationService.
    /// </summary>
    public class CommunicationServiceTests
    {
        private readonly Mock<IProtocolManager> _protocolManagerMock;
        private readonly Mock<ICommunicationManagerFactory> _managerFactoryMock;

        // SUT (Subject Under Test)
        private readonly CommunicationService _service;

        public CommunicationServiceTests()
        {
            _protocolManagerMock = new Mock<IProtocolManager>();
            _managerFactoryMock = new Mock<ICommunicationManagerFactory>();
            _service = new CommunicationService(_protocolManagerMock.Object, _managerFactoryMock.Object);
        }

        // Verica che SetActiveChannelAsync non faccia nulla se il canale attivo è lo stesso e già connesso.
        [Fact]
        public async Task SetActiveChannelAsync_SameChannelAndConnected_DoesNothing()
        {
            // Arrange
            var channel = CommunicationChannel.Can;
            var config = "test-config";
            var managerMock = new Mock<ICommunicationManager>();
            managerMock.Setup(m => m.IsConnected).Returns(true);
            managerMock.Setup(m => m.ConnectAsync(config, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _managerFactoryMock.Setup(f => f.Create(channel)).Returns(managerMock.Object);

            // Prima chiamata per impostare il canale attivo
            await _service.SetActiveChannelAsync(channel, config);

            // Act: Seconda chiamata con lo stesso canale
            var result = await _service.SetActiveChannelAsync(channel, config);

            // Assert: Verifica che non siano state effettuate ulteriori connessioni o disconnessioni
            Assert.True(result.IsSuccess);
            _managerFactoryMock.Verify(f => f.Create(channel), Times.Once);
            managerMock.Verify(m => m.ConnectAsync(config, It.IsAny<CancellationToken>()), Times.Once);
            managerMock.Verify(m => m.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // Verifica che SetActiveChannelAsync disconnetta il vecchio canale e connetta il nuovo quando cambia canale.
        [Fact]
        public async Task SetActiveChannelAsync_NewChannel_DisconnectsOldAndConnectsNew()
        {
            // Arrange: Configura due canali diversi per simulare un cambio.
            var oldChannel = CommunicationChannel.Ble;
            var newChannel = CommunicationChannel.Can;
            var config = "test-config";

            // Mock per il vecchio manager: già connesso, si connette con successo, si disconnette correttamente.
            var oldManagerMock = new Mock<ICommunicationManager>();
            oldManagerMock.Setup(m => m.IsConnected).Returns(true);
            oldManagerMock.Setup(m => m.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            oldManagerMock.Setup(m => m.DisconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Mock per il nuovo manager: si connette con successo.
            var newManagerMock = new Mock<ICommunicationManager>();
            newManagerMock.Setup(m => m.IsConnected).Returns(true);
            newManagerMock.Setup(m => m.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Setup della factory per restituire i mock corretti per ciascun canale.
            _managerFactoryMock.Setup(f => f.Create(oldChannel)).Returns(oldManagerMock.Object);
            _managerFactoryMock.Setup(f => f.Create(newChannel)).Returns(newManagerMock.Object);

            // Act: Imposta prima il vecchio canale, poi il nuovo.
            await _service.SetActiveChannelAsync(oldChannel, config);
            var result = await _service.SetActiveChannelAsync(newChannel, config);

            // Assert: Verifica che il vecchio sia stato disconnesso e il nuovo connesso.
            Assert.True(result.IsSuccess);
            oldManagerMock.Verify(m => m.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
            newManagerMock.Verify(m => m.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _managerFactoryMock.Verify(f => f.Create(It.IsAny<CommunicationChannel>()), Times.Exactly(2));
        }

        // Verifica che SetActiveChannelAsync restituisca un errore se la connessione fallisce.
        [Fact]
        public async Task SetActiveChannelAsync_ConnectionFails_ReturnsFailure()
        {
            // Arrange
            var channel = CommunicationChannel.Can;
            var config = "test-config";
            var managerMock = new Mock<ICommunicationManager>();
            managerMock.Setup(m => m.ConnectAsync(config, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            _managerFactoryMock.Setup(f => f.Create(channel)).Returns(managerMock.Object);

            // Act
            var result = await _service.SetActiveChannelAsync(channel, config);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCodes.ConnectionFailed, result.Error.Code);
            managerMock.Verify(m => m.ConnectAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        }

        // Verifica che SetActiveChannelAsync rispetti il CancellationToken.
        [Fact]
        public async Task SetActiveChannelAsync_WithCancellationToken_ReturnsCancelledResult()
        {
            // Arrange
            var channel = CommunicationChannel.Can;
            var config = "test-config";
            var managerMock = new Mock<ICommunicationManager>();

            // Simula una connessione che richiede tempo
            managerMock.Setup(m => m.ConnectAsync(config, It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((c, ct) => Task.Delay(10000, ct).ContinueWith(_ => true, ct));

            _managerFactoryMock.Setup(f => f.Create(channel)).Returns(managerMock.Object);

            var cts = new CancellationTokenSource();

            // Act
            var setTask = _service.SetActiveChannelAsync(channel, config, cts.Token);
            await Task.Delay(100);
            cts.Cancel();

            // Assert
            var result = await setTask;
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCodes.Cancelled, result.Error.Code);
        }

        // Verifica che SetActiveChannelAsync gestisca correttamente una configurazione nulla.
        [Fact]
        public async Task SetActiveChannelAsync_NullConfig_HandlesGracefully()
        {
            // Arrange
            var channel = CommunicationChannel.Can;
            string? config = null;
            var managerMock = new Mock<ICommunicationManager>();
            managerMock.Setup(m => m.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _managerFactoryMock.Setup(f => f.Create(channel)).Returns(managerMock.Object);

            // Act
            var result = await _service.SetActiveChannelAsync(channel, config!);

            // Assert
            Assert.True(result.IsSuccess);
            managerMock.Verify(m => m.ConnectAsync(null!, It.IsAny<CancellationToken>()), Times.Once);
        }

        // Verifica che SendCommandAsync restituisca un errore se non c'è canale attivo.
        [Fact]
        public async Task SendCommandAsync_NoActiveChannel_ReturnsFailure()
        {
            // Arrange
            ushort command = 0x0001;
            byte[] payload = new byte[] { 1, 2 };

            // Act
            var result = await _service.SendCommandAsync(command, payload, false);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCodes.NoActiveChannel, result.Error.Code);
        }

        // Verifica che SendCommandAsync invii i pacchetti correttamente senza attendere risposta.
        [Fact]
        public async Task SendCommandAsync_WithoutWaitAnswer_SendsPacketsSuccessfully()
        {
            // Arrange
            await SetupActiveChannelAsync();

            var managerMock = GetCurrentManagerMock();
            managerMock.Setup(m => m.MaxPacketSize).Returns(8);

            ushort command = 0x0001;
            byte[] payload = new byte[] { 1, 2 };
            var packets = new List<NetworkPacketChunk> { new([0, 0], 0, [1, 2]) };
            _protocolManagerMock.Setup(p => p.BuildPackets(command, payload, 0, 0, 6)).Returns(packets);

            managerMock.Setup(m => m.SendAsync(It.IsAny<byte[]>(), It.IsAny<uint?>())).ReturnsAsync(true);

            // Act
            var result = await _service.SendCommandAsync(command, payload, false);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value);
            _protocolManagerMock.Verify(p => p.BuildPackets(command, payload, 0, 0, 6), Times.Once);
            managerMock.Verify(m => m.SendAsync(It.IsAny<byte[]>(), It.IsAny<uint?>()), Times.Once);
        }

        // Verifica che SendCommandAsync riceva e validi correttamente la risposta quando attende.
        [Fact]
        public async Task SendCommandAsync_WithWaitAnswer_ReceivesAndValidatesResponse()
        {
            // Arrange
            await SetupActiveChannelAsync();

            var managerMock = GetCurrentManagerMock();
            managerMock.Setup(m => m.MaxPacketSize).Returns(8);

            ushort command = 0x0001;
            byte[] payload = new byte[] { 1, 2 };
            byte[] responsePayload = new byte[] { 3, 4, 5, 6 };
            var packets = new List<NetworkPacketChunk> { new([0, 0], 0, [1, 2]) };
            _protocolManagerMock.Setup(p => p.BuildPackets(command, payload, 0, 0, 6)).Returns(packets);

            managerMock.Setup(m => m.SendAsync(It.IsAny<byte[]>(), It.IsAny<uint?>())).ReturnsAsync(true);

            static bool validator(byte[] p) => p.SequenceEqual(new byte[] { 3, 4, 5, 6 });

            // Simula l'iscrizione all'evento
            _protocolManagerMock.SetupAdd(e => e.CommandDecoded += It.IsAny<EventHandler<AppLayerDecoderEventArgs>>());

            // Act
            var sendTask = _service.SendCommandAsync(command, payload, true, validator, 1000);

            // Simula la ricezione della risposta
            _protocolManagerMock.Raise(e => e.CommandDecoded += null, new AppLayerDecoderEventArgs(responsePayload));

            var result = await sendTask;

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(new byte[] { 5, 6 }, result.Value);
            _protocolManagerMock.VerifyRemove(e => e.CommandDecoded -= It.IsAny<EventHandler<AppLayerDecoderEventArgs>>(), Times.Once);
        }

        // Verifica che SendCommandAsync restituisca errore se l'invio fallisce.
        [Fact]
        public async Task SendCommandAsync_SendFails_ReturnsFailure()
        {
            // Arrange
            await SetupActiveChannelAsync();

            var managerMock = GetCurrentManagerMock();
            managerMock.Setup(m => m.MaxPacketSize).Returns(8);

            ushort command = 0x0001;
            byte[] payload = new byte[] { 1, 2 };
            var packets = new List<NetworkPacketChunk> { new([0, 0], 0, [1, 2]), new([0, 0], 0, [3, 4]) };
            _protocolManagerMock.Setup(p => p.BuildPackets(command, payload, 0, 0, 6)).Returns(packets);

            managerMock.SetupSequence(m => m.SendAsync(It.IsAny<byte[]>(), It.IsAny<uint?>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            // Act
            var result = await _service.SendCommandAsync(command, payload, false);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCodes.SendFailed, result.Error.Code);
        }

        // Verifica che SendCommandAsync restituisca timeout se la risposta non arriva in tempo.
        [Fact]
        public async Task SendCommandAsync_ResponseTimeout_ReturnsTimeoutError()
        {
            // Arrange
            await SetupActiveChannelAsync();

            var managerMock = GetCurrentManagerMock();
            managerMock.Setup(m => m.MaxPacketSize).Returns(8);

            ushort command = 0x0001;
            byte[] payload = new byte[] { 1, 2 };
            var packets = new List<NetworkPacketChunk> { new([0, 0], 0, [1, 2]) };
            _protocolManagerMock.Setup(p => p.BuildPackets(command, payload, 0, 0, 6)).Returns(packets);

            managerMock.Setup(m => m.SendAsync(It.IsAny<byte[]>(), It.IsAny<uint?>())).ReturnsAsync(true);

            // Act
            var result = await _service.SendCommandAsync(command, payload, true, null, 100);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCodes.Timeout, result.Error.Code);
        }

        // Verifica che SendCommandAsync ignori risposte non valide e finisca per timeout.
        [Fact]
        public async Task SendCommandAsync_ResponseValidatorFails_IgnoresInvalidResponseAndTimesOut()
        {
            // Arrange
            await SetupActiveChannelAsync();

            var managerMock = GetCurrentManagerMock();
            managerMock.Setup(m => m.MaxPacketSize).Returns(8);

            ushort command = 0x0001;
            byte[] payload = new byte[] { 1, 2 };
            byte[] invalidResponse = new byte[] { 9, 9, 9, 9 };
            var packets = new List<NetworkPacketChunk> { new([0, 0], 0, [1, 2]) };
            _protocolManagerMock.Setup(p => p.BuildPackets(command, payload, 0, 0, 6)).Returns(packets);

            managerMock.Setup(m => m.SendAsync(It.IsAny<byte[]>(), It.IsAny<uint?>())).ReturnsAsync(true);

            static bool validator(byte[] p) => false;

            // Act
            var sendTask = _service.SendCommandAsync(command, payload, true, validator, 100);

            // Simula la ricezione di una risposta non valida
            _protocolManagerMock.Raise(e => e.CommandDecoded += null, new AppLayerDecoderEventArgs(invalidResponse));

            var result = await sendTask;

            // Assert: Timeout poiché la risposta valida non è mai arrivata
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCodes.Timeout, result.Error.Code);
        }

        // Verifica che SendCommandAsync rispetti il CancellationToken durante l'attesa della risposta.
        [Fact]
        public async Task SendCommandAsync_WithCancellationToken_ReturnsCancelledResult()
        {
            // Arrange
            await SetupActiveChannelAsync();

            var managerMock = GetCurrentManagerMock();
            managerMock.Setup(m => m.MaxPacketSize).Returns(8);

            ushort command = 0x0001;
            byte[] payload = new byte[] { 1, 2 };
            var packets = new List<NetworkPacketChunk> { new([0, 0], 0, [1, 2]) };
            _protocolManagerMock.Setup(p => p.BuildPackets(command, payload, 0, 0, 6)).Returns(packets);

            managerMock.Setup(m => m.SendAsync(It.IsAny<byte[]>(), It.IsAny<uint?>())).ReturnsAsync(true);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(50);

            // Act
            var result = await _service.SendCommandAsync(command, payload, true, null, 1000, cts.Token);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCodes.Cancelled, result.Error.Code);
        }

        // Verifica che SendCommandAsync gestisca correttamente un payload vuoto.
        [Fact]
        public async Task SendCommandAsync_EmptyPayload_HandlesCorrectly()
        {
            // Arrange
            await SetupActiveChannelAsync();

            var managerMock = GetCurrentManagerMock();
            managerMock.Setup(m => m.MaxPacketSize).Returns(8);

            ushort command = 0x0001;
            byte[] payload = Array.Empty<byte>();
            var packets = new List<NetworkPacketChunk> { new([0, 0], 0, Array.Empty<byte>()) };
            _protocolManagerMock.Setup(p => p.BuildPackets(command, payload, 0, 0, 6)).Returns(packets);

            managerMock.Setup(m => m.SendAsync(It.IsAny<byte[]>(), It.IsAny<uint?>())).ReturnsAsync(true);

            // Act
            var result = await _service.SendCommandAsync(command, payload, false);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value);
            _protocolManagerMock.Verify(p => p.BuildPackets(command, payload, 0, 0, 6), Times.Once);
        }

        // Verifica che SendCommandAsync restituisca errore per eccezioni del protocollo.
        [Fact]
        public async Task SendCommandAsync_ExceptionInProtocol_ReturnsFailure()
        {
            // Arrange
            await SetupActiveChannelAsync();

            var managerMock = GetCurrentManagerMock();
            managerMock.Setup(m => m.MaxPacketSize).Returns(8);

            ushort command = 0x0001;
            byte[] payload = new byte[] { 1, 2 };
            _protocolManagerMock.Setup(p => p.BuildPackets(command, payload, 0, 0, 6))
                .Throws(new Exception("Protocol error"));

            bool eventRaised = false;
            _service.ErrorOccurred += (_, _) => eventRaised = true;

            // Act
            var result = await _service.SendCommandAsync(command, payload, false);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains("Protocol error", result.Error.Message);
            Assert.True(eventRaised);
        }

        // Verifica che OnPacketReceived processi i dati tramite il ProtocolManager.
        [Fact]
        public async Task OnPacketReceived_ProcessesViaProtocolManager()
        {
            // Arrange
            await SetupActiveChannelAsync();

            var managerMock = GetCurrentManagerMock();
            byte[] data = new byte[] { 1, 2, 3 };
            _protocolManagerMock.Setup(p => p.ProcessReceivedPacket(data)).Returns(Array.Empty<byte>());

            // Act: Sollevare l'evento con la firma corretta
            managerMock.Raise(m => m.PacketReceived += null, (object)managerMock.Object, data);

            // Assert
            _protocolManagerMock.Verify(p => p.ProcessReceivedPacket(data), Times.Once);
        }

        // Verifica che OnProtocolError sollevi l'evento ErrorOccurred con il messaggio corretto.
        [Fact]
        public void OnProtocolError_RaisesErrorOccurred()
        {
            // Arrange
            bool eventRaised = false;
            _service.ErrorOccurred += (_, args) =>
            {
                eventRaised = true;
                Assert.Contains("Test error", args.Message);
            };

            // Act
            _protocolManagerMock.Raise(p => p.ErrorOccurred += null, new ProtocolErrorEventArgs("Test error"));

            // Assert
            Assert.True(eventRaised);
        }

        // Verifica che OnConnectionStatusChanged non faccia nulla (nessun effetto collaterale verificabile).
        [Fact]
        public async Task OnConnectionStatusChanged_DoesNothing()
        {
            // Arrange
            await SetupActiveChannelAsync();

            var managerMock = GetCurrentManagerMock();

            // Act: Sollevare l'evento con la firma corretta
            managerMock.Raise(m => m.ConnectionStatusChanged += null, (object)managerMock.Object, true);

            // Assert: Nessun effetto collaterale da verificare
        }

        // Verifica che il costruttore inizializzi correttamente il servizio.
        [Fact]
        public void CommunicationService_Constructor_InitializesProperly()
        {
            // Act & Assert: Nessun'eccezione durante la creazione
            Assert.NotNull(_service);
        }

        // Verifica che SendCommandAsync sia thread-safe durante invii concorrenti.
        [Fact]
        public async Task CommunicationService_MultipleSends_ConcurrentSafe()
        {
            // Arrange
            await SetupActiveChannelAsync();

            var managerMock = GetCurrentManagerMock();
            managerMock.Setup(m => m.MaxPacketSize).Returns(8);
            managerMock.Setup(m => m.SendAsync(It.IsAny<byte[]>(), It.IsAny<uint?>())).ReturnsAsync(true);

            _protocolManagerMock.Setup(p => p.BuildPackets(It.IsAny<ushort>(), It.IsAny<byte[]>(), 0, 0, 6))
                .Returns(new List<NetworkPacketChunk> { new([0, 0], 0, [1]) });

            // Act: Eseguire 10 invii concorrenti
            var tasks = Enumerable.Range(0, 10)
                .Select(_ => _service.SendCommandAsync(0x0001, new byte[] { 1 }, false))
                .ToList();

            var results = await Task.WhenAll(tasks);

            // Assert: Tutti gli invii sono stati completati con successo
            Assert.All(results, r => Assert.True(r.IsSuccess));
            managerMock.Verify(m => m.SendAsync(It.IsAny<byte[]>(), It.IsAny<uint?>()), Times.Exactly(10));
        }

        //Helper per setup canale attivo
        private async Task SetupActiveChannelAsync(CommunicationChannel channel = CommunicationChannel.Can, string config = "test-config")
        {
            var managerMock = new Mock<ICommunicationManager>();
            managerMock.Setup(m => m.IsConnected).Returns(true);
            managerMock.Setup(m => m.ConnectAsync(config, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _managerFactoryMock.Setup(f => f.Create(channel)).Returns(managerMock.Object);

            await _service.SetActiveChannelAsync(channel, config);
        }

        // Helper per ottenere il mock del manager corrente
        private Mock<ICommunicationManager> GetCurrentManagerMock()
        {
            var field = typeof(CommunicationService).GetField("_currentManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var manager = (ICommunicationManager)field!.GetValue(_service)!;
            return Mock.Get(manager);
        }
    }
}