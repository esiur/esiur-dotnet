﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Membership
{
    public enum AuthorizationResultsResponse
    {
        Success,
        Failed,
        Expired,
        ServiceUnavailable,
        IAuthPlain,
        IAuthHashed,
        IAuthEncrypted
    }
}
