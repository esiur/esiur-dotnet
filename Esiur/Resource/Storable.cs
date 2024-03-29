﻿/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

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

using Esiur.Data;
using Esiur.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource;

[AttributeUsage(AttributeTargets.All)]
public class Storable : global::System.Attribute
{
    public delegate object SerializerFunction(object value);
    public delegate object DeserializerFunction(object data);

    SerializerFunction serializer;
    DeserializerFunction deserializer;
    RepresentationType dataType;

    public Storable()
    {
        //dataType =  = DataType.Void;
    }

    public DeserializerFunction Deserializer
    {
        get { return deserializer; }
    }

    public SerializerFunction Serializer
    {
        get { return serializer; }
    }

    public Storable(RepresentationType type)
    {
        this.dataType = type;
    }

    public Storable(RepresentationType type, SerializerFunction serializer, DeserializerFunction deserializer)
    {
        this.dataType = type;
        this.serializer = serializer;
        this.deserializer = deserializer;
    }
}
