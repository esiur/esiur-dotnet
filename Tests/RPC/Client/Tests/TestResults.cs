using System;
using System.Collections.Generic;
using System.Text;

namespace RPC.Client.Tests
{
    public class TestResults
    {
        public Dictionary<string, (long, long)> Docs { get; set; } = new();
        public Dictionary<string, (long, long)> Bytes { get; set; } = new();
        public Dictionary<string, (long, long)> Ints { get; set; } = new();

    }
}
