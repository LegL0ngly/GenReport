using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
using CoreDdd.Domain.Events;
using GenReport.DB.Domain.Events;
using GenReport.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenReport.Domain.Entities.Onboarding
{
    [Table("users")]
    public class User : BaseEntity, IDeletable
    {
        [NotMapped]
        private PasswordHasher<object> _passwordHasher;

        #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute]
        private User() { }

        [System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute]
        public User(string password, string email, string firstName, string lastName, string? middleName, string profileURL)
        {
            Email = email;
            FirstName = firstName;
            LastName = lastName;
            MiddleName = middleName;
            ProfileURL = profileURL;
            Password = passwordHasher.HashPassword(null,password);
        }

        #region Columns

        [NotMapped]
        private PasswordHasher<object> passwordHasher
        {
            get
            {
                return _passwordHasher ??= new PasswordHasher<object>();
            }
        }

        [Column("password")]
        public string Password { get; private set; }

        public bool MatchPassword(string unhashedPassword)
        {
            var result = passwordHasher.VerifyHashedPassword(null,Password, unhashedPassword);
            return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
        }

        public void UpdatePassword(string newPassword)
        {
            Password = passwordHasher.HashPassword(this, newPassword);
        }

        [EmailAddress]
        [Column("email")]
        public required string Email { get; set; }

        [Column("first_name")]
        public required string FirstName { get; set; }

        [Column("last_name")]
        public required string LastName { get; set; }

        [Column("middle_name")]
        public string? MiddleName { get; set; }
        [Column("profile_url")]
        public string? ProfileURL { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("role_id")]
        public int RoleId { get; set; }

        #endregion

        public void ForgotPassword()
        {
            DomainEvents.RaiseEvent(new ForgotPasswordEvent { Id = Id });
        }
    }
}
