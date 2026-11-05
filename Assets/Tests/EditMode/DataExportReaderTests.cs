using NAS.Core.Auth;
using NUnit.Framework;

namespace NAS.Tests
{
    /// <summary>
    /// The copy is counted with JsonUtility, which has to keep the length of a list of objects that have no fields of their own here. These
    /// run in Unity (they need the engine's JSON), and use a sample shaped like the backend's real export (camelCase, nested activity lists).
    /// </summary>
    public class DataExportReaderTests
    {
        private const string Export = @"{
  ""generatedAt"": ""2026-11-05T20:00:00Z"",
  ""account"": { ""email"": ""ada@example.com"", ""firstName"": ""Ada"", ""lastName"": ""Lovelace"", ""cellNumber"": ""0821234567"", ""status"": ""Active"" },
  ""leads"": [
    { ""dealership"": ""Acme"", ""vehicle"": ""Kwid"", ""firstName"": ""Ada"", ""email"": ""ada@example.com"", ""phone"": ""0821234567"", ""status"": ""New"", ""termMonths"": 60, ""estimatedMonthly"": 3500.5 },
    { ""dealership"": ""Acme"", ""vehicle"": ""Duster"", ""firstName"": ""Ada"", ""email"": ""ada@example.com"", ""phone"": ""0821234567"", ""status"": ""Sold"", ""termMonths"": 72, ""estimatedMonthly"": 4100 },
    { ""dealership"": ""Beta"", ""vehicle"": null, ""firstName"": ""Ada"", ""email"": ""ada@example.com"", ""phone"": ""0821234567"", ""status"": ""New"", ""termMonths"": 48, ""estimatedMonthly"": 2900 }
  ],
  ""savedBuilds"": [
    { ""vehicle"": ""Kwid"", ""totalPrice"": 189900, ""createdAt"": ""2026-10-01T10:00:00Z"" },
    { ""vehicle"": ""Duster"", ""totalPrice"": 289900, ""createdAt"": ""2026-10-02T10:00:00Z"" }
  ],
  ""activity"": {
    ""truncated"": true,
    ""sessions"": [ { ""startedAt"": ""2026-10-01T10:00:00Z"" }, { ""startedAt"": ""2026-10-02T10:00:00Z"" } ],
    ""events"": [ { ""eventType"": ""a"" }, { ""eventType"": ""b"" }, { ""eventType"": ""c"" }, { ""eventType"": ""d"" } ],
    ""vehicleInteractions"": [ { ""vehicle"": ""Kwid"" } ],
    ""arSessions"": [],
    ""affordabilitySessions"": [ { ""termMonths"": 60 }, { ""termMonths"": 72 } ],
    ""screenVisits"": [ { ""screenName"": ""login"" }, { ""screenName"": ""privacy"" }, { ""screenName"": ""estimator"" } ]
  }
}";

        [Test]
        public void EveryListEntryIsCounted_EvenThoughTheRecordClassHasNoFields()
        {
            var counts = DataExportReader.ReadCounts(Export);

            Assert.AreEqual(3, counts.Enquiries);
            Assert.AreEqual(2, counts.SavedCars);
            Assert.AreEqual(2 + 4 + 1 + 0 + 2 + 3, counts.ActivityRecords);
            Assert.IsTrue(counts.Truncated);
        }

        [Test]
        public void ACopyWithNothingInItIsCountedAsNothing()
        {
            var counts = DataExportReader.ReadCounts("{\"leads\":[],\"savedBuilds\":[],\"activity\":{\"truncated\":false}}");

            Assert.AreEqual(0, counts.Enquiries);
            Assert.AreEqual(0, counts.SavedCars);
            Assert.AreEqual(0, counts.ActivityRecords);
            Assert.IsFalse(counts.Truncated);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void NoTextIsCountedAsNothingAndDoesNotThrow(string json)
        {
            Assert.AreEqual(0, DataExportReader.ReadCounts(json).Enquiries);
        }

        [Test]
        public void TheSentenceUsesTheCounts()
        {
            var text = DataExportReader.Describe(Export);

            StringAssert.Contains("3 enquiries", text);
            StringAssert.Contains("2 saved cars", text);
            StringAssert.Contains("12 records of how you used the app", text);
            StringAssert.Contains("up to 5,000 records", text);
        }
    }
}
