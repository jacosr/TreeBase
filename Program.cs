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

            builder.Services.AddSingleton(sp =>
            {
                var configuredPath = builder.Configuration["Storage:RootPath"] ?? "Storage";
                var rootPath = Path.IsPathRooted(configuredPath)
                    ? configuredPath
                    : Path.Combine(builder.Environment.ContentRootPath, configuredPath);

                return new FileSystemStorage(rootPath, sp.GetRequiredService<ISearchEngine<FileSystemItem>>());
            });
            builder.Services.AddSingleton<IStorage<FileSystemItem>>(sp =>
                sp.GetRequiredService<FileSystemStorage>());

            var app = builder.Build();

            // Wire up the metadata storage dependency and seed the metadata from disk if necessary.
            var metadataStorage = app.Services.GetRequiredService<IMetadataStorage<FileSystemItem>>();
            var searchEngine = app.Services.GetRequiredService<ISearchEngine<FileSystemItem>>();
            searchEngine.MetadataStorage = metadataStorage;
            var storage = app.Services.GetRequiredService<FileSystemStorage>();
            storage.MetadataStorage = metadataStorage;

            var metadataFile = builder.Configuration["Search:MetadataFile"];
            if (!string.IsNullOrEmpty(metadataFile))
            {
                await metadataStorage.Load(metadataFile);
            }

            if (!(await metadataStorage.GetAll()).Any())
            {
                foreach (var item in await storage.CollectMetadata())
                {
                    await metadataStorage.Add(item);
                }
            }

            await storage.EnsureRoot();
            await searchEngine.BuildIndexes();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapControllers();

            app.Run();
        }
    }
}
