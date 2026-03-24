namespace TimeSheetAppWeb.Exceptions 
{
    public class UnableToCreateEntityException :Exception
    {
        public UnableToCreateEntityException() : base("Unable to create object") 
        {
            
        }
        public UnableToCreateEntityException(string entityName) : base($"Unable to create {entityName}.")
        {
        }

    }
}
