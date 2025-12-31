using FluentAssertions;
using MessagePack;
using Orleans.Persistence.Couchbase.Infrastructure;
using Xunit;

namespace Orleans.Persistence.Couchbase.UnitTests;

public class OrleansCouchbaseTranscoderTests
{
    private readonly OrleansCouchbaseTranscoder _transcoder = new();

    [Fact]
    public void Encode_ShouldProduceBinaryWithHeader()
    {
        // Arrange
        var state = new MessagePackTestState { Name = "Test", Value = 42, IsActive = true };
        using var stream = new MemoryStream();

        // Act
        _transcoder.Encode(stream, state, default, default);

        // Assert
        var data = stream.ToArray();
        data.Length.Should().BeGreaterThan(4);

        // Verify header
        data[0].Should().Be(2); // Version
        data[1].Should().Be(1); // Format (MessagePack)
        data[2].Should().Be(0); // Reserved
        data[3].Should().Be(0); // Reserved
    }

    [Fact]
    public void Decode_ShouldRestoreState()
    {
        // Arrange
        var original = new MessagePackTestState { Name = "Test", Value = 42, IsActive = true };
        using var stream = new MemoryStream();
        _transcoder.Encode(stream, original, default, default);
        var data = stream.ToArray();

        // Act
        var restored = _transcoder.Decode<MessagePackTestState>(data, default, default);

        // Assert
        restored.Should().NotBeNull();
        restored!.Name.Should().Be(original.Name);
        restored.Value.Should().Be(original.Value);
        restored.IsActive.Should().Be(original.IsActive);
    }

    [Fact]
    public void Decode_WithEmptyBuffer_ShouldReturnDefault()
    {
        // Act
        var result = _transcoder.Decode<MessagePackTestState>(ReadOnlyMemory<byte>.Empty, default, default);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Decode_WithInvalidHeader_ShouldThrow()
    {
        // Arrange - buffer too short
        var shortBuffer = new byte[] { 0x02, 0x01 };

        // Act & Assert
        Assert.Throws<InvalidDataException>(() =>
            _transcoder.Decode<MessagePackTestState>(shortBuffer, default, default));
    }

    [Fact]
    public void Decode_WithUnsupportedVersion_ShouldThrow()
    {
        // Arrange - version 99 is not supported
        var invalidVersionBuffer = new byte[] { 99, 1, 0, 0, 0x92, 0xa4 }; // Invalid version

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(() =>
            _transcoder.Decode<MessagePackTestState>(invalidVersionBuffer, default, default));

        ex.Message.Should().Contain("Unsupported document version");
    }

    [Fact]
    public void Decode_WithUnsupportedFormat_ShouldThrow()
    {
        // Arrange - format 99 is not supported
        var invalidFormatBuffer = new byte[] { 2, 99, 0, 0, 0x92, 0xa4 }; // Invalid format

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(() =>
            _transcoder.Decode<MessagePackTestState>(invalidFormatBuffer, default, default));

        ex.Message.Should().Contain("Unsupported serialization format");
    }

    [Fact]
    public void RoundTrip_WithNullValues_ShouldWork()
    {
        // Arrange
        var state = new MessagePackStateWithNullable { Name = null, OptionalValue = null };
        using var stream = new MemoryStream();

        // Act
        _transcoder.Encode(stream, state, default, default);
        var data = stream.ToArray();
        var restored = _transcoder.Decode<MessagePackStateWithNullable>(data, default, default);

        // Assert
        restored.Should().NotBeNull();
        restored!.Name.Should().BeNull();
        restored.OptionalValue.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_WithComplexObject_ShouldWork()
    {
        // Arrange
        var state = new MessagePackComplexState
        {
            Items = ["a", "b", "c"],
            Nested = new MessagePackTestState { Name = "Nested", Value = 100, IsActive = false },
            Timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };
        using var stream = new MemoryStream();

        // Act
        _transcoder.Encode(stream, state, default, default);
        var data = stream.ToArray();
        var restored = _transcoder.Decode<MessagePackComplexState>(data, default, default);

        // Assert
        restored.Should().NotBeNull();
        restored!.Items.Should().BeEquivalentTo(state.Items);
        restored.Nested.Name.Should().Be("Nested");
        restored.Nested.Value.Should().Be(100);
        restored.Timestamp.Should().Be(state.Timestamp);
    }

    [Fact]
    public void GetFormat_ShouldReturnBinaryFormat()
    {
        // Arrange
        var state = new MessagePackTestState { Name = "Test", Value = 1, IsActive = true };

        // Act
        var flags = _transcoder.GetFormat(state);

        // Assert
        flags.DataFormat.Should().Be(Couchbase.Core.IO.Operations.DataFormat.Binary);
    }

    [Fact]
    public void Encode_WithLargeObject_ShouldExpandBuffer()
    {
        // Arrange - create object larger than default 1KB buffer
        var largeState = new MessagePackTestState
        {
            Name = new string('x', 2000), // 2KB string
            Value = 123,
            IsActive = true
        };
        using var stream = new MemoryStream();

        // Act - should not throw
        _transcoder.Encode(stream, largeState, default, default);

        // Assert
        var data = stream.ToArray();
        data.Length.Should().BeGreaterThan(2000);

        // Verify it can be decoded
        var restored = _transcoder.Decode<MessagePackTestState>(data, default, default);
        restored.Should().NotBeNull();
        restored!.Name.Should().Be(largeState.Name);
    }
}

// Test models for MessagePack serializer
[MessagePackObject]
public class MessagePackTestState
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public int Value { get; set; }

    [Key(2)]
    public bool IsActive { get; set; }
}

[MessagePackObject]
public class MessagePackStateWithNullable
{
    [Key(0)]
    public string? Name { get; set; }

    [Key(1)]
    public int? OptionalValue { get; set; }
}

[MessagePackObject]
public class MessagePackComplexState
{
    [Key(0)]
    public string[] Items { get; set; } = [];

    [Key(1)]
    public MessagePackTestState Nested { get; set; } = new();

    [Key(2)]
    public DateTime Timestamp { get; set; }
}
