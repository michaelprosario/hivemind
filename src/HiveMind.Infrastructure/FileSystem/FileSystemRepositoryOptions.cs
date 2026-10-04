using System.ComponentModel.DataAnnotations;
namespace HiveMind.Infrastructure.FileSystem;
public sealed class FileSystemRepositoryOptions
{
    [Required] public string RootPath { get; set; } = "";
    public long MaxFileSizeBytes { get; set; } = 10_485_760;
}
