namespace GenReport.DB.Domain.Seed
{
    using GenReport.Domain.Entities.Onboarding;
    using Microsoft.EntityFrameworkCore;
    using System.Linq;

    /// <summary>
    /// Defines the <see cref="ApplicationDBContextSeeder" />
    /// </summary>
    public partial class ApplicationDBContextSeeder
    {
        /// <summary>
        /// The SeedUsers
        /// </summary>
        /// <returns>The <see cref="Task"/></returns>
        public async Task SeedUsers()
        {
            var adminUser = new User(
                password: "AdminPassword123", // User should change this
                    email: "admin@organization.com",
                firstName: "System",
                lastName: "Admin",
                middleName: "Middle",
                profileURL: "https://example.com/admin.png"
            );
            adminUser.RoleId = 2; 

            await applicationDbContext.Users.AddAsync(adminUser);
            await applicationDbContext.SaveChangesAsync();

        }
    }
}
