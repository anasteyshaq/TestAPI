using TestAPI.Models;

namespace TestAPI
{
    public interface IPersonRepository
    {
        public Task<int> Save(string json);
        public Task<string> GetAll(GetAllRequest request);
    }
}
