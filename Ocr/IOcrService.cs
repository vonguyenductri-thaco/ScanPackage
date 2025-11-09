namespace ScanPackage;

public enum OcrMode
{
    Container,
    Seal
}

public interface IOcrService
{
    Task<string?> ScanTextAsync(OcrMode mode);
}



