using System;
using System.Globalization;
using System.Text;

namespace NAS.Core
{
    /// <summary>
    /// What the customer reads on the two "your information" screens (a copy of their data, deleting the account).
    /// No Unity, no UI: unit-tested. The numbers and the sold-deal date come from the server's answers; the wording lives here.
    /// </summary>
    public static class DataRights
    {
        /// <summary>The most rows of each kind of activity the server puts in a copy. Mirrors CustomerDataExportDto.ExportRowCap in NAS_Backend.</summary>
        public const int ExportRowCap = 5000;

        /// <summary>The same address the privacy notice gives for requests.</summary>
        public const string SupportEmail = "support@neoxr.co.za";

        /// <summary>
        /// The most text put on the clipboard. Android's clipboard passes it through a size-limited channel (about 1 MB) and a larger
        /// paste can freeze a notes or mail app, so a bigger copy is not offered (the person is told to ask for it).
        /// </summary>
        public const int ClipboardLimitBytes = 500_000;

        public const string PasswordRequired = "Enter your password to delete your account.";

        public const string DeleteExplanation =
            "This deletes your account, your saved cars and the record of how you used the app, and it cannot be undone. " +
            "The enquiries you sent to dealerships stay as records for the dealership's own figures, without your name and contact details. " +
            "If an enquiry became a sale, the dealership may have to keep that record, with your details, for years (usually five). " +
            "The sign-in screen tells you until when, once, so make a note of the date. " +
            "A protected copy of our database from before our last software update can hold deleted information for up to 14 days; it is not used.";

        public const string CopyWarning =
            "Paste it somewhere safe, then clear your clipboard. Some keyboards keep a history of what you copy, and your other devices may receive it.";

        public static readonly string TooLargeToCopy =
            $"Your copy is too large to copy from here. Email {SupportEmail} and we will send it to you.";

        /// <summary>Null when the password is fine to send. The server checks that it is the right one.</summary>
        public static string ValidateDeletionPassword(string password) =>
            string.IsNullOrEmpty(password) ? PasswordRequired : null;

        /// <summary>The message shown on the sign-in screen after the account has been deleted.</summary>
        public static string DescribeDeletion(int enquiriesAnonymised, int soldRecordsKept, string soldRecordsKeptUntil)
        {
            var text = "Your account has been deleted.";
            if (enquiriesAnonymised > 0)
            {
                text += $" Your {enquiriesAnonymised} {Plural(enquiriesAnonymised, "enquiry to a dealership stays", "enquiries to dealerships stay")} as a record without your name and contact details.";
            }
            if (soldRecordsKept > 0)
            {
                var date = FormatDate(soldRecordsKeptUntil);
                // The server sends ONE date: the earliest of the periods. With several sales the others end later, so it is not promised for all.
                if (soldRecordsKept == 1)
                {
                    text += $" 1 sale record is kept with your details until {date ?? "the dealership's retention period ends"}, because the dealership may have to keep a record of a sale.";
                }
                else
                {
                    var first = date != null ? $"the first until {date} and the others as each period ends" : "until each retention period ends";
                    text += $" {soldRecordsKept} sale records are kept with your details, {first}, because the dealership may have to keep a record of a sale.";
                }
                if (date != null) text += " Make a note of the date.";
            }
            return text;
        }

        /// <summary>One sentence about what the copy holds, so the customer knows what they are looking at.</summary>
        public static string DescribeExport(int enquiries, int savedBuilds, int activityRecords, bool truncated)
        {
            var text = $"Your copy holds your account details, {enquiries} {Plural(enquiries, "enquiry", "enquiries")}, " +
                       $"{savedBuilds} saved {Plural(savedBuilds, "car", "cars")} and {activityRecords} {Plural(activityRecords, "record", "records")} of how you used the app.";
            if (truncated)
            {
                text += $" For some kinds of activity it holds up to {ExportRowCap.ToString("N0", CultureInfo.InvariantCulture)} records; email {SupportEmail} for the rest.";
            }
            return text;
        }

