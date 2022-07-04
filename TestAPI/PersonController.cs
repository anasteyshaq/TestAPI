using Microsoft.AspNetCore.Mvc;
using TestAPI.Domain.Interfaces;
using TestAPI.Models;

namespace TestAPI
{
    [Route("api/[controller]")]
    [ApiController]
    public class PersonController : ControllerBase
    {
        private string json = @"{
                firstName: 'Ivan',
                lastName: 'Petrov',
                address: {
                        city: 'Kiev',
                        addressLine: prospect ""Peremogy"" 28/7
            }}";
        IPersonService _personService;
        public PersonController(IPersonService personService)
        {
            _personService = personService;
        }

        [HttpPost]
        public async Task<ActionResult> Index()
        {
            await _personService.Save(json);
            return Ok();
        }
        [HttpGet]
        public async Task<ActionResult> Get()
        {
            var req = @"{
                ""city"": ""kiev""
}";
            GetAllRequest request = JsonSerializer.DeserializeObject<GetAllRequest>(req);
            var s = await _personService.GetAll(request);
            await _personService.Save(s);
            var str = @"{}";
            GetAllRequest request1 = JsonSerializer.DeserializeObject<GetAllRequest>(str);
            var s1 = await _personService.GetAll(request1);
            Console.WriteLine(s1);
            return  Ok();
        }

    }
}
