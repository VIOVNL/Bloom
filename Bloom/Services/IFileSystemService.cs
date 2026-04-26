namespace Bloom.Services;

public interface IFileSystemService
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
}
