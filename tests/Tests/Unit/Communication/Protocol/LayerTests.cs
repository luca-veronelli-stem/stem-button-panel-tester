using Communication.Protocol.Layers;

namespace Tests.Unit.Communication.Protocol
{
    /// <summary>
    /// Unit tests for the Layer abstract base class.
    /// Tests are performed through a concrete test implementation.
    /// </summary>
    public class LayerTests
    {
        /// <summary>
        /// Concrete implementation of Layer for testing purposes.
        /// </summary>
        private sealed class TestLayer : Layer
        {
            public TestLayer(byte[]? data) : base(data) { }
        }

        #region Constructor Tests

        public class ConstructorTests
        {
            [Fact]
            public void WithValidData_StoresData()
            {
                byte[] data = [0x01, 0x02, 0x03];

                var layer = new TestableLayer(data);

                Assert.Equal(data, layer.Data);
            }

            [Fact]
            public void WithEmptyData_StoresEmptyArray()
            {
                byte[] data = [];

                var layer = new TestableLayer(data);

                Assert.Empty(layer.Data);
                Assert.NotNull(layer.Data);
            }

            [Fact]
            public void WithNull_ReturnsEmptyArray()
            {
                var layer = new TestableLayer(null);

                Assert.NotNull(layer.Data);
                Assert.Empty(layer.Data);
            }

            [Fact]
            public void WithLargeData_StoresAllBytes()
            {
                byte[] data = new byte[10000];
                Random.Shared.NextBytes(data);

                var layer = new TestableLayer(data);

                Assert.Equal(data, layer.Data);
            }

            private sealed class TestableLayer : Layer
            {
                public TestableLayer(byte[]? data) : base(data) { }
            }
        }

        #endregion

        #region Data Property Tests

        public class DataPropertyTests
        {
            [Fact]
            public void Data_NeverReturnsNull()
            {
                var layer1 = new TestableLayer(null);
                var layer2 = new TestableLayer([]);
                var layer3 = new TestableLayer([0x01]);

                Assert.NotNull(layer1.Data);
                Assert.NotNull(layer2.Data);
                Assert.NotNull(layer3.Data);
            }

            [Fact]
            public void Data_ReturnsSameReference()
            {
                byte[] data = [0x01, 0x02, 0x03];
                var layer = new TestableLayer(data);

                var data1 = layer.Data;
                var data2 = layer.Data;

                Assert.Same(data1, data2);
            }

            [Fact]
            public void Data_IsNotCopied()
            {
                byte[] data = [0x01, 0x02, 0x03];
                var layer = new TestableLayer(data);

                // Modifying original array affects layer.Data
                data[0] = 0xFF;

                Assert.Equal(0xFF, layer.Data[0]);
            }

            private sealed class TestableLayer : Layer
            {
                public TestableLayer(byte[]? data) : base(data) { }
            }
        }

        #endregion

        #region Inheritance Verification

        public class InheritanceTests
        {
            [Fact]
            public void ApplicationLayer_InheritsFromLayer()
            {
                var appLayer = ApplicationLayer.Create(0x01, 0x02, [0x03]);

                Assert.IsAssignableFrom<Layer>(appLayer);
            }

            [Fact]
            public void AllLayers_ShareDataProperty()
            {
                byte[] payload = [0x01, 0x02, 0x03];
                var appLayer = ApplicationLayer.Create(0x01, 0x02, payload);

                // Data property from base class
                Assert.Equal(payload, appLayer.Data);
            }
        }

        #endregion
    }
}
