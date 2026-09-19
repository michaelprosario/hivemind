using FileRepo.Core.Files;
using FileRepo.Infrastructure.FileSystem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace FileRepo.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddFileRepositoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FileSystemRepositoryOptions>().BindConfiguration("FileRepository").ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IFileRepository, FileSystemFileRepository>();
        return services;
    }
}
