using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Esiur.Resource.Template;

#nullable enable

public class MemberData
{
    public MemberInfo Info;
    public string Name;
    public int Order;
    //public bool Inherited;
    public MemberData? Parent;
    public MemberData? Child;
    public byte Index;

    public PropertyPermission PropertyPermission;

    //public ExportAttribute ExportAttribute;


    //public string Name => ExportAttribute?.Name ?? Info.Name;

    public MemberData(MemberInfo info, int order)
    {
        var exportAttr = info.GetCustomAttribute<ExportAttribute>();

        if (info is PropertyInfo pi)
        {
            if (exportAttr != null && exportAttr.Permission.HasValue)
            {
                if ((exportAttr.Permission == PropertyPermission.Write
                    || exportAttr.Permission == PropertyPermission.ReadWrite) && !pi.CanWrite)
                {
                    throw new Exception($"Property '{pi.Name}' does not have a setter, but ExportAttribute specifies it as writable.");
                }

                if ((exportAttr.Permission == PropertyPermission.Read
                    || exportAttr.Permission == PropertyPermission.ReadWrite) && !pi.CanRead)
                {
                    throw new Exception($"Property '{pi.Name}' does not have a getter, but ExportAttribute specifies it as readable.");
                }

                this.PropertyPermission = exportAttr.Permission.Value;
            }
            else
            {
                this.PropertyPermission = (pi.CanRead && pi.CanWrite) ? PropertyPermission.ReadWrite 
                                                                      : pi.CanWrite ? PropertyPermission.Write 
                                                                                    : PropertyPermission.Read;
            }
        }

        this.Name = exportAttr?.Name ?? info.Name;
        this.Info = info;
        this.Order = order;
    }

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

    //public string? GetAnnotation()
    //{
    //    string? rt = null;
    //    var md = this;
    //    while (md != null)
    //    {
    //        var annotationAttr = md.Info.GetCustomAttribute<AnnotationAttribute>();
    //        if (annotationAttr != null)
    //            rt = annotationAttr.Annotation;
    //        md = md.Child;
    //    }

    //    return rt;
    //}
}

