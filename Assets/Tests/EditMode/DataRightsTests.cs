using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class DataRightsTests
    {
        [TestCase(null)]
        [TestCase("")]
        public void ADeletionNeedsAPassword(string password)
        {
            Assert.AreEqual(DataRights.PasswordRequired, DataRights.ValidateDeletionPassword(password));
        }

        [Test]
        public void AnyTypedPasswordIsLeftForTheServerToCheck()
        {
            Assert.IsNull(DataRights.ValidateDeletionPassword("x"));
            Assert.IsNull(DataRights.ValidateDeletionPassword(" a password with spaces "));
        }

        [Test]
        public void TheExplanationSaysWhatIsKeptForHowLongAndThatTheDateIsToldOnce()
        {
            StringAssert.Contains("cannot be undone", DataRights.DeleteExplanation);
            StringAssert.Contains("for years (usually five)", DataRights.DeleteExplanation);
            StringAssert.Contains("make a note of the date", DataRights.DeleteExplanation);
            StringAssert.Contains("up to 14 days", DataRights.DeleteExplanation);   // the copy made before a software update
        }

        [Test]
        public void ADeletionWithNothingElseToSaySaysOnlyThatTheAccountIsGone()
        {
            Assert.AreEqual("Your account has been deleted.", DataRights.DescribeDeletion(0, 0, null));
        }

        [Test]
        public void EnquiriesThatStayAreCountedAndSaidToHaveLostTheirDetails()
        {
            Assert.AreEqual(
                "Your account has been deleted. Your 1 enquiry to a dealership stays as a record without your name and contact details.",
                DataRights.DescribeDeletion(1, 0, null));
            Assert.AreEqual(
                "Your account has been deleted. Your 3 enquiries to dealerships stay as a record without your name and contact details.",
                DataRights.DescribeDeletion(3, 0, null));
        }

        [Test]
        public void OneSaleThatIsKeptSaysUntilWhenInWordsAndToNoteTheDate()
        {
            var text = DataRights.DescribeDeletion(0, 1, "2031-01-15");

            StringAssert.Contains("1 sale record is kept with your details until 15 January 2031", text);
            StringAssert.Contains("because the dealership may have to keep a record of a sale", text);
            StringAssert.EndsWith("Make a note of the date.", text);
        }

        [Test]
        public void SeveralSalesAreNotAllPromisedTheSameDate_BecauseTheServerSendsOnlyTheEarliest()
        {
            var text = DataRights.DescribeDeletion(2, 3, "2031-01-15");

            StringAssert.Contains("3 sale records are kept with your details, the first until 15 January 2031 and the others as each period ends", text);
            StringAssert.DoesNotContain("are kept with your details until", text);
        }

        [Test]
        public void AMissingDateIsNotInventedAndNoNoteIsAskedFor()
        {
            var one = DataRights.DescribeDeletion(0, 1, null);
            var several = DataRights.DescribeDeletion(0, 2, null);

            StringAssert.Contains("until the dealership's retention period ends", one);
            StringAssert.Contains("until each retention period ends", several);
            StringAssert.DoesNotContain("Make a note", one);
            StringAssert.DoesNotContain("Make a note", several);
        }

        [TestCase("2031-01-15", "15 January 2031")]
        [TestCase(" 2026-11-05 ", "5 November 2026")]
        public void ADateIsWrittenInWords(string iso, string expected)
        {
            Assert.AreEqual(expected, DataRights.FormatDate(iso));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("soon")]
        [TestCase("15/01/2031")]
        public void WhatIsNotADateIsNotFormatted(string iso)
        {
            Assert.IsNull(DataRights.FormatDate(iso));
        }

        [Test]
        public void TheCopyIsDescribedInCountsAndSingularsAreRight()
        {
            Assert.AreEqual(
                "Your copy holds your account details, 1 enquiry, 1 saved car and 1 record of how you used the app.",
                DataRights.DescribeExport(1, 1, 1, false));
            Assert.AreEqual(
                "Your copy holds your account details, 0 enquiries, 2 saved cars and 340 records of how you used the app.",
                DataRights.DescribeExport(0, 2, 340, false));
        }

        [Test]
        public void ACopyThatHitTheCapSaysSoAndGivesTheAddressToAskForTheRest()
        {
            var text = DataRights.DescribeExport(1, 1, 9000, true);

            StringAssert.Contains("up to 5,000 records", text);
            StringAssert.Contains("email support@neoxr.co.za for the rest", text);
        }

        // --- the clipboard --------------------------------------------------------------

        [Test]
        public void ATextIsOfferedForTheClipboardOnlyWhileItIsSmallEnough()
        {
            Assert.IsTrue(DataRights.FitsClipboard(null));
            Assert.IsTrue(DataRights.FitsClipboard(new string('a', DataRights.ClipboardLimitBytes)));
            Assert.IsFalse(DataRights.FitsClipboard(new string('a', DataRights.ClipboardLimitBytes + 1)));
        }

        [Test]
        public void TheSizeIsCountedInBytesNotCharacters()
        {
            // "é" is two bytes in UTF-8, so half the limit in characters is already the limit in bytes.
            Assert.IsTrue(DataRights.FitsClipboard(new string('é', DataRights.ClipboardLimitBytes / 2)));
            Assert.IsFalse(DataRights.FitsClipboard(new string('é', DataRights.ClipboardLimitBytes / 2 + 1)));
        }

        [Test]
        public void TheTooLargeMessageSaysWhomToEmail()
        {
            StringAssert.Contains("support@neoxr.co.za", DataRights.TooLargeToCopy);
        }

        [Test]
        public void TheTooLargeMessagePointsToTheEmailButton()
        {
            StringAssert.Contains("Email it to me", DataRights.TooLargeToCopy);
        }

        [Test]
        public void TheEmailedCopyMessageNamesTheHiddenAddress()
        {
            StringAssert.Contains("p***@example.com", DataRights.EmailedCopySent("p***@example.com"));
        }

        [TestCase(null)]
        [TestCase("  ")]
        public void TheEmailedCopyMessageStillReadsWithoutAnAddress(string sentTo)
        {
            StringAssert.DoesNotContain("emailed your copy to .", DataRights.EmailedCopySent(sentTo));
            StringAssert.Contains("emailed your copy", DataRights.EmailedCopySent(sentTo));
        }

        [Test]
        public void TheWarningSaysToClearTheClipboardAndAboutKeyboardHistory()
        {
            StringAssert.Contains("clear your clipboard", DataRights.CopyWarning);
            StringAssert.Contains("keyboards keep a history", DataRights.CopyWarning);
        }

        // --- pretty-printing -------------------------------------------------------------

        [Test]
        public void CompactJsonIsLaidOutInIndentedLines()
        {
            var pretty = DataRights.PrettyPrintJson("{\"a\":1,\"b\":[1,2],\"c\":{\"d\":\"x\"}}");

            Assert.AreEqual("{\n  \"a\": 1,\n  \"b\": [\n    1,\n    2\n  ],\n  \"c\": {\n    \"d\": \"x\"\n  }\n}", pretty);
        }

        [Test]
        public void StringsAreLeftExactlyAsTheyAre_IncludingBracesCommasColonsAndEscapedQuotes()
        {
            var pretty = DataRights.PrettyPrintJson("{\"note\":\"a, b: {c} \\\"quoted\\\" [d]\",\"x\":1}");

            StringAssert.Contains("\"note\": \"a, b: {c} \\\"quoted\\\" [d]\"", pretty);
            StringAssert.Contains("\"x\": 1", pretty);
        }

        [Test]
        public void EmptyObjectsAndListsStayOnOneLine()
        {
            var pretty = DataRights.PrettyPrintJson("{\"leads\":[],\"activity\":{}}");

            Assert.AreEqual("{\n  \"leads\": [],\n  \"activity\": {}\n}", pretty);
        }

        [Test]
        public void PrettyPrintingKeepsEveryNonSpaceCharacterAndNothingIsLost()
        {
            const string compact = "{\"leads\":[{\"name\":\"Ada, \\\"A\\\"\",\"n\":3}],\"truncated\":false}";

            var pretty = DataRights.PrettyPrintJson(compact);

            Assert.AreEqual(Strip(compact), Strip(pretty));
        }

        [Test]
        public void EscapedCharactersAreShownAsTheCharactersTheyStandFor()
        {
            // What the server sends for O'Connor, a plus in an email address, and Müller.
            var pretty = DataRights.PrettyPrintJson("{\"name\":\"O\\u0027Connor\",\"email\":\"a\\u002Bb@example.com\",\"surname\":\"M\\u00FCller\"}");

            StringAssert.Contains("\"name\": \"O'Connor\"", pretty);
            StringAssert.Contains("\"email\": \"a+b@example.com\"", pretty);
            StringAssert.Contains("\"surname\": \"M\u00FCller\"", pretty);
        }

        [Test]
        public void ASurrogatePairIsPutBackAsOneCharacter()
        {
            var pretty = DataRights.PrettyPrintJson("{\"note\":\"hi \\uD83D\\uDE00\"}");

            StringAssert.Contains("\"note\": \"hi \uD83D\uDE00\"", pretty);
        }

        [TestCase("\\u0022")]   // a quote would end the string
        [TestCase("\\u005C")]   // a backslash would escape the next character
        [TestCase("\\u000A")]   // a new line inside a string is not valid JSON
        [TestCase("\\uD83D")]   // an unpaired surrogate
        [TestCase("\\uDE00")]
        [TestCase("\\u00zz")]   // not an escape at all
        public void WhatWouldMakeTheTextInvalidOrUnclearStaysEscaped(string escape)
        {
            var pretty = DataRights.PrettyPrintJson("{\"x\":\"a" + escape + "b\"}");

            StringAssert.Contains("a" + escape + "b", pretty);
        }

        [Test]
        public void AnEscapedBackslashBeforeAUIsNotMistakenForAnEscape()
        {
            // The text is: a backslash then the letters u0027 (an escaped backslash followed by plain text).
            var pretty = DataRights.PrettyPrintJson("{\"x\":\"\\\\u0027\"}");

            StringAssert.Contains("\"x\": \"\\\\u0027\"", pretty);
        }

        [TestCase(null)]
        [TestCase("")]
        public void NothingToPrintIsReturnedAsItIs(string json)
        {
            Assert.AreEqual(json, DataRights.PrettyPrintJson(json));
        }

        // Whitespace outside strings only (the compact input has none, so comparing without any is enough for this sample).
        private static string Strip(string s) => s.Replace("\n", "").Replace(" ", "");
    }
}
