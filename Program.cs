using TestProject.Business;

namespace TestProject {
    public class Program {
        public static async Task Main(string[] args) {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();

            builder.Services.AddSingleton<InMemoryMetadataStorage<FileSystemItem>>();
            builder.Services.AddSingleton<IMetadataStorage<FileSystemItem>>(sp =>
                sp.GetRequiredService<InMemoryMetadataStorage<FileSystemItem>>());

            builder.Services.AddSingleton<FileSystemSearchEngine>();
            builder.Services.AddSingleton<ISearchEngine<FileSystemItem>>(sp =>
                sp.GetRequiredService<FileSystemSearchEngine>());

            builder.Services.AddSingleton(sp => new FileSystemStorage(
                Path.Combine(builder.Environment.ContentRootPath, "Storage"),
                sp.GetRequiredService<ISearchEngine<FileSystemItem>>()));
            builder.Services.AddSingleton<IStorage<FileSystemItem>>(sp =>
                sp.GetRequiredService<FileSystemStorage>());

            var app = builder.Build();

            // Wire up the metadata storage dependency and seed the root directory.
            var metadataStorage = app.Services.GetRequiredService<IMetadataStorage<FileSystemItem>>();
            app.Services.GetRequiredService<ISearchEngine<FileSystemItem>>().MetadataStorage = metadataStorage;
            var storage = app.Services.GetRequiredService<FileSystemStorage>();
            storage.MetadataStorage = metadataStorage;
            await storage.EnsureRoot();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.MapControllers();

            app.Run();
        }
    }
}
