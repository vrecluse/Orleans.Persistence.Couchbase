using System;
using System.IO;
using System.Text;
using System.Text.Json;
using CommunityToolkit.HighPerformance.Buffers;
using FluentAssertions;
using MessagePack;
using Orleans.Persistence.Couchbase.Infrastructure;
using Orleans.Persistence.Couchbase.Serialization;
using Xunit;

namespace Orleans.Persistence.Couchbase.UnitTests;

public class MessagePackCouchbaseSerializerTests
{
    private readonly MessagePackCouchbaseSerializer _serializer = new();

    [Fact]
    public void Format_ShouldReturnMessagePack()
    {
        _serializer.Format.Should().Be(CouchbaseDataFormat.MessagePack);
    }

    [Fact]
    public void Serialize_ShouldProduceBinaryWithHeader()
    {
        // Arrange
        var state = new MessagePackTestState { Name = "Test", Value = 42, IsActive = true };
        using var writer = new ArrayPoolBufferWriter<byte>(1024);

        // Act
        _serializer.Serialize(writer, state);

        // Assert
        var data = writer.WrittenSpan;
        data.Length.Should().BeGreaterThan(4);

        // Verify 4-byte header
        data[0].Should().Be(1); // Version
        data[1].Should().Be(1); // Format (MessagePack)
        data[2].Should().Be(0); // Reserved
        data[3].Should().Be(0); // Reserved
    }

    [Fact]
    public void RoundTrip_ShouldRestoreState()
    {
        // Arrange
        var original = new MessagePackTestState { Name = "Test", Value = 42, IsActive = true };
        using var writer = new ArrayPoolBufferWriter<byte>(1024);
        _serializer.Serialize(writer, original);
        var data = writer.WrittenMemory;

        // Act
        var restored = _serializer.Deserialize<MessagePackTestState>(data);

        // Assert
        restored.Name.Should().Be(original.Name);
        restored.Value.Should().Be(original.Value);
        restored.IsActive.Should().Be(original.IsActive);
    }

    [Fact]
    public void Deserialize_WithTooShortBuffer_ShouldThrow()
    {
        // Arrange
        var shortBuffer = new byte[] { 0x01, 0x01 };

        // Act & Assert
        Assert.Throws<InvalidDataException>(() =>
            _serializer.Deserialize<MessagePackTestState>(shortBuffer));
    }

    [Fact]
    public void Deserialize_WithUnsupportedVersion_ShouldThrow()
    {
        // Arrange - version 99 > 1
        var invalidVersionBuffer = new byte[] { 99, 1, 0, 0, 0x92, 0xa4 };

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(() =>
            _serializer.Deserialize<MessagePackTestState>(invalidVersionBuffer));

        ex.Message.Should().Contain("Unsupported MessagePack version");
    }
}

public class JsonCouchbaseSerializerTests
{
    private readonly JsonCouchbaseSerializer _serializer = new();

    [Fact]
    public void Format_ShouldReturnJson()
    {
        _serializer.Format.Should().Be(CouchbaseDataFormat.Json);
    }

    [Fact]
    public void Serialize_ShouldProducePureJson()
    {
        // Arrange
        var state = new JsonTestState { Name = "Test", Value = 42, IsActive = true };
        using var writer = new ArrayPoolBufferWriter<byte>(1024);

        // Act
        _serializer.Serialize(writer, state);

        // Assert
        var json = Encoding.UTF8.GetString(writer.WrittenSpan);

        // Should start with '{' - pure JSON, no binary header
        json[0].Should().Be('{');

        // Verify it's valid JSON
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("name").GetString().Should().Be("Test");
        parsed.RootElement.GetProperty("value").GetInt32().Should().Be(42);
        parsed.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_ShouldRestoreState()
    {
        // Arrange
        var original = new JsonTestState { Name = "Test", Value = 42, IsActive = true };
        using var writer = new ArrayPoolBufferWriter<byte>(1024);
        _serializer.Serialize(writer, original);
        var data = writer.WrittenMemory;

        // Act
        var restored = _serializer.Deserialize<JsonTestState>(data);

        // Assert
        restored.Name.Should().Be(original.Name);
        restored.Value.Should().Be(original.Value);
        restored.IsActive.Should().Be(original.IsActive);
    }

    [Fact]
    public void Serialize_WithNullValues_ShouldWork()
    {
        // Arrange
        var state = new JsonStateWithNullable { Name = null, OptionalValue = null };
        using var writer = new ArrayPoolBufferWriter<byte>(1024);

        // Act
        _serializer.Serialize(writer, state);
        var data = writer.WrittenMemory;
        var restored = _serializer.Deserialize<JsonStateWithNullable>(data);

        // Assert
        restored.Name.Should().BeNull();
        restored.OptionalValue.Should().BeNull();
    }
}

public class SmartCouchbaseTranscoderTests
{
    private readonly SmartCouchbaseTranscoder _transcoder;
    private readonly MessagePackCouchbaseSerializer _msgPackSerializer = new();
    private readonly JsonCouchbaseSerializer _jsonSerializer = new();

    public SmartCouchbaseTranscoderTests()
    {
        _transcoder = new SmartCouchbaseTranscoder(_msgPackSerializer, _jsonSerializer);
    }

    [Fact]
    public void Decode_WithJsonDocument_ShouldAutoDetectAndDeserialize()
    {
        // Arrange - pure JSON (starts with '{')
        var json = """{"name":"Test","value":42,"isActive":true}""";
        var data = Encoding.UTF8.GetBytes(json);

        // Act
        var result = _transcoder.Decode<JsonTestState>(data, default, default);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Decode_WithJsonArray_ShouldAutoDetectAndDeserialize()
    {
        // Arrange - JSON array (starts with '[')
        var json = """[{"name":"Item1"},{"name":"Item2"}]""";
        var data = Encoding.UTF8.GetBytes(json);

        // Act
        var result = _transcoder.Decode<JsonTestState[]>(data, default, default);

        // Assert
        result.Should().NotBeNull();
        result!.Length.Should().Be(2);
        result[0].Name.Should().Be("Item1");
    }

    [Fact]
    public void Decode_WithMessagePackDocument_ShouldAutoDetectAndDeserialize()
    {
        // Arrange - MessagePack with binary header
        using var writer = new ArrayPoolBufferWriter<byte>(1024);
        var original = new MessagePackTestState { Name = "Test", Value = 42, IsActive = true };
        _msgPackSerializer.Serialize(writer, original);
        var data = writer.WrittenMemory;

        // Act
        var result = _transcoder.Decode<MessagePackTestState>(data, default, default);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Decode_WithEmptyBuffer_ShouldReturnDefault()
    {
        // Act
        var result = _transcoder.Decode<JsonTestState>(ReadOnlyMemory<byte>.Empty, default, default);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Encode_ShouldThrowNotSupported()
    {
        // Arrange
        using var stream = new MemoryStream();
        var state = new JsonTestState();

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            _transcoder.Encode(stream, state, default, default));
    }

    [Fact]
    public void GetFormat_ShouldReturnBinary()
    {
        // Act
        var flags = _transcoder.GetFormat(new JsonTestState());

        // Assert
        flags.DataFormat.Should().Be(global::Couchbase.Core.IO.Operations.DataFormat.Binary);
    }
}

// Test models for MessagePack
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

// Test models for JSON (uses System.Text.Json naming policy)
public class JsonTestState
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public bool IsActive { get; set; }
}

public class JsonStateWithNullable
{
    public string? Name { get; set; }
    public int? OptionalValue { get; set; }
}
