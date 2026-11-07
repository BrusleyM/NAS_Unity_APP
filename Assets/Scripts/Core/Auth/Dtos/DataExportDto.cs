using System;
using System.Collections.Generic;

namespace NAS.Core.Auth.Dtos
{
    /// <summary>
    /// Only what is needed to COUNT what a downloaded copy holds (NAS_Backend CustomerDataExportDto). The copy itself is shown and copied as the
    /// server's own text, so none of its fields are mirrored here; each list entry is an empty record because only how many there are matters.
    /// </summary>
    [Serializable]
    public sealed class DataExportDto
    {
        public List<ExportRecord> leads = new List<ExportRecord>();
        public List<ExportRecord> savedBuilds = new List<ExportRecord>();
        public ExportActivity activity = new ExportActivity();

        public int ActivityRecordCount =>
            Count(activity?.sessions) + Count(activity?.events) + Count(activity?.vehicleInteractions) +
            Count(activity?.arSessions) + Count(activity?.affordabilitySessions) + Count(activity?.screenVisits);

        private static int Count(List<ExportRecord> list) => list?.Count ?? 0;
    }

    [Serializable]
    public sealed class ExportRecord
    {
    }

    [Serializable]
    public sealed class ExportActivity
    {
        public bool truncated;
        public List<ExportRecord> sessions = new List<ExportRecord>();
        public List<ExportRecord> events = new List<ExportRecord>();
        public List<ExportRecord> vehicleInteractions = new List<ExportRecord>();
        public List<ExportRecord> arSessions = new List<ExportRecord>();
        public List<ExportRecord> affordabilitySessions = new List<ExportRecord>();
        public List<ExportRecord> screenVisits = new List<ExportRecord>();
    }

    /// <summary>POST /api/customer/profile/export/email: where the copy went, partly hidden ("p***@example.com").</summary>
    [Serializable]
    public sealed class EmailedDataCopyResponse
    {
        public string sentTo;
    }
}
