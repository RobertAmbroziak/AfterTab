using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterTab.DAL.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialNumericKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "EntityFrameworkHiLoSequence",
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayNameConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Users_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountActions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Challenge = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    State = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReturnOrigin = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BrowserHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountActions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountActions_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountActions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SettlementVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Events_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Events_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GuestDevices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestDevices_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestDevices_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestDevices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserIdentities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    EmailVerifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIdentities", x => x.Id);
                    table.CheckConstraint("CK_UserIdentities_Login", "(\"Provider\" = 'Local' AND \"NormalizedEmail\" IS NOT NULL) OR (\"Provider\" IN ('Google', 'Facebook') AND \"ProviderSubject\" IS NOT NULL AND \"PasswordHash\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_UserIdentities_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserIdentities_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserIdentities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EventInvites",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventInvites_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventInvites_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventInvites_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EventParticipants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MergedIntoParticipantId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeftAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventParticipants", x => x.Id);
                    table.UniqueConstraint("AK_EventParticipants_Id_EventId", x => new { x.Id, x.EventId });
                    table.ForeignKey(
                        name: "FK_EventParticipants_EventParticipants_MergedIntoParticipantId~",
                        columns: x => new { x.MergedIntoParticipantId, x.EventId },
                        principalTable: "EventParticipants",
                        principalColumns: new[] { "Id", "EventId" });
                    table.ForeignKey(
                        name: "FK_EventParticipants_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventParticipants_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventParticipants_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    GuestDeviceId = table.Column<long>(type: "bigint", nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeviceLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_GuestDevices_GuestDeviceId",
                        column: x => x.GuestDeviceId,
                        principalTable: "GuestDevices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sessions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sessions_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    OthersAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Balanced = table.Column<bool>(type: "boolean", nullable: false),
                    BalanceParticipantId = table.Column<long>(type: "bigint", nullable: true),
                    BalanceAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    PaidByParticipantId = table.Column<long>(type: "bigint", nullable: false),
                    SplitMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.UniqueConstraint("AK_Expenses_Id_EventId", x => new { x.Id, x.EventId });
                    table.CheckConstraint("CK_Expenses_Amount", "\"AmountMinor\" > 0");
                    table.CheckConstraint("CK_Expenses_Balance", "\"BalanceAmountMinor\" >= 0 AND \"BalanceAmountMinor\" <= \"AmountMinor\" AND ((\"BalanceParticipantId\" IS NULL AND \"BalanceAmountMinor\" = 0) OR (\"BalanceParticipantId\" IS NOT NULL AND \"BalanceAmountMinor\" > 0 AND \"Balanced\" = TRUE AND \"OthersAmountMinor\" = 0))");
                    table.CheckConstraint("CK_Expenses_Others", "\"OthersAmountMinor\" >= 0 AND \"OthersAmountMinor\" <= \"AmountMinor\"");
                    table.ForeignKey(
                        name: "FK_Expenses_EventParticipants_BalanceParticipantId_EventId",
                        columns: x => new { x.BalanceParticipantId, x.EventId },
                        principalTable: "EventParticipants",
                        principalColumns: new[] { "Id", "EventId" });
                    table.ForeignKey(
                        name: "FK_Expenses_EventParticipants_PaidByParticipantId_EventId",
                        columns: x => new { x.PaidByParticipantId, x.EventId },
                        principalTable: "EventParticipants",
                        principalColumns: new[] { "Id", "EventId" });
                    table.ForeignKey(
                        name: "FK_Expenses_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Expenses_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Expenses_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Expenses_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TransferConfirmations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    SettlementVersion = table.Column<int>(type: "integer", nullable: false),
                    FromParticipantId = table.Column<long>(type: "bigint", nullable: false),
                    ToParticipantId = table.Column<long>(type: "bigint", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferConfirmations", x => x.Id);
                    table.CheckConstraint("CK_TransferConfirmations_Valid", "\"AmountMinor\" > 0 AND \"SettlementVersion\" > 0 AND \"FromParticipantId\" <> \"ToParticipantId\"");
                    table.ForeignKey(
                        name: "FK_TransferConfirmations_EventParticipants_FromParticipantId_E~",
                        columns: x => new { x.FromParticipantId, x.EventId },
                        principalTable: "EventParticipants",
                        principalColumns: new[] { "Id", "EventId" });
                    table.ForeignKey(
                        name: "FK_TransferConfirmations_EventParticipants_ToParticipantId_Eve~",
                        columns: x => new { x.ToParticipantId, x.EventId },
                        principalTable: "EventParticipants",
                        principalColumns: new[] { "Id", "EventId" });
                    table.ForeignKey(
                        name: "FK_TransferConfirmations_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferConfirmations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransferConfirmations_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GuestLoginRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Challenge = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReturnOrigin = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProposedDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LegacyUserId = table.Column<long>(type: "bigint", nullable: true),
                    LegacySessionId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    GuestDeviceId = table.Column<long>(type: "bigint", nullable: true),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestLoginRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_GuestDevices_GuestDeviceId",
                        column: x => x.GuestDeviceId,
                        principalTable: "GuestDevices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_Sessions_LegacySessionId",
                        column: x => x.LegacySessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_Users_LegacyUserId",
                        column: x => x.LegacyUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestLoginRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExpenseSplits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ExpenseId = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    ParticipantId = table.Column<long>(type: "bigint", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    PercentageBps = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseSplits", x => x.Id);
                    table.CheckConstraint("CK_ExpenseSplits_Amount", "\"AmountMinor\" >= 0");
                    table.CheckConstraint("CK_ExpenseSplits_Percentage", "\"PercentageBps\" IS NULL OR (\"PercentageBps\" > 0 AND \"PercentageBps\" <= 10000)");
                    table.ForeignKey(
                        name: "FK_ExpenseSplits_EventParticipants_ParticipantId_EventId",
                        columns: x => new { x.ParticipantId, x.EventId },
                        principalTable: "EventParticipants",
                        principalColumns: new[] { "Id", "EventId" });
                    table.ForeignKey(
                        name: "FK_ExpenseSplits_Expenses_ExpenseId_EventId",
                        columns: x => new { x.ExpenseId, x.EventId },
                        principalTable: "Expenses",
                        principalColumns: new[] { "Id", "EventId" });
                    table.ForeignKey(
                        name: "FK_ExpenseSplits_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExpenseSplits_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountActions_CreatedByUserId",
                table: "AccountActions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountActions_ModifiedByUserId",
                table: "AccountActions",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountActions_TokenHash",
                table: "AccountActions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountActions_UserId",
                table: "AccountActions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventInvites_CreatedByUserId",
                table: "EventInvites",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventInvites_EventId",
                table: "EventInvites",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventInvites_ModifiedByUserId",
                table: "EventInvites",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventInvites_TokenHash",
                table: "EventInvites",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_CreatedByUserId",
                table: "EventParticipants",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_EventId_UserId",
                table: "EventParticipants",
                columns: new[] { "EventId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_MergedIntoParticipantId_EventId",
                table: "EventParticipants",
                columns: new[] { "MergedIntoParticipantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_ModifiedByUserId",
                table: "EventParticipants",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_PublicId",
                table: "EventParticipants",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_UserId",
                table: "EventParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedAtUtc_Id",
                table: "Events",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedByUserId",
                table: "Events",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ModifiedByUserId",
                table: "Events",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_OwnerUserId",
                table: "Events",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_PublicId",
                table: "Events",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_AuthorUserId",
                table: "Expenses",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BalanceParticipantId_EventId",
                table: "Expenses",
                columns: new[] { "BalanceParticipantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreatedByUserId",
                table: "Expenses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_EventId",
                table: "Expenses",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ModifiedByUserId",
                table: "Expenses",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_PaidByParticipantId_EventId",
                table: "Expenses",
                columns: new[] { "PaidByParticipantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_PublicId",
                table: "Expenses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSplits_CreatedByUserId",
                table: "ExpenseSplits",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSplits_ExpenseId_EventId",
                table: "ExpenseSplits",
                columns: new[] { "ExpenseId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSplits_ExpenseId_ParticipantId",
                table: "ExpenseSplits",
                columns: new[] { "ExpenseId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSplits_ModifiedByUserId",
                table: "ExpenseSplits",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSplits_ParticipantId_EventId",
                table: "ExpenseSplits",
                columns: new[] { "ParticipantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestDevices_CreatedByUserId",
                table: "GuestDevices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestDevices_ModifiedByUserId",
                table: "GuestDevices",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestDevices_TokenHash",
                table: "GuestDevices",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestDevices_UserId",
                table: "GuestDevices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_CodeHash",
                table: "GuestLoginRequests",
                column: "CodeHash",
                unique: true,
                filter: "\"CodeHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_CreatedByUserId",
                table: "GuestLoginRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_ExpiresAtUtc",
                table: "GuestLoginRequests",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_GuestDeviceId",
                table: "GuestLoginRequests",
                column: "GuestDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_LegacySessionId",
                table: "GuestLoginRequests",
                column: "LegacySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_LegacyUserId",
                table: "GuestLoginRequests",
                column: "LegacyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_ModifiedByUserId",
                table: "GuestLoginRequests",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_RequestHash",
                table: "GuestLoginRequests",
                column: "RequestHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestLoginRequests_UserId",
                table: "GuestLoginRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CreatedByUserId",
                table: "Sessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_GuestDeviceId",
                table: "Sessions",
                column: "GuestDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ModifiedByUserId",
                table: "Sessions",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_PublicId",
                table: "Sessions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TokenHash",
                table: "Sessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId_ExpiresAtUtc",
                table: "Sessions",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_CreatedByUserId",
                table: "TransferConfirmations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_EventId_SettlementVersion_FromPartici~",
                table: "TransferConfirmations",
                columns: new[] { "EventId", "SettlementVersion", "FromParticipantId", "ToParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_FromParticipantId_EventId",
                table: "TransferConfirmations",
                columns: new[] { "FromParticipantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_ModifiedByUserId",
                table: "TransferConfirmations",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferConfirmations_ToParticipantId_EventId",
                table: "TransferConfirmations",
                columns: new[] { "ToParticipantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentities_CreatedByUserId",
                table: "UserIdentities",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentities_ModifiedByUserId",
                table: "UserIdentities",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentities_NormalizedEmail",
                table: "UserIdentities",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"Provider\" = 'Local' AND \"NormalizedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentities_Provider_ProviderSubject",
                table: "UserIdentities",
                columns: new[] { "Provider", "ProviderSubject" },
                unique: true,
                filter: "\"ProviderSubject\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentities_UserId",
                table: "UserIdentities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedByUserId",
                table: "Users",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ModifiedByUserId",
                table: "Users",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PublicId",
                table: "Users",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountActions");

            migrationBuilder.DropTable(
                name: "EventInvites");

            migrationBuilder.DropTable(
                name: "ExpenseSplits");

            migrationBuilder.DropTable(
                name: "GuestLoginRequests");

            migrationBuilder.DropTable(
                name: "TransferConfirmations");

            migrationBuilder.DropTable(
                name: "UserIdentities");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "EventParticipants");

            migrationBuilder.DropTable(
                name: "GuestDevices");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropSequence(
                name: "EntityFrameworkHiLoSequence");
        }
    }
}
