using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Annotations
{
    public sealed class ModelConfig
    {
        public string Name { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public ApiKeyCredential ApiKey { get; set; } = default!;
        public string ModelName { get; set; } = "";
    }
}
