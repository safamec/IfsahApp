using Xunit;
using Microsoft.Extensions.Logging;
using IfsahApp.Services.Email;

namespace IfsahApp.Tests.Services.Email;

public class FakeEmailServiceTests
{
    private class TestLogger<T> : ILogger<T>
    {
        public List<string> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception? exception, System.Func<TState, System.Exception?, string> formatter)
        {
            Logs.Add(formatter(state, exception));
        }
    }

    [Fact]
    public async Task SendAsync_Should_Log_Message()
    {
        // Arrange
        var logger = new TestLogger<FakeEmailService>();
        var service = new FakeEmailService(logger);

        string to = "test@example.com";
        string subject = "Test Subject";
        string body = "Hello World";

        // Act
        await service.SendAsync(to, subject, body, isHtml: true);

        // Assert
        Assert.Contains(logger.Logs, log => log.Contains(to) && log.Contains(subject));
    }
}
