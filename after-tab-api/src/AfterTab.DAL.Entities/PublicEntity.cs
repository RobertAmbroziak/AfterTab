namespace AfterTab.DAL.Entities;

// Database joins use Id; API clients only receive this independent identifier.
public abstract class PublicEntity : BaseEntity
{
    public Guid PublicId { get; set; } = Guid.NewGuid();
}
