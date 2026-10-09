using Doorbell.Domain.Exceptions;
using System;

namespace Doorbell.Domain.Entities
{
    public sealed class User
    {
        public Guid Id { get; private set; }

        public string Name { get; private set; } = null!;

        public string? SubjectId { get; private set; }

        public string? Email { get; private set; }

        public string? Username { get; private set; }

        private User()
        {
        }

        public User(
            string name,
            string? subjectId = null,
            string? email = null,
            string? username = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("User name is required.");

            if (subjectId is not null && string.IsNullOrWhiteSpace(subjectId))
                throw new DomainException("User subject id cannot be blank.");

            if (email is not null && string.IsNullOrWhiteSpace(email))
                throw new DomainException("User email cannot be blank.");

            if (username is not null && string.IsNullOrWhiteSpace(username))
                throw new DomainException("User username cannot be blank.");

            Id = Guid.NewGuid();
            Name = name;
            SubjectId = subjectId;
            Email = email;
            Username = username;
        }
    }
}