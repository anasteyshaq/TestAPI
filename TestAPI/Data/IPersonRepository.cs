using TestAPI.Models;

namespace TestAPI.Data
{
    public interface IPersonRepository
    {
        List<Person> GetPeopleByRequest(GetAllRequest request); 
        void InsertOrUpdate(Person person);

    }
}