        /// <summary>True when the text is small enough to put on the clipboard (see <see cref="ClipboardLimitBytes"/>).</summary>
        public static bool FitsClipboard(string text) =>
            string.IsNullOrEmpty(text) || Encoding.UTF8.GetByteCount(text) <= ClipboardLimitBytes;

        /// <summary>
        /// Lays compact JSON out in lines and indents, so what is pasted can be read. Strings are left as they are, except as below; empty
        /// objects and lists stay on one line. The server's \\uXXXX escapes are turned back into the characters (see below). It does not check that the text is valid JSON.
        /// </summary>
        public static string PrettyPrintJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            var sb = new StringBuilder(json.Length + json.Length / 3);
            var indent = 0;
            var inString = false;
            var escaped = false;
            for (var i = 0; i < json.Length; i++)
            {
                var c = json[i];
                if (inString)
                {
                    // The server writes every non-ASCII character and some ASCII ones (an apostrophe, a plus sign) as \uXXXX. They are put back as
                    // the characters they stand for, so "O'Connor" and "Müller" read as names. Quotes, backslashes, control characters and an
                    // unpaired surrogate stay escaped, so the text is still valid JSON.
                    if (!escaped && c == '\\' && TryReadUnicodeEscape(json, i, out var shown, out var length))
                    {
                        sb.Append(shown);
                        i += length - 1;
                        continue;
                    }
                    sb.Append(c);
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        sb.Append(c);
                        break;
                    case '{':
                    case '[':
                        var next = NextNonSpace(json, i + 1);
                        if (next > 0 && json[next] == (c == '{' ? '}' : ']'))
                        {
                            sb.Append(c).Append(json[next]);
                            i = next;
                        }
                        else
                        {
                            sb.Append(c);
                            indent++;
                            NewLine(sb, indent);
                        }
                        break;
                    case '}':
                    case ']':
                        indent = Math.Max(0, indent - 1);
                        NewLine(sb, indent);
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c);
                        NewLine(sb, indent);
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    default:
                        if (!char.IsWhiteSpace(c)) sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>"2031-01-15" (what the server sends) to "15 January 2031". Null when it is not a date.</summary>
        public static string FormatDate(string isoDate)
        {
            if (string.IsNullOrWhiteSpace(isoDate)) return null;
            return DateTime.TryParseExact(isoDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)
                : null;
        }

        // At the backslash of \uXXXX: the character (or the surrogate pair) it stands for, and how many characters of the text it covers.
        private static bool TryReadUnicodeEscape(string s, int at, out string shown, out int length)
        {
            shown = null;
            length = 0;
            if (!TryHex4(s, at, out var first)) return false;
            if (char.IsHighSurrogate(first))
            {
                if (!TryHex4(s, at + 6, out var second) || !char.IsLowSurrogate(second)) return false;
                shown = new string(new[] { first, second });
                length = 12;
                return true;
            }
            if (char.IsLowSurrogate(first) || first == '"' || first == '\\' || first < ' ') return false;
            shown = first.ToString();
            length = 6;
            return true;
        }

        private static bool TryHex4(string s, int at, out char value)
        {
            value = default;
            if (at + 6 > s.Length || s[at] != '\\' || s[at + 1] != 'u') return false;
            var code = 0;
            for (var k = 2; k < 6; k++)
            {
                var digit = Uri.IsHexDigit(s[at + k]) ? Convert.ToInt32(s[at + k].ToString(), 16) : -1;
                if (digit < 0) return false;
                code = code * 16 + digit;
            }
            value = (char)code;
            return true;
        }

        private static int NextNonSpace(string s, int from)
        {
            for (var i = from; i < s.Length; i++) if (!char.IsWhiteSpace(s[i])) return i;
            return -1;
        }

        private static void NewLine(StringBuilder sb, int indent) => sb.Append('\n').Append(' ', indent * 2);

        private static string Plural(int count, string one, string many) => count == 1 ? one : many;
    }
}
