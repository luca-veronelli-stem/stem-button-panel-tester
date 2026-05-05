using Core.Exceptions;

namespace Tests.Unit.Services.Lib
{
    /// <summary>
    /// Unit tests for CommunicationException class.
    /// Tests exception construction and inheritance.
    /// </summary>
    public class CommunicationExceptionTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithMessage_SetsMessage()
        {
            // Arrange
            string message = "Test communication error";

            // Act
            var exception = new CommunicationException(message);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void Constructor_WithMessageAndInnerException_SetsBoth()
        {
            // Arrange
            string message = "Test communication error";
            var innerException = new TimeoutException("Connection timeout");

            // Act
            var exception = new CommunicationException(message, innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Same(innerException, exception.InnerException);
        }

        [Fact]
        public void Constructor_EmptyMessage_IsAllowed()
        {
            // Act
            var exception = new CommunicationException("");

            // Assert
            Assert.Equal("", exception.Message);
        }

        #endregion

        #region Inheritance Tests

        [Fact]
        public void CommunicationException_InheritsFromException()
        {
            // Arrange & Act
            var exception = new CommunicationException("test");

            // Assert
            Assert.IsAssignableFrom<Exception>(exception);
        }

        [Fact]
        public void CommunicationException_CanBeCaughtAsException()
        {
            // Arrange & Act & Assert
            Exception? caught = null;
            try
            {
                throw new CommunicationException("test");
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.IsType<CommunicationException>(caught);
        }

        [Fact]
        public void CommunicationException_CanBeCaughtSpecifically()
        {
            // Arrange & Act & Assert
            CommunicationException? caught = null;
            try
            {
                throw new CommunicationException("test");
            }
            catch (CommunicationException ex)
            {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.Equal("test", caught.Message);
        }

        #endregion

        #region Throw and Propagate Tests

        [Fact]
        public void CommunicationException_PreservesStackTrace()
        {
            // Arrange & Act & Assert
            CommunicationException? caught = null;
            try
            {
                ThrowCommunicationException();
            }
            catch (CommunicationException ex)
            {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.Contains("ThrowCommunicationException", caught.StackTrace);
        }

        [Fact]
        public void CommunicationException_PropagatesInnerExceptionStackTrace()
        {
            // Arrange
            var innerException = CaptureException(() => throw new TimeoutException("timeout"));

            // Act
            var exception = new CommunicationException("outer", innerException);

            // Assert
            Assert.NotNull(exception.InnerException?.StackTrace);
        }

        [Fact]
        public void CommunicationException_ChainedExceptions_PreserveChain()
        {
            // Arrange
            var level1 = new InvalidOperationException("level 1");
            var level2 = new TimeoutException("level 2", level1);
            var level3 = new CommunicationException("level 3", level2);

            // Assert
            Assert.Equal("level 3", level3.Message);
            Assert.IsType<TimeoutException>(level3.InnerException);
            Assert.Equal("level 2", level3.InnerException?.Message);
            Assert.IsType<InvalidOperationException>(level3.InnerException?.InnerException);
            Assert.Equal("level 1", level3.InnerException?.InnerException?.Message);
        }

        private static void ThrowCommunicationException()
        {
            throw new CommunicationException("test from method");
        }

        private static Exception CaptureException(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                return ex;
            }
            return null!;
        }

        #endregion

        #region Real-World Usage Tests

        [Fact]
        public void CommunicationException_TypicalUsagePattern()
        {
            // Arrange - Simulate a typical communication failure scenario
            CommunicationException? caught = null;

            try
            {
                SimulateCommunicationFailure();
            }
            catch (CommunicationException ex)
            {
                caught = ex;
            }

            // Assert
            Assert.NotNull(caught);
            Assert.Contains("Failed to send", caught.Message);
            Assert.IsType<TimeoutException>(caught.InnerException);
        }

        private static void SimulateCommunicationFailure()
        {
            try
            {
                throw new TimeoutException("Connection timed out after 5000ms");
            }
            catch (TimeoutException ex)
            {
                throw new CommunicationException("Failed to send command", ex);
            }
        }

        #endregion
    }
}
