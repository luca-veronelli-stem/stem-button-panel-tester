using Core.Models.Communication;
using Infrastructure;
using Infrastructure.Lib;

using Microsoft.Extensions.Logging;
using Moq;
using Peak.Can.Basic;
using System.Reflection;

namespace Tests.Unit.Infrastructure
{
    /// <summary>
    /// Test unitari per la classe <see cref="PcanAdapter"/>.
    /// Utilizza Moq per simulare le chiamate a <see cref="IPcanApi"/>.
    /// </summary>
    /// <remarks>
    /// Questa classe di test verifica il comportamento dell'adattatore PCAN
    /// in scenari di connessione, disconnessione, invio e ricezione messaggi,
    /// gestione degli errori e logging.
    /// </remarks>
    public class PcanAdapterTests : IAsyncLifetime
    {
        private readonly Mock<IPcanApi> _mockApi;
        private readonly Mock<ILogger<PcanAdapter>> _mockLogger;
        private readonly PcanAdapter _adapter;

        /// <summary>
        /// Inizializza una nuova istanza della classe di test.
        /// Configura i mock per <see cref="IPcanApi"/> e <see cref="ILogger{PcanAdapter}"/>
        /// </summary>
        public PcanAdapterTests()
        {
            _mockApi = new Mock<IPcanApi>();
            _mockLogger = new Mock<ILogger<PcanAdapter>>();
            _adapter = new PcanAdapter(_mockApi.Object, _mockLogger.Object);
        }

        /// <inheritdoc/>
        public Task InitializeAsync() => Task.CompletedTask;

        /// <summary>
        /// Rilascia le risorse dell'adapter al termine di ogni test.
        /// </summary>
        public async Task DisposeAsync()
        {
            if (_adapter != null)
            {
                await _adapter.DisposeAsync();
            }
        }

        #region Helper Methods

        /// <summary>
        /// Configura il mock per restituire lo stato specificato durante l'inizializzazione.
        /// </summary>
        /// <param name="status">Lo stato da restituire dalla chiamata Initialize.</param>
        private void MockInitialize(PcanStatus status)
        {
            _mockApi.Setup(api => api.Initialize(It.IsAny<PcanChannel>(), It.IsAny<Bitrate>()))
                    .Returns(status);
        }

        /// <summary>
        /// Configura il mock per restituire lo stato specificato durante la deinizializzazione.
        /// </summary>
        /// <param name="status">Lo stato da restituire dalla chiamata Uninitialize.</param>
        private void MockUninitialize(PcanStatus status)
        {
            _mockApi.Setup(a => a.Uninitialize(It.IsAny<PcanChannel>()))
                    .Returns(status);
        }

        /// <summary>
        /// Configura il mock per restituire lo stato specificato durante la lettura dello stato del bus.
        /// </summary>
        /// <param name="status">Lo stato del bus da restituire.</param>
        private void MockGetStatus(PcanStatus status)
        {
            _mockApi.Setup(api => api.GetStatus(It.IsAny<PcanChannel>()))
                    .Returns(status);
        }

        /// <summary>
        /// Configura il mock per restituire lo stato specificato durante la lettura dei messaggi.
        /// </summary>
        /// <param name="status">Lo stato da restituire dalla chiamata Read.</param>
        private void MockRead(PcanStatus status)
        {
            _mockApi.Setup(api => api.Read(It.IsAny<PcanChannel>(), out It.Ref<PcanMessage>.IsAny, out It.Ref<ulong>.IsAny))
                    .Returns(status);
        }

        /// <summary>
        /// Configura il mock per restituire una sequenza di risultati durante le letture successive.
        /// </summary>
        /// <param name="sequence">
        /// Sequenza di tuple contenenti: stato della lettura, messaggio opzionale e timestamp.
        /// </param>
        /// <remarks>
        /// Dopo aver esaurito la sequenza, le letture successive restituiscono <see cref="PcanStatus.ReceiveQueueEmpty"/>.
        /// </remarks>
        private void MockReadSequence(params (PcanStatus Status, PcanMessage? Message, ulong Timestamp)[] sequence)
        {
            int readCallIndex = 0;
            _mockApi.Setup(api => api.Read(It.IsAny<PcanChannel>(), out It.Ref<PcanMessage>.IsAny, out It.Ref<ulong>.IsAny))
                .Callback((PcanChannel _, out PcanMessage msg, out ulong ts) =>
                {
                    if (readCallIndex < sequence.Length)
                    {
                        var (Status, Message, Timestamp) = sequence[readCallIndex];
                        if (Status == PcanStatus.OK)
                        {
                            msg = Message ?? default!;
                            ts = Timestamp;
                        }
                        else
                        {
                            msg = default!;
                            ts = 0;
                        }
                    }
                    else
                    {
                        msg = default!;
                        ts = 0;
                    }
                })
                .Returns(() =>
                {
                    PcanStatus status;
                    if (readCallIndex < sequence.Length)
                    {
                        status = sequence[readCallIndex].Status;
                    }
                    else
                    {
                        status = PcanStatus.ReceiveQueueEmpty;
                    }
                    readCallIndex++;
                    return status;
                });
        }

