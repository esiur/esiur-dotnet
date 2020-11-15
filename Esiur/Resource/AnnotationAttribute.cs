using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Event)]
    public class AnnotationAttribute : Attribute
    {

        public string Annotation { get; set; }
        public AnnotationAttribute(string annotation)
        {
            this.Annotation = annotation;
        }
    }
}
