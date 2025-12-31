using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Orleans.Persistence.Couchbase.Core;
using Orleans.Runtime;
using Orleans.Storage;
using Xunit;

namespace Orleans.Persistence.Couchbase.UnitTests;

public class CouchbaseGrainStorageTests
{
    private readonly Mock<ICouchbaseDataManager> _mockDataManager;
    private readonly Mock<ILogger<CouchbaseGrainStorage>> _mockLogger;
    private readonly CouchbaseGrainStorage _storage;

    public CouchbaseGrainStorageTests()
    {
        _mockDataManager = new Mock<ICouchbaseDataManager>();
        _mockLogger = new Mock<ILogger<CouchbaseGrainStorage>>();

        _storage = new CouchbaseGrainStorage(
            "TestStorage",
            _mockDataManager.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ReadStateAsync_ShouldDeserializeStateAndSetCas()
    {
        // Arrange
        var grainId = GrainId.Create("test-grain", "key1");
        var grainState = new GrainState<TestState> { State = new TestState() };
        var expectedState = new TestState { Value = "test" };
        var testCas = 12345UL;

        _mockDataManager
            .Setup(dm => dm.ReadAsync<TestState>("TestState", grainId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((expectedState, testCas));

        // Act
        await _storage.ReadStateAsync("TestState", grainId, grainState);

        // Assert
        grainState.State.Should().BeSameAs(expectedState);
        grainState.ETag.Should().Be(testCas.ToString());
        grainState.RecordExists.Should().BeTrue();
    }

    [Fact]
    public async Task ReadStateAsync_WhenDocumentNotFound_ShouldReturnDefaultState()
    {
        // Arrange
        var grainId = GrainId.Create("test-grain", "key1");
        var grainState = new GrainState<TestState> { State = new TestState() };

        _mockDataManager
            .Setup(dm => dm.ReadAsync<TestState>("TestState", grainId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((TestState?)null, 0UL));

        // Act
        await _storage.ReadStateAsync("TestState", grainId, grainState);

        // Assert
        grainState.ETag.Should().BeNull();
        grainState.RecordExists.Should().BeFalse();
    }

    [Fact]
    public async Task WriteStateAsync_ShouldWriteState()
    {
        // Arrange
        var grainId = GrainId.Create("test-grain", "key1");
        var testState = new TestState { Value = "test-value" };
        var grainState = new GrainState<TestState>
        {
            State = testState,
            ETag = "123"
        };

        var newCas = 456UL;
        _mockDataManager
            .Setup(dm => dm.WriteAsync(
                "TestState",
                grainId.ToString(),
                testState,
                123UL,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(newCas);

        // Act
        await _storage.WriteStateAsync("TestState", grainId, grainState);

        // Assert
        grainState.ETag.Should().Be(newCas.ToString());
        grainState.RecordExists.Should().BeTrue();

        _mockDataManager.Verify(
            dm => dm.WriteAsync(
                "TestState",
                grainId.ToString(),
                testState,
                123UL,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WriteStateAsync_WithNullETag_ShouldUseZeroCas()
    {
        // Arrange
        var grainId = GrainId.Create("test-grain", "key1");
        var testState = new TestState { Value = "test-value" };
        var grainState = new GrainState<TestState>
        {
            State = testState,
            ETag = null
        };

        var newCas = 789UL;
        _mockDataManager
            .Setup(dm => dm.WriteAsync(
                "TestState",
                grainId.ToString(),
                testState,
                0UL,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(newCas);

        // Act
        await _storage.WriteStateAsync("TestState", grainId, grainState);

        // Assert
        grainState.ETag.Should().Be(newCas.ToString());

        _mockDataManager.Verify(
            dm => dm.WriteAsync(
                "TestState",
                grainId.ToString(),
                testState,
                0UL,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClearStateAsync_ShouldCallDeleteWithCorrectCas()
    {
        // Arrange
        var grainId = GrainId.Create("test-grain", "key1");
        var grainState = new GrainState<TestState>
        {
            State = new TestState(),
            ETag = "999"
        };

        _mockDataManager
            .Setup(dm => dm.DeleteAsync(
                "TestState",
                grainId.ToString(),
                999UL,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _storage.ClearStateAsync("TestState", grainId, grainState);

        // Assert
        grainState.ETag.Should().BeNull();
        grainState.RecordExists.Should().BeFalse();

        _mockDataManager.Verify(
            dm => dm.DeleteAsync(
                "TestState",
                grainId.ToString(),
                999UL,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClearStateAsync_WithInconsistentStateException_ShouldThrow()
    {
        // Arrange
        var grainId = GrainId.Create("test-grain", "key1");
        var grainState = new GrainState<TestState>
        {
            State = new TestState(),
            ETag = "123"
        };

        _mockDataManager
            .Setup(dm => dm.DeleteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ulong>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InconsistentStateException("ETag mismatch", "123", "456"));

        // Act & Assert
        await Assert.ThrowsAsync<InconsistentStateException>(
            async () => await _storage.ClearStateAsync("TestState", grainId, grainState));
    }

    private class TestState
    {
        public string Value { get; set; } = string.Empty;
    }
}
