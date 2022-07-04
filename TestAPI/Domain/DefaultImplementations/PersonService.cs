using TestAPI.Domain.Interfaces;
using TestAPI.Models;

namespace TestAPI.Domain.DefaultImplementations
{
    public class PersonService : IPersonService
    {
        IPersonRepository _personRepo;
        public PersonService(IPersonRepository personRepo)
        {
            _personRepo = personRepo;
        }
        public Task<string> GetAll(GetAllRequest request)
        {
            string text = "";
            Person person = new Person();
            List<Person> p = _personRepo.GetPeopleByRequest(request);
            if (p.Count == 1)
            {
                foreach (Person p2 in p)
                    text = new JsonSerializer().Serialize(p2);
            }
            else if (p.Count > 1)
            {
                text = new JsonSerializer().Serialize(p);
            }
            return Task.FromResult(text);
        }
        public Task<long> Save(string json)
        {
            var person = new JsonSerializer().Deserialize<Person>(json);
            _personRepo.InsertOrUpdate(person);
            return Task.FromResult(person.Id);
        }

    }
}
