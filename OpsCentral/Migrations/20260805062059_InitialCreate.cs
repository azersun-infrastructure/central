using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpsCentral.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdActionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    RequestedByUpnOrUsername = table.Column<string>(type: "text", nullable: false),
                    RequestedByAuthSource = table.Column<string>(type: "text", nullable: false),
                    DispatchTarget = table.Column<string>(type: "text", nullable: false),
                    ExternalJobId = table.Column<string>(type: "text", nullable: true),
                    ExternalJobUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ActionNote = table.Column<string>(type: "text", nullable: true),
                    RawResultPayload = table.Column<string>(type: "text", nullable: true),
                    ErrorDetail = table.Column<string>(type: "text", nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CallbackReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastPolledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TimeoutAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PollAttemptCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdActionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalAdminAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalAdminAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdActionEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdActionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    StatusAtEvent = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    RawPayload = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdActionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdActionEvents_AdActionRequests_AdActionRequestId",
                        column: x => x.AdActionRequestId,
                        principalTable: "AdActionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdActionEvents_AdActionRequestId",
                table: "AdActionEvents",
                column: "AdActionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AdActionRequests_RequestedAtUtc",
                table: "AdActionRequests",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdActionRequests_Status",
                table: "AdActionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LocalAdminAccounts_Username",
                table: "LocalAdminAccounts",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdActionEvents");

            migrationBuilder.DropTable(
                name: "LocalAdminAccounts");

            migrationBuilder.DropTable(
                name: "AdActionRequests");
        }
    }
}
