using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Esiur.Resource.Template;

public class MemberData
{
    public MemberInfo Info;
    public string Name;
    public int Order;
    //public bool Inherited;
    public MemberData? Parent;
    public MemberData? Child;
    public byte Index;

    public MemberInfo GetMemberInfo()
    {
        var rt = Info;
        var md = Child;
        while (md != null)
        {
            rt = Info;
            md = md.Child;
        }
        return rt;
    }

    public string? GetAnnotation()
    {
        string rt = null;
        var md = this;
        while (md != null)
        {
            var annotationAttr = md.Info.GetCustomAttribute<AnnotationAttribute>();
            if (annotationAttr != null)
                rt = annotationAttr.Annotation;
            md = md.Child;
        }

        return rt;
    }
}

