using Esiur.Resource;

namespace Esiur.ASPNet
{
    [Resource]
    public partial class MyResource
    {
        [Export] int number;

        [Export]
        public string[] GetInfo() => new string[] { Environment.MachineName, Environment.UserName, Environment.CurrentDirectory,
            Environment.CommandLine, Environment.OSVersion.ToString(), Environment.ProcessPath };

    }
}
