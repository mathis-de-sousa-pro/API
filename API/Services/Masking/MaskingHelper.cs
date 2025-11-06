using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace API.Services.Masking;

public sealed class MaskingHelper : IMaskingHelper
{
    private readonly Regex[] _tokenPatterns;
    private readonly HashSet<string> _redactHeaders;

    public MaskingHelper(IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        _tokenPatterns = (configuration.GetSection("Masking:TokenPatterns").Get<string[]>() ?? Array.Empty<string>())
            .Select(pattern => new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase))
            .ToArray();

        _redactHeaders = new HashSet<string>(
            configuration.GetSection("Masking:RedactHeaders").Get<string[]>() ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public string Mask(string input)
    {
        string result = input;

        if (!string.IsNullOrEmpty(result))
        {
            foreach (Regex regex in _tokenPatterns)
            {
                result = regex.Replace(result, "Bearer ***");
            }
        }

        return result;
    }

    public IDictionary<string, string> SanitizeHeaders(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        Dictionary<string, string> sanitized = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, StringValues value) in headers)
        {
            string joined = string.Join(";", value);
            sanitized[key] = _redactHeaders.Contains(key) ? "***" : Mask(joined);
        }

        return sanitized;
    }

    public string? Truncate(string? input, int max)
    {
        string? result = input;

        if (!string.IsNullOrEmpty(result))
        {
            if (max <= 0)
            {
                result = string.Empty;
            }
            else if (result!.Length > max)
            {
                result = result.Substring(0, max);
            }
        }

        return result;
    }
}
