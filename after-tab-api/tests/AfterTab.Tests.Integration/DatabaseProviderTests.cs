using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.Entities.Expenses;
using AfterTab.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Integration;

[TestClass]
public sealed class DatabaseProviderTests
{
    private sealed class Actor : ICurrentUser { public long? UserId => null; }

    [TestMethod]
    [DataRow(null, DatabaseProvider.SqlServer)]
    [DataRow("SqlServer", DatabaseProvider.SqlServer)]
    [DataRow("postgresql", DatabaseProvider.PostgreSql)]
    public void Provider_Selection_Is_Explicit_And_Defaults_To_SqlServer(string? value, DatabaseProvider expected)
        => Assert.AreEqual(expected, DatabaseConfiguration.ParseProvider(value));

    [TestMethod]
    [DataRow("")]
    [DataRow("PostgresTypo")]
    public void Invalid_Provider_Fails_Before_Registering_A_Database(string provider)
        => Assert.ThrowsExactly<InvalidOperationException>(() => new ServiceCollection().AddAfterTabData("unused", provider));

    [TestMethod]
    [DataRow("SqlServer")]
    [DataRow("PostgreSql")]
    public void Runtime_And_Ef_Cli_Select_The_Same_Provider_And_Migration_Set(string provider)
    {
        var connection = provider == "SqlServer"
            ? "Server=(localdb)\\MSSQLLocalDB;Database=aftertab_design;Trusted_Connection=True;TrustServerCertificate=True"
            : "Host=localhost;Database=aftertab_design;Username=aftertab";
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUser, Actor>();
        services.AddAfterTabData(connection, provider);
        using var container = services.BuildServiceProvider();
        using var scope = container.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var design = new AppDbContextFactory().CreateDbContext([
            "--Database:Provider", provider, "--ConnectionStrings:AfterTab", connection]);
        Assert.AreEqual(runtime.Database.ProviderName, design.Database.ProviderName);
        CollectionAssert.AreEqual(runtime.Database.GetMigrations().ToArray(), design.Database.GetMigrations().ToArray());
        Assert.IsFalse(runtime.Database.HasPendingModelChanges(), "The selected migration snapshot must match the current model.");
        Assert.IsTrue(runtime.Database.GetMigrations().Any());

        var model = runtime.GetService<IDesignTimeModel>().Model;
        foreach (var entity in model.GetEntityTypes())
        {
            var key = entity.FindPrimaryKey()!;
            Assert.AreEqual(1, key.Properties.Count);
            Assert.AreEqual(typeof(long), key.Properties.Single().ClrType);
            Assert.AreEqual("bigint", key.Properties.Single().GetColumnType());
            foreach (var foreignKey in entity.GetForeignKeys())
                Assert.IsTrue(foreignKey.Properties.All(x => x.ClrType == typeof(long) || x.ClrType == typeof(long?)));
            if (typeof(PublicEntity).IsAssignableFrom(entity.ClrType))
            {
                Assert.AreEqual(typeof(Guid), entity.FindProperty("PublicId")!.ClrType);
                Assert.IsTrue(entity.GetIndexes().Any(x => x.IsUnique && x.Properties.Count == 1 && x.Properties[0].Name == "PublicId"));
            }
            // SQL Server creates clustered primary keys unless NONCLUSTERED is requested.
            if (provider == "SqlServer") Assert.AreNotEqual(false, key.IsClustered());
        }
        Assert.AreEqual(1, model.GetSequences().Count());
        Assert.IsTrue(model.GetSequences().Single().IncrementBy > 1);
        var version = model.FindEntityType(typeof(Expense))!.FindProperty(nameof(Expense.RowVersion))!;
        Assert.IsTrue(version.IsConcurrencyToken);
        var script = runtime.GetService<IMigrator>().GenerateScript();
        if (provider == "PostgreSql")
        {
            Assert.AreEqual(DatabaseConfiguration.PostgreSqlMigrationsAssembly,
                runtime.GetService<IMigrationsAssembly>().Assembly.GetName().Name);
            Assert.AreEqual(ValueGenerated.Never, version.ValueGenerated);
            Assert.Contains("bytea", script);
            Assert.DoesNotContain("[AmountMinor]", script);
            foreach (var entity in model.GetEntityTypes())
            {
                foreach (var constraint in entity.GetCheckConstraints()) Assert.DoesNotContain("[", constraint.Sql);
                foreach (var index in entity.GetIndexes()) Assert.DoesNotContain("[", index.GetFilter() ?? "");
            }
        }
        else
        {
            Assert.AreEqual(typeof(AppDbContext).Assembly, runtime.GetService<IMigrationsAssembly>().Assembly);
            Assert.AreEqual(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
            Assert.Contains("rowversion", script);
        }
    }
}
