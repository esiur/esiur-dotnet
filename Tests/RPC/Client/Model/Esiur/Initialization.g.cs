using Esiur.Resource;
using System;
namespace Esiur.Tests.RPC.EsiurServer
{
    public static class Initialization
    {
        public static Type[] Resources { get; } = new Type[] { typeof(Esiur.Tests.RPC.EsiurServer.Service), typeof(Esiur.Tests.RPC.EsiurServer.TestObject) };
        public static Type[] Records { get; } = new Type[] { typeof(Esiur.Tests.RPC.EsiurServer.BusinessDocument), typeof(Esiur.Tests.RPC.EsiurServer.Attachment), typeof(Esiur.Tests.RPC.EsiurServer.Party), typeof(Esiur.Tests.RPC.EsiurServer.Address), typeof(Esiur.Tests.RPC.EsiurServer.DocumentHeader), typeof(Esiur.Tests.RPC.EsiurServer.LineItem), typeof(Esiur.Tests.RPC.EsiurServer.Variant), typeof(Esiur.Tests.RPC.EsiurServer.Payment) };
        public static Type[] Enums { get; } = new Type[] { typeof(Esiur.Tests.RPC.EsiurServer.Currency), typeof(Esiur.Tests.RPC.EsiurServer.DocType), typeof(Esiur.Tests.RPC.EsiurServer.Kind), typeof(Esiur.Tests.RPC.EsiurServer.LineType), typeof(Esiur.Tests.RPC.EsiurServer.PaymentMethod) };


        public static void RegisterTypes(Warehouse warehouse)
        {
            foreach(var type in Resources)
                warehouse.RegisterProxyType(type);

            foreach(var type in Records)
                warehouse.RegisterProxyType(type);

            foreach(var type in Enums)
                warehouse.RegisterProxyType(type);
        }
    }
}