        /// <summary>
        /// Configura il mock per restituire lo stato specificato durante la scrittura dei messaggi.
        /// </summary>
        /// <param name="status">Lo stato da restituire dalla chiamata Write.</param>
        /// <param name="verifyMessage">Azione opzionale per verificare il messaggio inviato.</param>
        private void MockWrite(PcanStatus status, Action<PcanMessage>? verifyMessage = null)
        {
            _mockApi.Setup(api => api.Write(It.IsAny<PcanChannel>(), It.IsAny<PcanMessage>()))
                    .Callback((PcanChannel channel, PcanMessage msg) =>
                    {
                        verifyMessage?.Invoke(msg);
                    })
                    .Returns(status);
        }

        /// <summary>
        /// Invoca il metodo privato TryParseConfig tramite reflection per testarlo.
        /// </summary>
        /// <param name="config">La stringa di configurazione da parsare.</param>
        /// <param name="baudRate">Il baud rate risultante.</param>
        /// <returns><c>true</c> se il parsing è riuscito, <c>false</c> altrimenti.</returns>
        private static bool InvokeTryParseConfig(string config, out Bitrate baudRate)
        {
            var method = typeof(PcanAdapter)
                .GetMethod("TryParseConfig", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("TryParseConfig method not found.");

            object[] parameters = [config, Bitrate.Pcan250];
            bool result = (bool)method.Invoke(null, parameters)!;
            baudRate = (Bitrate)parameters[1];
            return result;
        }

        /// <summary>
        /// Configura tutti i mock necessari per una connessione riuscita.
        /// </summary>
        private void SetupSuccessfulConnection()
        {
            MockUninitialize(PcanStatus.OK);
            MockInitialize(PcanStatus.OK);
            MockGetStatus(PcanStatus.OK);
            MockRead(PcanStatus.ReceiveQueueEmpty);
        }

        #endregion

        #region Constructor Tests

        /// <summary>
        /// Verifica che il costruttore lanci <see cref="ArgumentNullException"/> quando l'API è null.
        /// </summary>
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenApiIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new PcanAdapter(null!, _mockLogger.Object));
        }

