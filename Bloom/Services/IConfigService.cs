using Bloom.Models;

namespace Bloom.Services;

public interface IConfigService
{
    BloomConfig Load();
    void Save(BloomConfig config);
}
