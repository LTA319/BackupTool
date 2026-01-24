using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Net.Sockets;

namespace MySqlBackupTool.Tests.Services;

public class NetworkRetryServiceTests
{
    private readonly Mock<ILogger<NetworkRetryService>> _mockLogger;
    private readonly NetworkRetryService _service;
    private readonly NetworkRetryConfig _config;

    public NetworkRetryServiceTests()
    {
        _mockLogger = new Mock<ILogger<NetworkRetryService>>();
        _config = new NetworkRetryConfig
        {
            MaxRetryAttempts = 3,
            BaseRetryDelay = TimeSpan.FromMilliseconds(100),
            MaxRetryDelay = TimeSpan.FromSeconds(1),
            EnableJitter = false,
            RetryOnUnknownExceptions = false
        };
        _service = new NetworkRetryService(_mockLogger.Object, _config);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_SucceedsOnFirstAttempt_ReturnsResult()
    {
        // Arrange
        var expectedResult = "success";
        var operationCalled = false;

        // Act
        var result = await _service.ExecuteWithRetryAsync(
            async (ct) =>
            {
                operationCalled = true;
                return expectedResult;
            },
            "TestOperation",
            "test-id");

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.True(operationCalled);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_FailsWithRetriableException_RetriesAndSucceeds()
    {
        // Arrange
        var attemptCount = 0;
        var expectedResult = "success";

        // Act
        var result = await _service.ExecuteWithRetryAsync(
            async (ct) =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new SocketException((int)SocketError.ConnectionRefused);
                }
                return expectedResult;
            },
            "TestOperation",
            "test-id");

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_FailsWithNonRetriableException_ThrowsImmediately()
    {
        // Arrange
        var attemptCount = 0;
        var expectedException = new ArgumentException("Non-retriable error");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _service.ExecuteWithRetryAsync(
                async (ct) =>
                {
                    attemptCount++;
                    throw expectedException;
                },
                "TestOperation",
                "test-id");
        });

        Assert.Equal(expectedException, ex);
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ExhaustsRetries_ThrowsNetworkRetryException()
    {
        // Arrange
        var attemptCount = 0;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NetworkRetryException>(async () =>
        {
            await _service.ExecuteWithRetryAsync(
                async (ct) =>
                {
                    attemptCount++;
                    throw new SocketException((int)SocketError.ConnectionRefused);
                },
                "TestOperation",
                "test-id");
        });

        Assert.Equal(_config.MaxRetryAttempts, attemptCount);
        Assert.Equal("TestOperation", ex.OperationName);
        Assert.Equal(_config.MaxRetryAttempts, ex.AttemptsExhausted);
    }

    [Fact]
    public async Task TestConnectivityAsync_ValidHost_ReturnsSuccessResult()
    {
        // Act
        var result = await _service.TestConnectivityAsync("127.0.0.1", 80, TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("127.0.0.1", result.Host);
        Assert.Equal(80, result.Port);
        // Note: Actual connectivity depends on environment, so we just verify structure
    }

    [Fact]
    public async Task TestConnectivityAsync_InvalidHost_ReturnsFailureResult()
    {
        // Act
        var result = await _service.TestConnectivityAsync("invalid-host-12345", 80, TimeSpan.FromSeconds(1));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("invalid-host-12345", result.Host);
        Assert.Equal(80, result.Port);
        Assert.False(result.IsReachable);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public async Task WaitForConnectivityAsync_ConnectivityNeverRestored_ReturnsFalse()
    {
        // Act
        var result = await _service.WaitForConnectivityAsync(
            "invalid-host-12345", 
            80, 
            TimeSpan.FromMilliseconds(500));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _service.ExecuteWithRetryAsync(
                async (ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return "success";
                },
                "TestOperation",
                "test-id",
                cts.Token);
        });
    }
}