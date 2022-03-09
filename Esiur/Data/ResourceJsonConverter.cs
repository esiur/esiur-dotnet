
/*
 
Copyright (c) 2017-2021 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Esiur.Net.IIP;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esiur.Data;

class ResourceJsonConverter : JsonConverter<IResource>
{
    public override IResource Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return (IResource)JsonSerializer.Deserialize(ref reader, typeof(IResource), options);
    }


    public override void Write(
        Utf8JsonWriter writer,
        IResource resource,
        JsonSerializerOptions options)
    {

        writer.WriteStartObject();

        foreach (var pt in resource.Instance.Template.Properties)
        {
            var rt = pt.PropertyInfo.GetValue(resource, null);
            if (rt != null && rt.GetType().IsGenericType)
                continue;

            writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(pt.Name) ?? pt.Name);

            if (rt is IResource)
                JsonSerializer.Serialize(writer, (IResource)rt, options);
            else
                JsonSerializer.Serialize(writer, rt, options);
        }

        writer.WriteEndObject();

    }
}


public class DoubleJsonConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() == "NaN")
        {
            return double.NaN;
        }

        return reader.GetDouble(); // JsonException thrown if reader.TokenType != JsonTokenType.Number
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        if (double.IsNaN(value))
        {
            writer.WriteStringValue("NaN");
        }
        else
        {
            writer.WriteNumberValue(value);
        }
    }
}

