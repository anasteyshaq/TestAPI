using TestAPI.Models;

namespace TestAPI.Domain.Interfaces
{
    public interface IPersonService
    {
        public Task<long> Save(string json);
        public Task<string> GetAll(GetAllRequest request);
    }
}