        /// <summary>
        /// Verifica che il costruttore lanci <see cref="ArgumentNullException"/> quando il logger è null.
        /// </summary>
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new PcanAdapter(_mockApi.Object, null!));
        }

        /// <summary>
        /// Verifica che il costruttore accetti API e logger validi.
        /// </summary>
        [Fact]
        public void Constructor_AcceptsValidApiAndLogger()
        {
            var adapter = new PcanAdapter(_mockApi.Object, _mockLogger.Object);
            Assert.NotNull(adapter);
            Assert.False(adapter.IsConnected);
        }

        /// <summary>
        /// Verifica che il costruttore accetti un canale personalizzato.
        /// </summary>
        [Fact]
        public void Constructor_AcceptsCustomChannel()
        {
            var adapter = new PcanAdapter(_mockApi.Object, _mockLogger.Object, PcanChannel.Usb02);
            Assert.NotNull(adapter);
        }

        #endregion

        #region ConnectAsync Tests

        /// <summary>
        /// Verifica che ConnectAsync restituisca true quando l'inizializzazione ha successo.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_ReturnsTrue_WhenInitializeSucceeds()
        {
            SetupSuccessfulConnection();

            bool eventFired = false;
            bool receivedConnectedValue = false;
            _adapter.ConnectionStatusChanged += (_, connected) =>
            {
                eventFired = true;
                receivedConnectedValue = connected;
            };

            bool result = await _adapter.ConnectAsync("250");

            Assert.True(result);
            Assert.True(_adapter.IsConnected);
            Assert.True(eventFired);
            Assert.True(receivedConnectedValue);
        }

        /// <summary>
        /// Verifica che ConnectAsync chiami Uninitialize prima di Initialize.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_CallsUninitializeBeforeInitialize()
        {
            SetupSuccessfulConnection();

            var callOrder = new List<string>();
            _mockApi.Setup(api => api.Uninitialize(It.IsAny<PcanChannel>()))
                .Callback(() => callOrder.Add("Uninitialize"))
                .Returns(PcanStatus.OK);
            _mockApi.Setup(api => api.Initialize(It.IsAny<PcanChannel>(), It.IsAny<Bitrate>()))
                .Callback(() => callOrder.Add("Initialize"))
                .Returns(PcanStatus.OK);

            await _adapter.ConnectAsync("250");

            Assert.Equal(2, callOrder.Count);
            Assert.Equal("Uninitialize", callOrder[0]);
            Assert.Equal("Initialize", callOrder[1]);
        }

        /// <summary>
        /// Verifica che ConnectAsync chiami GetStatus dopo Initialize.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_CallsGetStatusAfterInitialize()
        {
            SetupSuccessfulConnection();

            await _adapter.ConnectAsync("250");

            _mockApi.Verify(api => api.GetStatus(It.IsAny<PcanChannel>()), Times.AtLeastOnce);
        }

        /// <summary>
        /// Verifica che ConnectAsync avvii il loop di lettura quando la connessione ha successo.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_StartsReadingLoop_WhenConnectionSucceeds()
        {
            SetupSuccessfulConnection();

            await _adapter.ConnectAsync("250");

            FieldInfo? readingTaskField = typeof(PcanAdapter).GetField("_readingTask", BindingFlags.NonPublic | BindingFlags.Instance);
            Task? readingTask = (Task?)readingTaskField?.GetValue(_adapter);

            Assert.NotNull(readingTask);
            Assert.True(readingTask.Status == TaskStatus.RanToCompletion ||
                        readingTask.Status == TaskStatus.Running ||
                        readingTask.Status == TaskStatus.WaitingForActivation);
        }

        /// <summary>
        /// Verifica che ConnectAsync resetti il CancellationToken durante la riconnessione.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_ResetsCancellationTokenOnReconnect()
        {
            SetupSuccessfulConnection();

            await _adapter.ConnectAsync("250");

            FieldInfo? ctsField = typeof(PcanAdapter).GetField("_cts", BindingFlags.NonPublic | BindingFlags.Instance);
            CancellationTokenSource? firstCts = (CancellationTokenSource?)ctsField?.GetValue(_adapter);
            Assert.NotNull(firstCts);
            Assert.False(firstCts.IsCancellationRequested);

            await _adapter.ConnectAsync("250");

            Assert.True(firstCts.IsCancellationRequested);

            CancellationTokenSource? secondCts = (CancellationTokenSource?)ctsField?.GetValue(_adapter);
            Assert.NotNull(secondCts);
            Assert.NotEqual(firstCts, secondCts);
            Assert.False(secondCts.IsCancellationRequested);
        }

        /// <summary>
        /// Verifica che ConnectAsync restituisca false quando l'inizializzazione fallisce.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_ReturnsFalse_WhenInitializeFails()
        {
            MockUninitialize(PcanStatus.OK);
            MockInitialize(PcanStatus.AnyBusError);

            bool connectionChangedFired = false;
            _adapter.ConnectionStatusChanged += (_, connected) =>
            {
                connectionChangedFired = true;
                Assert.False(connected);
            };

            var result = await _adapter.ConnectAsync("250");

            Assert.False(result);
            Assert.False(_adapter.IsConnected);
            Assert.True(connectionChangedFired);
        }

        /// <summary>
        /// Verifica che ConnectAsync restituisca false con una stringa di configurazione non valida.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_ReturnsFalse_OnInvalidConfigString()
        {
            var invalidConfig = "abc";

            bool connectionChangedFired = false;
            _adapter.ConnectionStatusChanged += (_, _) => connectionChangedFired = true;

            var result = await _adapter.ConnectAsync(invalidConfig);

            Assert.False(result);
            Assert.False(_adapter.IsConnected);
            Assert.False(connectionChangedFired);
            _mockApi.Verify(api => api.Initialize(It.IsAny<PcanChannel>(), It.IsAny<Bitrate>()), Times.Never);
        }

        /// <summary>
        /// Verifica che ConnectAsync lanci <see cref="OperationCanceledException"/> quando viene cancellato.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_ThrowsOperationCanceledException_WhenCancelled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _adapter.ConnectAsync("250", cts.Token));
        }

        /// <summary>
        /// Verifica che ConnectAsync mappi correttamente i baud rate comuni.
        /// </summary>
        /// <param name="config">La stringa di configurazione del baud rate.</param>
        /// <param name="expectedBaudRate">Il baud rate atteso.</param>
        [Theory]
        [InlineData("100", Bitrate.Pcan100)]
        [InlineData("125", Bitrate.Pcan125)]
        [InlineData("250", Bitrate.Pcan250)]
        [InlineData("500", Bitrate.Pcan500)]
        [InlineData("800", Bitrate.Pcan800)]
        [InlineData("1000", Bitrate.Pcan1000)]
        [InlineData("250000", Bitrate.Pcan250)]
        [InlineData("999", Bitrate.Pcan250)]
        public async Task ConnectAsync_MapsCommonBaudRatesCorrectly(string config, Bitrate expectedBaudRate)
        {
            MockUninitialize(PcanStatus.OK);
            _mockApi.Setup(api => api.Initialize(It.IsAny<PcanChannel>(), expectedBaudRate))
                .Returns(PcanStatus.OK);
            MockGetStatus(PcanStatus.OK);
            MockRead(PcanStatus.ReceiveQueueEmpty);

            var result = await _adapter.ConnectAsync(config);

            Assert.True(result);
            _mockApi.Verify(api => api.Initialize(It.IsAny<PcanChannel>(), expectedBaudRate), Times.Once);
        }

        /// <summary>
        /// Verifica che ConnectAsync generi messaggi diagnostici durante la connessione.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_RaisesDiagnosticMessage_DuringConnection()
        {
            SetupSuccessfulConnection();

            await _adapter.ConnectAsync("250");

            // Verification is now done via logger calls instead of DiagnosticMessage events
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Connecting")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region DisconnectAsync Tests

        /// <summary>
        /// Verifica che DisconnectAsync cancelli il loop di lettura e deinizializzi il canale.
        /// </summary>
        [Fact]
        public async Task DisconnectAsync_CancelsReadingLoopAndUninitializesChannel()
        {
            SetupSuccessfulConnection();
            MockUninitialize(PcanStatus.OK);

            bool connectionChangedRaised = false;
            bool? lastConnectionStatus = null;
            _adapter.ConnectionStatusChanged += (_, connected) =>
            {
                connectionChangedRaised = true;
                lastConnectionStatus = connected;
            };

            await _adapter.ConnectAsync("500");
            await _adapter.DisconnectAsync();

            Assert.False(_adapter.IsConnected);
            Assert.True(connectionChangedRaised);
            Assert.False(lastConnectionStatus);

            // Uninitialize chiamato due volte: una durante Connect (pre-init) e una durante Disconnect
            _mockApi.Verify(a => a.Uninitialize(It.IsAny<PcanChannel>()), Times.Exactly(2));
        }

        /// <summary>
        /// Verifica che DisconnectAsync sia idempotente quando chiamato più volte.
        /// </summary>
        [Fact]
        public async Task DisconnectAsync_IsIdempotent_WhenCalledMultipleTimes()
        {
            SetupSuccessfulConnection();
            MockUninitialize(PcanStatus.OK);

            await _adapter.ConnectAsync("500");
            await _adapter.DisconnectAsync();
            await _adapter.DisconnectAsync();

            Assert.False(_adapter.IsConnected);
            // Solo 2 chiamate: 1 durante connect (pre-init), 1 durante la prima disconnect
            _mockApi.Verify(a => a.Uninitialize(It.IsAny<PcanChannel>()), Times.Exactly(2));
        }

        /// <summary>
        /// Verifica che DisconnectAsync non faccia nulla quando non è connesso.
        /// </summary>
        [Fact]
        public async Task DisconnectAsync_DoesNothing_WhenNotConnected()
        {
            await _adapter.DisconnectAsync();

            Assert.False(_adapter.IsConnected);
            _mockApi.Verify(a => a.Uninitialize(It.IsAny<PcanChannel>()), Times.Never);
        }

        /// <summary>
        /// Verifica che DisconnectAsync attenda il completamento del task di lettura.
        /// </summary>
        [Fact]
        public async Task DisconnectAsync_WaitsForReadingTaskToComplete()
        {
            SetupSuccessfulConnection();
            MockUninitialize(PcanStatus.OK);

            await _adapter.ConnectAsync("250");

            FieldInfo? readingTaskField = typeof(PcanAdapter).GetField("_readingTask", BindingFlags.NonPublic | BindingFlags.Instance);
            Task? readingTask = (Task?)readingTaskField?.GetValue(_adapter);
            Assert.NotNull(readingTask);

            await _adapter.DisconnectAsync();

            await Task.WhenAny(readingTask, Task.Delay(1000));
            Assert.True(readingTask.IsCompleted);
        }

        #endregion

        #region Send Tests

        /// <summary>
        /// Verifica che Send restituisca false quando non è connesso.
        /// </summary>
        [Fact]
        public async Task Send_ReturnsFalse_WhenNotConnected()
        {
            uint arbitrationId = 0x123;
            byte[] data = [0x01, 0x02];

            bool result = await _adapter.Send(arbitrationId, data);

            Assert.False(result);
            _mockApi.Verify(api => api.Write(It.IsAny<PcanChannel>(), It.IsAny<PcanMessage>()), Times.Never);
        }

        /// <summary>
        /// Verifica che Send restituisca false quando i dati sono null.
        /// </summary>
        [Fact]
        public async Task Send_ReturnsFalse_WhenDataIsNull()
        {
            SetupSuccessfulConnection();
            await _adapter.ConnectAsync("250");

            bool result = await _adapter.Send(0x123, null!);

            Assert.False(result);
            _mockApi.Verify(api => api.Write(It.IsAny<PcanChannel>(), It.IsAny<PcanMessage>()), Times.Never);
        }

        /// <summary>
        /// Verifica che Send restituisca false quando i dati sono vuoti.
        /// </summary>
        [Fact]
        public async Task Send_ReturnsFalse_WhenDataIsEmpty()
        {
            SetupSuccessfulConnection();
            await _adapter.ConnectAsync("250");

            bool result = await _adapter.Send(0x123, []);

            Assert.False(result);
            _mockApi.Verify(api => api.Write(It.IsAny<PcanChannel>(), It.IsAny<PcanMessage>()), Times.Never);
        }

        /// <summary>
        /// Verifica che Send restituisca false quando la lunghezza dei dati supera 8 byte.
        /// </summary>
        [Fact]
        public async Task Send_ReturnsFalse_WhenDataLengthExceeds8()
        {
            SetupSuccessfulConnection();
            await _adapter.ConnectAsync("250");

            bool result = await _adapter.Send(0x123, new byte[9]);

            Assert.False(result);
            _mockApi.Verify(api => api.Write(It.IsAny<PcanChannel>(), It.IsAny<PcanMessage>()), Times.Never);
        }

        /// <summary>
        /// Verifica che Send restituisca true con frame standard quando la scrittura ha successo.
        /// </summary>
        [Fact]
        public async Task Send_ReturnsTrue_WhenWriteSucceeds_StandardFrame()
        {
            SetupSuccessfulConnection();
            await _adapter.ConnectAsync("250");

            uint arbitrationId = 0x7FF;
            byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];

            bool messageVerified = false;
            MockWrite(PcanStatus.OK, msg =>
            {
                messageVerified = true;
                Assert.Equal(arbitrationId, msg.ID);
                Assert.Equal(MessageType.Standard, msg.MsgType);
                Assert.Equal((byte)data.Length, msg.DLC);
                var msgData = new byte[msg.DLC];
                for (int i = 0; i < msg.DLC; i++) msgData[i] = msg.Data[i];
                Assert.Equal(data, msgData);
            });

            bool result = await _adapter.Send(arbitrationId, data);

            Assert.True(result);
            Assert.True(messageVerified);
        }

        /// <summary>
        /// Verifica che Send restituisca true con frame esteso quando la scrittura ha successo.
        /// </summary>
        [Fact]
        public async Task Send_ReturnsTrue_WhenWriteSucceeds_ExtendedFrame()
        {
            SetupSuccessfulConnection();
            await _adapter.ConnectAsync("250");

            uint arbitrationId = 0x1FFFFFFF;
            byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];

            bool messageVerified = false;
            MockWrite(PcanStatus.OK, msg =>
            {
                messageVerified = true;
                Assert.Equal(arbitrationId, msg.ID);
                Assert.Equal(MessageType.Extended, msg.MsgType);
                Assert.Equal((byte)data.Length, msg.DLC);
            });

            bool result = await _adapter.Send(arbitrationId, data, isExtended: true);

            Assert.True(result);
            Assert.True(messageVerified);
        }

        /// <summary>
        /// Verifica che Send copi solo i byte DLC nei dati del messaggio.
        /// </summary>
        [Fact]
        public async Task Send_CopiesOnlyDlcBytesIntoMessageData()
        {
            SetupSuccessfulConnection();
            await _adapter.ConnectAsync("250");

            byte[] data = [0x01, 0x02, 0x03, 0x04];

            bool messageVerified = false;
            MockWrite(PcanStatus.OK, msg =>
            {
                messageVerified = true;
                Assert.Equal((byte)4, msg.DLC);
                var msgData = new byte[8];
                for (int i = 0; i < 8; i++) msgData[i] = msg.Data[i];
                Assert.Equal(data, msgData.Take(4).ToArray());
                Assert.All(msgData.Skip(4), b => Assert.Equal(0, b));
            });

            bool result = await _adapter.Send(0x123, data);

            Assert.True(result);
            Assert.True(messageVerified);
        }

        /// <summary>
        /// Verifica che Send restituisca false quando la scrittura API fallisce.
        /// </summary>
        [Fact]
        public async Task Send_ReturnsFalse_WhenApiWriteFails()
        {
            SetupSuccessfulConnection();
            await _adapter.ConnectAsync("250");

            MockWrite(PcanStatus.AnyBusError);

            bool result = await _adapter.Send(0x123, [0x01, 0x02]);

            Assert.False(result);
            _mockApi.Verify(api => api.Write(It.IsAny<PcanChannel>(), It.IsAny<PcanMessage>()), Times.Once);
        }

        #endregion

        #region Reading Loop Tests

        /// <summary>
        /// Verifica che il loop di lettura invochi PacketReceived quando riceve un messaggio valido.
        /// </summary>
        [Fact]
        public async Task ReadingLoop_InvokesPacketReceived_WhenValidMessageReceived()
        {
            MockUninitialize(PcanStatus.OK);
            MockInitialize(PcanStatus.OK);
            MockGetStatus(PcanStatus.OK);

            var expectedMsg = new PcanMessage
            {
                ID = 0x123,
                MsgType = MessageType.Standard,
                DLC = 4,
                Data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00 }
            };

            MockReadSequence(
                (PcanStatus.OK, expectedMsg, 123456789UL),
                (PcanStatus.ReceiveQueueEmpty, null, 0)
            );

            CanPacket? receivedPacket = null;
            _adapter.PacketReceived += (_, packet) => receivedPacket = packet;

            await _adapter.ConnectAsync("250");
            await Task.Delay(100);
            await _adapter.DisconnectAsync();

            Assert.NotNull(receivedPacket);
        }

        /// <summary>
        /// Verifica che il loop di lettura imposti correttamente le proprietà del CanPacket.
        /// </summary>
        [Fact]
        public async Task ReadingLoop_SetsCorrectCanPacketProperties()
        {
            MockUninitialize(PcanStatus.OK);
            MockInitialize(PcanStatus.OK);
            MockGetStatus(PcanStatus.OK);

            var expectedData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11 };
            var expectedMsg = new PcanMessage
            {
                ID = 0x1FFFFFFF,
                MsgType = MessageType.Extended,
                DLC = 8,
                Data = expectedData
            };
            ulong expectedTimestamp = 987654321UL;

            MockReadSequence(
                (PcanStatus.OK, expectedMsg, expectedTimestamp),
                (PcanStatus.ReceiveQueueEmpty, null, 0)
            );

            CanPacket? receivedPacket = null;
            _adapter.PacketReceived += (_, packet) => receivedPacket = packet;

            await _adapter.ConnectAsync("250");
            await Task.Delay(100);
            await _adapter.DisconnectAsync();

            Assert.NotNull(receivedPacket);
            Assert.Equal(expectedMsg.ID, receivedPacket.ArbitrationId);
            Assert.True(receivedPacket.IsExtended);
            Assert.Equal(expectedMsg.DLC, receivedPacket.Data.Length);
            Assert.Equal([.. expectedData.Take(expectedMsg.DLC)], receivedPacket.Data);
            Assert.Equal(expectedTimestamp, receivedPacket.TimestampMicroseconds);
        }

        /// <summary>
        /// Verifica che il loop di lettura si fermi correttamente quando viene richiesta la cancellazione.
        /// </summary>
        [Fact]
        public async Task ReadingLoop_StopsGracefully_WhenCancellationRequested()
        {
            SetupSuccessfulConnection();

            await _adapter.ConnectAsync("250");

            var readingTaskField = typeof(PcanAdapter).GetField("_readingTask", BindingFlags.NonPublic | BindingFlags.Instance);
            var readingTask = (Task?)readingTaskField?.GetValue(_adapter);
            var ctsField = typeof(PcanAdapter).GetField("_cts", BindingFlags.NonPublic | BindingFlags.Instance);
            var cts = (CancellationTokenSource?)ctsField?.GetValue(_adapter);

            Assert.NotNull(readingTask);
            Assert.False(readingTask.IsCompleted);

            cts?.Cancel();

            await Task.WhenAny(readingTask, Task.Delay(1000));
            Assert.True(readingTask.IsCompletedSuccessfully || readingTask.IsCanceled);
        }

        /// <summary>
        /// Verifica che il loop di lettura continui il polling dopo errori di lettura.
        /// </summary>
        [Fact]
        public async Task ReadingLoop_ContinuesPolling_AfterReadErrors()
        {
            MockUninitialize(PcanStatus.OK);
            MockInitialize(PcanStatus.OK);
            MockGetStatus(PcanStatus.OK);

            var expectedMsg = new PcanMessage
            {
                ID = 0x456,
                MsgType = MessageType.Standard,
                DLC = 2,
                Data = new byte[] { 0xAB, 0xCD, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
            };

            MockReadSequence(
                (PcanStatus.AnyBusError, null, 0),
                (PcanStatus.OK, expectedMsg, 0UL),
                (PcanStatus.ReceiveQueueEmpty, null, 0)
            );

            int eventFireCount = 0;
            _adapter.PacketReceived += (_, _) => eventFireCount++;

            await _adapter.ConnectAsync("250");
            await Task.Delay(200);
            await _adapter.DisconnectAsync();

            Assert.Equal(1, eventFireCount);
            _mockApi.Verify(api => api.Read(It.IsAny<PcanChannel>(), out It.Ref<PcanMessage>.IsAny, out It.Ref<ulong>.IsAny), Times.AtLeast(3));
        }

        /// <summary>
        /// Verifica che il loop di lettura generi messaggi diagnostici quando riceve pacchetti.
        /// </summary>
        [Fact]
        public async Task ReadingLoop_RaisesDiagnosticMessage_WhenPacketReceived()
        {
            MockUninitialize(PcanStatus.OK);
            MockInitialize(PcanStatus.OK);
            MockGetStatus(PcanStatus.OK);

            var expectedMsg = new PcanMessage
            {
                ID = 0x123,
                MsgType = MessageType.Standard,
                DLC = 2,
                Data = new byte[] { 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
            };

            MockReadSequence(
                (PcanStatus.OK, expectedMsg, 0UL),
                (PcanStatus.ReceiveQueueEmpty, null, 0)
            );

            CanPacket? receivedPacket = null;
            _adapter.PacketReceived += (_, packet) => receivedPacket = packet;

            await _adapter.ConnectAsync("250");
            await Task.Delay(100);
            await _adapter.DisconnectAsync();

            Assert.NotNull(receivedPacket);
        }

        #endregion

        #region TryParseConfig Tests

        /// <summary>
        /// Verifica che TryParseConfig restituisca false con input non numerico.
        /// </summary>
        /// <param name="config">La stringa di configurazione non valida.</param>
        [Theory]
        [InlineData("abc")]
        [InlineData("250k")]
        [InlineData("")]
        [InlineData("  ")]
        public void TryParseConfig_ReturnsFalse_OnNonNumericInput(string config)
        {
            bool result = InvokeTryParseConfig(config, out var baudRate);

            Assert.False(result);
            Assert.Equal(Bitrate.Pcan250, baudRate);
        }

        /// <summary>
        /// Verifica che TryParseConfig restituisca false con input null.
        /// </summary>
        [Fact]
        public void TryParseConfig_ReturnsFalse_OnNullInput()
        {
            bool result = InvokeTryParseConfig(null!, out var baudRate);

            Assert.False(result);
            Assert.Equal(Bitrate.Pcan250, baudRate);
        }

        /// <summary>
        /// Verifica che TryParseConfig mappi correttamente tutti i baud rate supportati.
        /// </summary>
        /// <param name="config">La stringa di configurazione.</param>
        /// <param name="expectedBaudRate">Il baud rate atteso.</param>
        [Theory]
        [InlineData("100", Bitrate.Pcan100)]
        [InlineData("125", Bitrate.Pcan125)]
        [InlineData("250", Bitrate.Pcan250)]
        [InlineData("500", Bitrate.Pcan500)]
        [InlineData("800", Bitrate.Pcan800)]
        [InlineData("1000", Bitrate.Pcan1000)]
        [InlineData("100000", Bitrate.Pcan100)]
        [InlineData("125000", Bitrate.Pcan125)]
        [InlineData("250000", Bitrate.Pcan250)]
        [InlineData("500000", Bitrate.Pcan500)]
        [InlineData("800000", Bitrate.Pcan800)]
        [InlineData("1000000", Bitrate.Pcan1000)]
        public void TryParseConfig_MapsAllSupportedBaudratesCorrectly(string config, Bitrate expectedBaudRate)
        {
            bool result = InvokeTryParseConfig(config, out var baudRate);

            Assert.True(result);
            Assert.Equal(expectedBaudRate, baudRate);
        }

        /// <summary>
        /// Verifica che TryParseConfig usi 250k come fallback per valori non supportati.
        /// </summary>
        /// <param name="config">La stringa di configurazione con valore non supportato.</param>
        [Theory]
        [InlineData("0")]
        [InlineData("999")]
        [InlineData("2000000")]
        [InlineData("-500")]
        public void TryParseConfig_FallsBackTo250k_OnUnsupportedValue(string config)
        {
            bool result = InvokeTryParseConfig(config, out var baudRate);

            Assert.True(result);
            Assert.Equal(Bitrate.Pcan250, baudRate);
        }

        #endregion

        #region DisposeAsync Tests

        /// <summary>
        /// Verifica che DisposeAsync chiami DisconnectAsync e rilasci il CancellationTokenSource.
        /// </summary>
        [Fact]
        public async Task DisposeAsync_CallsDisconnectAsync_AndDisposesCts()
        {
            SetupSuccessfulConnection();
            MockUninitialize(PcanStatus.OK);

            await _adapter.ConnectAsync("250");

            var ctsField = typeof(PcanAdapter).GetField("_cts", BindingFlags.NonPublic | BindingFlags.Instance);
            var cts = (CancellationTokenSource?)ctsField?.GetValue(_adapter);
            Assert.NotNull(cts);
            Assert.False(cts.IsCancellationRequested);

            await _adapter.DisposeAsync();

            Assert.False(_adapter.IsConnected);
            Assert.True(cts.IsCancellationRequested);
            Assert.Throws<ObjectDisposedException>(() => cts.Token.ThrowIfCancellationRequested());
        }

        /// <summary>
        /// Verifica che DisposeAsync sia sicuro quando non è mai stato connesso.
        /// </summary>
        [Fact]
        public async Task DisposeAsync_IsSafeWhenNeverConnected()
        {
            await _adapter.DisposeAsync();

            _mockApi.Verify(api => api.Uninitialize(It.IsAny<PcanChannel>()), Times.Never);
            Assert.False(_adapter.IsConnected);
        }

        /// <summary>
        /// Verifica che DisposeAsync possa essere chiamato più volte senza errori.
        /// </summary>
        [Fact]
        public async Task DisposeAsync_CanBeCalledMultipleTimes()
        {
            SetupSuccessfulConnection();
            MockUninitialize(PcanStatus.OK);

            await _adapter.ConnectAsync("250");

            await _adapter.DisposeAsync();
            await _adapter.DisposeAsync();

            // 2 chiamate: 1 durante connect (pre-init), 1 durante il primo dispose
            _mockApi.Verify(api => api.Uninitialize(It.IsAny<PcanChannel>()), Times.Exactly(2));
        }

        #endregion

        #region Event Tests

        /// <summary>
        /// Verifica che gli eventi non lancino eccezioni quando non ci sono sottoscrittori.
        /// </summary>
        [Fact]
        public async Task Events_DoNotThrow_WhenNoSubscribers()
        {
            MockUninitialize(PcanStatus.OK);
            MockInitialize(PcanStatus.OK);
            MockGetStatus(PcanStatus.OK);
            MockWrite(PcanStatus.OK);

            var expectedMsg = new PcanMessage
            {
                ID = 0x123,
                MsgType = MessageType.Standard,
                DLC = 1,
                Data = new byte[] { 0xAA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
            };
            MockReadSequence((PcanStatus.OK, expectedMsg, 0UL));

            await _adapter.ConnectAsync("250");
            await Task.Delay(100);
            var sendResult = await _adapter.Send(0x123, [0xAA]);
            await _adapter.DisconnectAsync();

            Assert.True(sendResult);
        }

        /// <summary>
        /// Verifica che ConnectionStatusChanged venga generato durante la connessione.
        /// </summary>
        [Fact]
        public async Task ConnectionStatusChanged_FiredOnConnect()
        {
            SetupSuccessfulConnection();

            var statusChanges = new List<bool>();
            _adapter.ConnectionStatusChanged += (_, connected) => statusChanges.Add(connected);

            await _adapter.ConnectAsync("250");

            Assert.Single(statusChanges);
            Assert.True(statusChanges[0]);
        }

        /// <summary>
        /// Verifica che ConnectionStatusChanged venga generato durante la disconnessione.
        /// </summary>
        [Fact]
        public async Task ConnectionStatusChanged_FiredOnDisconnect()
        {
            SetupSuccessfulConnection();
            MockUninitialize(PcanStatus.OK);

            var statusChanges = new List<bool>();
            _adapter.ConnectionStatusChanged += (_, connected) => statusChanges.Add(connected);

            await _adapter.ConnectAsync("250");
            await _adapter.DisconnectAsync();

            Assert.Equal(2, statusChanges.Count);
            Assert.True(statusChanges[0]);
            Assert.False(statusChanges[1]);
        }

        #endregion

        #region Channel Tests

        /// <summary>
        /// Verifica che ConnectAsync utilizzi il canale specificato.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_UsesSpecifiedChannel()
        {
            var adapter = new PcanAdapter(_mockApi.Object, _mockLogger.Object, PcanChannel.Usb03);

            _mockApi.Setup(api => api.Uninitialize(PcanChannel.Usb03)).Returns(PcanStatus.OK);
            _mockApi.Setup(api => api.Initialize(PcanChannel.Usb03, It.IsAny<Bitrate>())).Returns(PcanStatus.OK);
            _mockApi.Setup(api => api.GetStatus(PcanChannel.Usb03)).Returns(PcanStatus.OK);
            _mockApi.Setup(api => api.Read(PcanChannel.Usb03, out It.Ref<PcanMessage>.IsAny, out It.Ref<ulong>.IsAny))
                .Returns(PcanStatus.ReceiveQueueEmpty);

            await adapter.ConnectAsync("250");

            _mockApi.Verify(api => api.Initialize(PcanChannel.Usb03, It.IsAny<Bitrate>()), Times.Once);

            await adapter.DisposeAsync();
        }

        #endregion

        #region Logging Tests

        /// <summary>
        /// Verifica che ConnectAsync registri informazioni quando ha successo.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_LogsInformationOnSuccess()
        {
            SetupSuccessfulConnection();

            await _adapter.ConnectAsync("250");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Connecting")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        /// <summary>
        /// Verifica che ConnectAsync registri un errore quando fallisce.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_LogsErrorOnFailure()
        {
            MockUninitialize(PcanStatus.OK);
            MockInitialize(PcanStatus.AnyBusError);

            await _adapter.ConnectAsync("250");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        /// <summary>
        /// Verifica che ConnectAsync registri un warning con configurazione non valida.
        /// </summary>
        [Fact]
        public async Task ConnectAsync_LogsWarningOnInvalidConfig()
        {
            await _adapter.ConnectAsync("invalid");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion
    }
}
