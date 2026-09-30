using Doorbell.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Domain.Entities
{
    public sealed class User
    {
        public Guid Id { get; private set; }

        public string Name { get; private set; } = null!;

        private User()
        {
        }

        public User(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("User name is required.");

            Id = Guid.NewGuid();
            Name = name;
        }
    }
}
