using NAS.Core.Auth.Dtos;
using UnityEngine;

namespace NAS.Core.Auth
{
    /// <summary>What a downloaded copy holds, counted from the server's JSON. Kept out of the screen's controller so it can be tested.</summary>
    public readonly struct DataExportCounts
    {
        public readonly int Enquiries;
        public readonly int SavedCars;
        public readonly int ActivityRecords;
        public readonly bool Truncated;

        public DataExportCounts(int enquiries, int savedCars, int activityRecords, bool truncated)
        {
            Enquiries = enquiries;
            SavedCars = savedCars;
            ActivityRecords = activityRecords;
            Truncated = truncated;
        }
    }

    public static class DataExportReader
    {
        public static DataExportCounts ReadCounts(string json)
        {
            var export = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<DataExportDto>(json);
            if (export == null) return new DataExportCounts(0, 0, 0, false);
            return new DataExportCounts(export.leads?.Count ?? 0, export.savedBuilds?.Count ?? 0, export.ActivityRecordCount, export.activity != null && export.activity.truncated);
        }

        /// <summary>The one sentence the screen shows under the button.</summary>
        public static string Describe(string json)
        {
            var counts = ReadCounts(json);
            return DataRights.DescribeExport(counts.Enquiries, counts.SavedCars, counts.ActivityRecords, counts.Truncated);
        }
    }
}
