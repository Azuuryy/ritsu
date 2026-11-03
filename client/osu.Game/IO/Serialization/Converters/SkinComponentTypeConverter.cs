// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using Newtonsoft.Json;

namespace osu.Game.IO.Serialization.Converters
{
    public class SkinComponentTypeConverter : JsonConverter<Type>
    {
        public override Type? ReadJson(JsonReader reader, Type objectType, Type? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.String || reader.Value is not string typeName)
                throw new JsonSerializationException("Expected a skin component type name.");

            return Type.GetType(typeName, name => Assembly.Load(new AssemblyName(name.Name!)), null, throwOnError: true);
        }

        public override void WriteJson(JsonWriter writer, Type? value, JsonSerializer serializer)
        {
            if (value == null)
                writer.WriteNull();
            else
                writer.WriteValue($"{value.FullName}, {value.Assembly.GetName().Name}");
        }
    }
}
