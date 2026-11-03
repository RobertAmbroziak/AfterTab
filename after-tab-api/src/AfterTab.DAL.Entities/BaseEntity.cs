namespace AfterTab.DAL.Entities;
public abstract class BaseEntity
{
    public long Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public long? CreatedByUserId { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public long? ModifiedByUserId { get; set; }
}
