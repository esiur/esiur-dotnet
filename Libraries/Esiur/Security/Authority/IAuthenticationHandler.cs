using Esiur.Net.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public interface IAuthenticationHandler
    {

        public string Protocol { get; }
        //public AuthenticationMode Mode { get; }
        //public AuthenticationResult Initialize(object authData);

        public AuthenticationResult Process(object authData);

        //public AuthenticationResult? Result {  get; }
    }
}
