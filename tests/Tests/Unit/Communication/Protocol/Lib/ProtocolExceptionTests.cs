using Communication.Protocol.Lib;

namespace Tests.Unit.Communication.Protocol.Lib
{
    /// <summary>
    /// Unit tests for ProtocolException class.
    /// Tests exception construction and inheritance.
    /// </summary>
    public class ProtocolExceptionTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithMessage_SetsMessage()
        {
            // Arrange
            string message = "Test protocol error";

            // Act
            var exception = new ProtocolException(message);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void Constructor_WithMessageAndInnerException_SetsBoth()
        {
            // Arrange
            string message = "Test protocol error";
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var exception = new ProtocolException(message, innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Same(innerException, exception.InnerException);
        }

        [Fact]
        public void Constructor_EmptyMessage_IsAllowed()
        {
            // Act
            var exception = new ProtocolException("");

            // Assert
            Assert.Equal("", exception.Message);
        }

        [Fact]
        public void Constructor_NullInnerException_IsAllowed()
        {
            // Act
            var exception = new ProtocolException("error", null!);

            // Assert
            Assert.Null(exception.InnerException);
        }

        #endregion

        #region Inheritance Tests

        [Fact]
        public void ProtocolException_InheritsFromException()
        {
            // Arrange & Act
            var exception = new ProtocolException("test");

            // Assert
            Assert.IsAssignableFrom<Exception>(exception);
        }

        [Fact]
        public void ProtocolException_CanBeCaughtAsException()
        {
            // Arrange & Act & Assert
            Exception? caught = null;
            try
            {
                throw new ProtocolException("test");
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.IsType<ProtocolException>(caught);
        }

        [Fact]
        public void ProtocolException_CanBeCaughtSpecifically()
        {
            // Arrange & Act & Assert
            ProtocolException? caught = null;
            try
            {
                throw new ProtocolException("test");
            }
            catch (ProtocolException ex)
            {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.Equal("test", caught.Message);
        }

        #endregion

        #region Throw and Propagate Tests

        [Fact]
        public void ProtocolException_PreservesStackTrace()
        {
            // Arrange & Act & Assert
            ProtocolException? caught = null;
            try
            {
                ThrowProtocolException();
            }
            catch (ProtocolException ex)
            {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.Contains("ThrowProtocolException", caught.StackTrace);
        }

        [Fact]
        public void ProtocolException_PropagatesInnerExceptionStackTrace()
        {
            // Arrange
            var innerException = CaptureException(() => throw new InvalidOperationException("inner"));

            // Act
            var exception = new ProtocolException("outer", innerException);

            // Assert
            Assert.NotNull(exception.InnerException?.StackTrace);
        }

        private static void ThrowProtocolException()
        {
            throw new ProtocolException("test from method");
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
    }
}
