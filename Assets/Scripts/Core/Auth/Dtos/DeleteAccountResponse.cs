using System;

namespace NAS.Core.Auth.Dtos
{
    /// <summary>What a deletion did, in counts only (NAS_Backend CustomerErasureResultDto).</summary>
    [Serializable]
    public sealed class DeleteAccountResponse
    {
        public int leadsAnonymised;       // enquiries kept as records with the contact details removed
        public int telemetryRowsDeleted;
        public int buildsDeleted;
        public bool accountDeleted;
        public int soldRecordsKept;       // sales a dealership may have to keep, with the details, for a while
        // "2031-01-15" or null (a DateOnly on the server). JsonUtility has no nullable date, so it stays a string.
        public string soldRecordsKeptUntil;
    }
}
