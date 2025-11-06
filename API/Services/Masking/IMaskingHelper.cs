namespace API.Services.Masking;

public interface IMaskingHelper
{
    string Mask(string input);

    IDictionary<string, string> SanitizeHeaders(IHeaderDictionary headers);

    string? Truncate(string? input, int max);
}
