namespace TestAPI.Models
{
    public class Person
    {
        public long Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public long? AddressId { get; set; }
        public virtual Address? Address { get; set; }
    }
}
