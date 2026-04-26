namespace Bloom.Services;

public interface IApplicationService
{
    void SetThemeVariant(bool isDark);
    void Shutdown();
}
