using TestAPI.Models;

namespace TestAPI.Data
{
    public class PersonRepository : IPersonRepository
    {
        private DataContext _context;
        public PersonRepository(DataContext context)
        {
            _context = context;
        }

        public List<Person> GetPeopleByRequest(GetAllRequest request)
        {
            var query = from person in _context.People
                        join address in _context.Addresses
                        on person.AddressId equals address.Id
                        where address.City == request.City 
                        || person.FirstName == request.FirstName
                        || person.LastName == request.LastName
                        select person;    
            return query.ToList();
        }

        public void InsertOrUpdate(Person person)
        {
            var query = _context.People.Where(x => x.Id == person.Id).FirstOrDefault();
            if (query == null)
                _context.People.Add(person);
            else
            {
                query.Address = person.Address;
                query.FirstName = person.FirstName;
                query.LastName = person.LastName;
            }
            _context.SaveChanges();
        }
    }
}
