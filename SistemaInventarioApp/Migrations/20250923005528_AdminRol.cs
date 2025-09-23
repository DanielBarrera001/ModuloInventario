using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaInventarioApp.Migrations
{
    /// <inheritdoc />
    public partial class AdminRol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (Select Id from AspNetRoles WHERE Id = '8b07874a-6426-4e6f-b4d7-3a442f22c65f')
                BEGIN
                INSERT AspNetRoles (Id, [Name], [NormalizedName])
                VALUES ('8b07874a-6426-4e6f-b4d7-3a442f22c65f', 'admin','ADMIN')
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE AspNetRoles WHERE Id='8b07874a-6426-4e6f-b4d7-3a442f22c65f'");
        }
    }
}
