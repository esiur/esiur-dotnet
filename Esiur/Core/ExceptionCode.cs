using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Core
{
    public enum ExceptionCode : ushort
    {
        HostNotReachable,
        AccessDenied,
        ResourceNotFound,
        AttachDenied,
        InvalidMethod,
        InvokeDenied,
        CreateDenied,
        AddParentDenied,
        AddChildDenied,
        ViewAttributeDenied,
        UpdateAttributeDenied,
        StoreNotFound,
        ParentNotFound,
        ChildNotFound,
        ResourceIsNotStore,
        DeleteDenied,
        DeleteFailed,
        UpdateAttributeFailed,
        GetAttributesFailed,
        ClearAttributesFailed,
        TemplateNotFound,
        RenameDenied,
        ClassNotFound,
        MethodNotFound,
        PropertyNotFound,
        SetPropertyDenied,
        ReadOnlyProperty
    }
}
