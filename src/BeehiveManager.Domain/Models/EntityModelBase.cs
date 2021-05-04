using Etherna.DomainEvents;
using Etherna.MongODM.Core.Domain.Models;
using System;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Domain.Models
{
    public abstract class EntityModelBase : ModelBase, IEntityModel
    {
        private DateTime _creationDateTime;
        private readonly List<IDomainEvent> _events = new();

        // Constructors and dispose.
        protected EntityModelBase()
        {
            _creationDateTime = DateTime.Now;
        }

        public virtual void DisposeForDelete() { }

        // Properties.
        public virtual DateTime CreationDateTime { get => _creationDateTime; protected set => _creationDateTime = value; }
        public virtual IReadOnlyCollection<IDomainEvent> Events => _events;

        // Methods.
        public void AddEvent(IDomainEvent e) => _events.Add(e);

        public void ClearEvents() => _events.Clear();
    }

    public abstract class EntityModelBase<TKey> : EntityModelBase, IEntityModel<TKey>
    {
        // Properties.
        public virtual TKey Id { get; protected set; } = default!;

        // Methods.
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is null) return false;
            if (EqualityComparer<TKey>.Default.Equals(Id, default) ||
                !(obj is IEntityModel<TKey>) ||
                EqualityComparer<TKey>.Default.Equals((obj as IEntityModel<TKey>)!.Id, default)) return false;
            return GetType() == obj.GetType() &&
                EqualityComparer<TKey>.Default.Equals(Id, (obj as IEntityModel<TKey>)!.Id);
        }

        public override int GetHashCode()
        {
            if (EqualityComparer<TKey>.Default.Equals(Id, default))
                return -1;
            return Id!.GetHashCode();
        }
    }
}
