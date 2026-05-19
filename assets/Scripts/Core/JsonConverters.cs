using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;

/// <summary>
/// Custom System.Text.Json converter for UnityEngine.Vector2.
/// Serializes as {"x": 1.0, "y": 2.0} with camelCase property names.
/// </summary>
public class Vector2Converter : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        float x = 0f, y = 0f;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected StartObject, got {reader.TokenType}");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException($"Expected PropertyName, got {reader.TokenType}");

            string prop = reader.GetString();
            reader.Read();

            if (prop == "x")
                x = reader.GetSingle();
            else if (prop == "y")
                y = reader.GetSingle();
            else
                reader.Skip();
        }

        return new Vector2(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.x);
        writer.WriteNumber("y", value.y);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Custom System.Text.Json converter for UnityEngine.Color.
/// Serializes as {"r": 1.0, "g": 0.5, "b": 0.0, "a": 1.0} with camelCase property names.
/// </summary>
public class ColorConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        float r = 0f, g = 0f, b = 0f, a = 1f;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected StartObject, got {reader.TokenType}");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException($"Expected PropertyName, got {reader.TokenType}");

            string prop = reader.GetString();
            reader.Read();

            if (prop == "r")
                r = reader.GetSingle();
            else if (prop == "g")
                g = reader.GetSingle();
            else if (prop == "b")
                b = reader.GetSingle();
            else if (prop == "a")
                a = reader.GetSingle();
            else
                reader.Skip();
        }

        return new Color(r, g, b, a);
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("r", value.r);
        writer.WriteNumber("g", value.g);
        writer.WriteNumber("b", value.b);
        writer.WriteNumber("a", value.a);
        writer.WriteEndObject();
    }
}
