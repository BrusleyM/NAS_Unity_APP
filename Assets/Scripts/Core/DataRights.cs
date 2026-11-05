using System;
using System.Globalization;

namespace NAS.Core
{
    /// <summary>
    /// What the customer reads on the two "your information" screens (download a copy, delete the account).
    /// No Unity, no UI: unit-tested. The numbers and the sold-deal date come from the server's answers; the wording lives here.
    /// </summary>
    public static class DataRights
    {
        /// <summary>The most rows of each kind of activity the server puts in a copy. Mirrors CustomerDataExportDto.ExportRowCap in NAS_Backend.</summary>
        public const int ExportRowCap = 5000;

        public const string PasswordRequired = "Enter your password to delete your account.";

        public const string DeleteExplanation =
            "This deletes your account, your saved cars and the record of how you used the app, and it cannot be undone. " +
            "The enquiries you sent to dealerships stay as records for the dealership's own figures, without your name and contact details. " +
            "If an enquiry became a sale, the dealership may have to keep that record, with your details, for a few years; we will tell you until when.";

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
                var until = FormatDate(soldRecordsKeptUntil) ?? "the dealership's retention period ends";
                text += $" {soldRecordsKept} sale {Plural(soldRecordsKept, "record is", "records are")} kept with your details until {until}, because the dealership may have to keep a record of a sale.";
            }
            return text;
        }

        /// <summary>One sentence about what the downloaded copy holds, so the customer knows what they are looking at.</summary>
        public static string DescribeExport(int enquiries, int savedBuilds, int activityRecords, bool truncated)
        {
            var text = $"Your copy holds your account details, {enquiries} {Plural(enquiries, "enquiry", "enquiries")}, " +
                       $"{savedBuilds} saved {Plural(savedBuilds, "car", "cars")} and {activityRecords} {Plural(activityRecords, "record", "records")} of how you used the app.";
            if (truncated)
            {
                text += $" For some kinds of activity it holds up to {ExportRowCap.ToString("N0", CultureInfo.InvariantCulture)} records; ask us for the rest.";
            }
            return text;
        }

        /// <summary>"2031-01-15" (what the server sends) to "15 January 2031". Null when it is not a date.</summary>
        public static string FormatDate(string isoDate)
        {
            if (string.IsNullOrWhiteSpace(isoDate)) return null;
            return DateTime.TryParseExact(isoDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)
                : null;
        }

        private static string Plural(int count, string one, string many) => count == 1 ? one : many;
    }
}
