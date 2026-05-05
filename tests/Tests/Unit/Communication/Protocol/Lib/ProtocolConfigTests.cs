using Communication.Protocol.Lib;

namespace Tests.Unit.Communication.Protocol.Lib
{
    /// <summary>
    /// Unit tests for ProtocolConfig class.
    /// Tests packet ID generation and configuration constants.
    /// </summary>
    public class ProtocolConfigTests
    {
        #region Constants Tests

        [Fact]
        public void MinPacketId_IsOne()
        {
            Assert.Equal(1, ProtocolConfig.MinPacketId);
        }

        [Fact]
        public void MaxPacketId_IsSeven()
        {
            Assert.Equal(7, ProtocolConfig.MaxPacketId);
        }

        [Fact]
        public void PacketIdRange_IsValid()
        {
            Assert.True(ProtocolConfig.MinPacketId > 0);
            Assert.True(ProtocolConfig.MaxPacketId >= ProtocolConfig.MinPacketId);
            Assert.True(ProtocolConfig.MaxPacketId <= 7); // 3 bits max
        }

        #endregion

        #region GetNextPacketId Tests

        [Fact]
        public void GetNextPacketId_StartsAtMinPacketId()
        {
            // Arrange
            int currentId = ProtocolConfig.MinPacketId - 1;

            // Act
            int nextId = ProtocolConfig.GetNextPacketId(ref currentId);

            // Assert
            Assert.Equal(ProtocolConfig.MinPacketId, nextId);
        }

        [Fact]
        public void GetNextPacketId_IncrementsId()
        {
            // Arrange
            int currentId = ProtocolConfig.MinPacketId - 1;

            // Act
            int id1 = ProtocolConfig.GetNextPacketId(ref currentId);
            int id2 = ProtocolConfig.GetNextPacketId(ref currentId);
            int id3 = ProtocolConfig.GetNextPacketId(ref currentId);

            // Assert
            Assert.Equal(1, id1);
            Assert.Equal(2, id2);
            Assert.Equal(3, id3);
        }

        [Fact]
        public void GetNextPacketId_CyclesAfterMax()
        {
            // Arrange
            int currentId = ProtocolConfig.MaxPacketId;

            // Act
            int nextId = ProtocolConfig.GetNextPacketId(ref currentId);

            // Assert - Should cycle back to MinPacketId
            Assert.Equal(ProtocolConfig.MinPacketId, nextId);
        }

        [Fact]
        public void GetNextPacketId_FullCycle_ReturnsAllValidIds()
        {
            // Arrange
            int currentId = ProtocolConfig.MinPacketId - 1;
            var ids = new List<int>();

            // Act - Get one full cycle
            for (int i = 0; i < 7; i++)
            {
                ids.Add(ProtocolConfig.GetNextPacketId(ref currentId));
            }

            // Assert
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, ids);
        }

        [Fact]
        public void GetNextPacketId_MultipleCycles_StaysInRange()
        {
            // Arrange
            int currentId = ProtocolConfig.MinPacketId - 1;

            // Act - Get 3 full cycles
            for (int cycle = 0; cycle < 3; cycle++)
            {
                for (int i = 0; i < 7; i++)
                {
                    int id = ProtocolConfig.GetNextPacketId(ref currentId);

                    // Assert - Always within valid range
                    Assert.InRange(id, ProtocolConfig.MinPacketId, ProtocolConfig.MaxPacketId);
                }
            }
        }

        [Fact]
        public void GetNextPacketId_NeverReturnsZero()
        {
            // Arrange
            int currentId = ProtocolConfig.MinPacketId - 1;

            // Act & Assert - Check many iterations
            for (int i = 0; i < 100; i++)
            {
                int id = ProtocolConfig.GetNextPacketId(ref currentId);
                Assert.NotEqual(0, id);
            }
        }

        [Fact]
        public void GetNextPacketId_NeverExceedsMax()
        {
            // Arrange
            int currentId = ProtocolConfig.MinPacketId - 1;

            // Act & Assert - Check many iterations
            for (int i = 0; i < 100; i++)
            {
                int id = ProtocolConfig.GetNextPacketId(ref currentId);
                Assert.True(id <= ProtocolConfig.MaxPacketId, $"ID {id} exceeded max {ProtocolConfig.MaxPacketId}");
            }
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task GetNextPacketId_ConcurrentCalls_AllIdsValid()
        {
            // Arrange
            int currentId = ProtocolConfig.MinPacketId - 1;
            var ids = new System.Collections.Concurrent.ConcurrentBag<int>();
            int iterations = 1000;

            // Act - Call from multiple threads concurrently
            Task[] tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < iterations / 10; i++)
                {
                    int id = ProtocolConfig.GetNextPacketId(ref currentId);
                    ids.Add(id);
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            // Assert - All IDs should be valid
            Assert.Equal(iterations, ids.Count);
            Assert.All(ids, id => Assert.InRange(id, ProtocolConfig.MinPacketId, ProtocolConfig.MaxPacketId));
        }

        [Fact]
        public async Task GetNextPacketId_ConcurrentCalls_NoInvalidIds()
        {
            // Arrange
            int currentId = ProtocolConfig.MinPacketId - 1;
            var invalidIds = new System.Collections.Concurrent.ConcurrentBag<int>();

            // Act - Stress test with concurrent access
            Task[] tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                {
                    int id = ProtocolConfig.GetNextPacketId(ref currentId);
                    if (id < ProtocolConfig.MinPacketId || id > ProtocolConfig.MaxPacketId)
                    {
                        invalidIds.Add(id);
                    }
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            // Assert - No invalid IDs should have been generated
            Assert.Empty(invalidIds);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void GetNextPacketId_FromMaxValue_WrapsCorrectly()
        {
            // Arrange - Start at max
            int currentId = ProtocolConfig.MaxPacketId;

            // Act
            int nextId = ProtocolConfig.GetNextPacketId(ref currentId);

            // Assert - Should wrap to min
            Assert.Equal(ProtocolConfig.MinPacketId, nextId);
        }

        [Fact]
        public void GetNextPacketId_UpdatesRefParameter()
        {
            // Arrange
            int currentId = ProtocolConfig.MinPacketId - 1;

            // Act
            ProtocolConfig.GetNextPacketId(ref currentId);

            // Assert - currentId should have been updated
            Assert.True(currentId >= ProtocolConfig.MinPacketId - 1);
        }

        [Fact]
        public void GetNextPacketId_SequentialCalls_ProduceSequence()
        {
            // Arrange
            int currentId = ProtocolConfig.MinPacketId - 1;
            var sequence = new List<int>();

            // Act - Get 14 IDs (2 full cycles)
            for (int i = 0; i < 14; i++)
            {
                sequence.Add(ProtocolConfig.GetNextPacketId(ref currentId));
            }

            // Assert - Should have 2 complete cycles of 1-7
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7 }, sequence);
        }

        #endregion
    }
}
