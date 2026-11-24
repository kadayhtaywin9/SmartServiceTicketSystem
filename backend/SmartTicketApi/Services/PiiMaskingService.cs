using System.Text.RegularExpressions;

namespace SmartTicketApi.Services;

public class PiiMaskingService
{
    // Matches Credit/Debit cards (13-19 digits with optional spaces/dashes)
    private static readonly Regex CardRegex = new(@"\b(?:\d[ -]*?){13,19}\b", RegexOptions.Compiled);

    // Matches Myanmar NRC format e.g. 12/TaKaNa(N)123456
    private static readonly Regex NrcRegex = new(@"\b\d{1,2}/[A-Za-z]+\([A-Za-z]\)\d{6}\b", RegexOptions.Compiled);

    // Matches realistic Phone Numbers (+959..., 09-..., +1-555-..., etc.) without corrupting TKT-2026-000101
    private static readonly Regex PhoneRegex = new(@"(?<!TKT-\d{4}-)(?:\+\d{1,3}[-.\s]?\d{2,4}[-.\s]?\d{3,4}[-.\s]?\d{3,4}|\b09[-.\s]?\d{7,9}\b)", RegexOptions.Compiled);

    // Matches Bearer / API Secrets
    private static readonly Regex TokenRegex = new(@"(?:sk-|ghp_|Bearer\s+)[A-Za-z0-9_\-\.]{16,}", RegexOptions.Compiled);

    public (string MaskedText, bool WasMasked) MaskSensitiveData(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (input, false);

        string result = input;
        bool masked = false;

        if (TokenRegex.IsMatch(result))
        {
            result = TokenRegex.Replace(result, "[MASKED-SECRET-TOKEN]");
            masked = true;
        }

        if (NrcRegex.IsMatch(result))
        {
            result = NrcRegex.Replace(result, "[MASKED-NATIONAL-ID]");
            masked = true;
        }

        if (CardRegex.IsMatch(result))
        {
            result = CardRegex.Replace(result, "[MASKED-CARD-NUMBER]");
            masked = true;
        }

        if (PhoneRegex.IsMatch(result))
        {
            result = PhoneRegex.Replace(result, "[MASKED-PHONE]");
            masked = true;
        }

        return (result, masked);
    }
}
