using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Event, AllowMultiple = true)]
public class AnnotationAttribute : Attribute
{

    public readonly string? Key;
    public readonly string Value;

    public AnnotationAttribute(string annotation)
    {
        Key = null;
        Value = annotation;
    }
    public AnnotationAttribute(string key, string value)
    {
        Key = key;
        Value = value;
    }

    //public AnnotationAttribute(params string[] annotations)
    //{
    //    this.Annotation = String.Join("\n", annotations);
    //}
}
