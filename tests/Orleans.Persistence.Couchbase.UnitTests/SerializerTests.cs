using FluentAssertions;
using MessagePack;
using Orleans.Persistence.Couchbase.Serialization;
using System;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Orleans.Persistence.Couchbase.UnitTests;

public class JsonGrainStateSerializerTests
{
    private readonly JsonGrainStateSerializer _serializer = new();

    [Fact]
    public void ContentType_ShouldReturnApplicationJson()
    {
        _serializer.ContentType.Should().Contain("application/json");
    }

    [Fact]
    public void Serialize_ShouldProduceValidJson()
    {
        // Arrange
        var state = new TestState { Name = "Test", Value = 42, IsActive = true };

        // Act
        var data = _serializer.Serialize(state);

        // Assert
        data.Length.Should().BeGreaterThan(0);

        // Verify it's valid JSON (System.Text.Json uses camelCase by default)
        var json = Encoding.UTF8.GetString(data.Span);
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("name").GetString().Should().Be("Test");
        parsed.RootElement.GetProperty("value").GetInt32().Should().Be(42);
        parsed.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Deserialize_ShouldRestoreState()
    {
        // Arrange
        var original = new TestState { Name = "Test", Value = 42, IsActive = true };
        var data = _serializer.Serialize(original);

        // Act
        var restored = _serializer.Deserialize<TestState>(data);

        // Assert
        restored.Name.Should().Be(original.Name);
        restored.Value.Should().Be(original.Value);
        restored.IsActive.Should().Be(original.IsActive);
    }

    [Fact]
    public void SerializeAndDeserialize_WithNullValues_ShouldWork()
    {
        // Arrange
        var state = new TestStateWithNullable { Name = null, OptionalValue = null };

        // Act
        var data = _serializer.Serialize(state);
        var restored = _serializer.Deserialize<TestStateWithNullable>(data);

        // Assert
        restored.Name.Should().BeNull();
        restored.OptionalValue.Should().BeNull();
    }

    [Fact]
    public void SerializeAndDeserialize_WithComplexObject_ShouldWork()
    {
        // Arrange
        var state = new ComplexState
        {
            Items = new[] { "a", "b", "c" },
            Nested = new TestState { Name = "Nested", Value = 100, IsActive = false },
            Timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        // Act
        var data = _serializer.Serialize(state);
        var restored = _serializer.Deserialize<ComplexState>(data);

        // Assert
        restored.Items.Should().BeEquivalentTo(state.Items);
        restored.Nested.Name.Should().Be("Nested");
        restored.Nested.Value.Should().Be(100);
        restored.Timestamp.Should().Be(state.Timestamp);
    }
}

public class MessagePackGrainStateSerializerTests
{
    private readonly MessagePackGrainStateSerializer _serializer = new();

    [Fact]
    public void ContentType_ShouldReturnApplicationMsgpack()
    {
        _serializer.ContentType.Should().Be("application/x-msgpack");
    }

    [Fact]
    public void Serialize_ShouldProduceBinaryData()
    {
        // Arrange
        var state = new MessagePackTestState { Name = "Test", Value = 42, IsActive = true };

        // Act
        var data = _serializer.Serialize(state);

        // Assert
        data.Length.Should().BeGreaterThan(0);

        // Should NOT be valid UTF-8 text (it's binary)
        // MessagePack format starts with specific bytes
        data.Span[0].Should().NotBe((byte)'{'); // Not JSON
    }

    [Fact]
    public void Deserialize_ShouldRestoreState()
    {
        // Arrange
        var original = new MessagePackTestState { Name = "Test", Value = 42, IsActive = true };
        var data = _serializer.Serialize(original);

        // Act
        var restored = _serializer.Deserialize<MessagePackTestState>(data);

        // Assert
        restored.Name.Should().Be(original.Name);
        restored.Value.Should().Be(original.Value);
        restored.IsActive.Should().Be(original.IsActive);
    }

    [Fact]
    public void SerializeAndDeserialize_WithNullValues_ShouldWork()
    {
        // Arrange
        var state = new MessagePackStateWithNullable { Name = null, OptionalValue = null };

        // Act
        var data = _serializer.Serialize(state);
        var restored = _serializer.Deserialize<MessagePackStateWithNullable>(data);

        // Assert
        restored.Name.Should().BeNull();
        restored.OptionalValue.Should().BeNull();
    }

    [Fact]
    public void Serialize_ShouldBeSmallerThanJson()
    {
        // Arrange
        var state = new MessagePackTestState { Name = "TestWithLongerName", Value = 123456, IsActive = true };

        var jsonSerializer = new JsonGrainStateSerializer();
        var jsonData = jsonSerializer.Serialize(new TestState
        {
            Name = "TestWithLongerName",
            Value = 123456,
            IsActive = true
        });

        // Act
        var msgpackData = _serializer.Serialize(state);

        // Assert - MessagePack should typically be more compact
        msgpackData.Length.Should().BeLessThan(jsonData.Length);
    }
}

// Test models for JSON serializer
public class TestState
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public bool IsActive { get; set; }
}

public class TestStateWithNullable
{
    public string? Name { get; set; }
    public int? OptionalValue { get; set; }
}

public class ComplexState
{
    public string[] Items { get; set; } = Array.Empty<string>();
    public TestState Nested { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

// Test models for MessagePack serializer (need MessagePackObject attribute)
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
