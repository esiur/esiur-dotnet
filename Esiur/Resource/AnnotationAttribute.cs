using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Event)]
public class AnnotationAttribute : Attribute
{

    public string Annotation { get; set; }
    public AnnotationAttribute(string annotation)
    {
        this.Annotation = annotation;
    }
    public AnnotationAttribute(params string[] annotations)
    {
        this.Annotation = String.Join("\n", annotations);
    }
}
