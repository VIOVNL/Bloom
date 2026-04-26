namespace Bloom.Services;

public interface IProcessLauncher
{
    void Launch(string fileName, string? arguments = null, string? workingDirectory = null);
}
