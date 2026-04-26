namespace Bloom.Services;

public interface IIconExtractorService
{
    string? ExtractIconAsBase64(string filePath);
}
