namespace TimeSheetAppWeb.Exceptions
{
    public class EntityNotFoundException : Exception
    {
        public EntityNotFoundException() : base("Entity not found.")
        {
        }
        public EntityNotFoundException(string entity) : base($"{entity} is empty")
        {
        }
    }
}
