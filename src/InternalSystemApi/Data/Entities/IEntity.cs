namespace BidOne.InternalSystemApi.Data.Entities;

public interface IEntity
{
    object? Id { get; }
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}

public interface IEntity<T> : IEntity
{
    new T Id { get; set; }
}