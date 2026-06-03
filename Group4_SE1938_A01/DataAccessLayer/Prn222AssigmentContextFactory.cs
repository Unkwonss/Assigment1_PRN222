using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Domain.Models;

namespace DataAccessLayer;

public class Prn222AssigmentContextFactory : IDesignTimeDbContextFactory<Prn222AssigmentContext>
{
    public Prn222AssigmentContext CreateDbContext(string[] args)
    {
        var dataAccessDir = Directory.GetCurrentDirectory();
        var presentationDir = Path.GetFullPath(Path.Combine(dataAccessDir, "..", "PresentationLayer"));

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(presentationDir, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(presentationDir, "appsettings.Development.json"), optional: true)
            .Build();

        string connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? "Server=.;Database=Prn222_assigment;User Id=sa;Password=12345;TrustServerCertificate=True;MultipleActiveResultSets=True;";

        var optionsBuilder = new DbContextOptionsBuilder<Prn222AssigmentContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new Prn222AssigmentContext(optionsBuilder.Options);
    }
}
