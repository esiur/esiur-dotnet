using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.CLI
{
    internal class GetTemplateOptions
    {
        [Option('d', "dir", Required = false, HelpText = "Directory name where the generated models will be saved.")]
        public string Dir { get; set; }

        [Option('u', "username", Required = false, HelpText = "Authentication username.")]
        public string Username { get; set; }

        [Option('p', "password", Required = false, HelpText = "Authentication password.")]
        public string Password { get; set; }


        [Option('a', "async-setters", Required = false, HelpText = "Use asynchronous property setters.")]
        public bool AsyncSetters { get; set; }

    }
}
