using Esiur.Core;
using Esiur.Resource;
using Microsoft.AspNetCore.Mvc;

namespace Esiur.AspNetCore.Example
{
    [ApiController]
    [Route("[controller]")]
    public class MainController : ControllerBase
    {
   
        private readonly ILogger<MainController> _logger;

        public MainController(ILogger<MainController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "Get")]
        public async AsyncReply<MyResource> Get()
        {
            return await Warehouse.Default.Get<MyResource>("/sys/demo");
        }
    }
}
