using System.IO;
using Game.Configs.Editor;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    /// <summary>
    /// The style doc is handed to writers verbatim, so the numbers in it are the numbers writers obey. The
    /// validator enforces its own constants, and the two drifting apart means people are briefed against a
    /// rule nothing checks. C# is authoritative; this test fails the prose, not the code.
    /// </summary>
    public sealed class BookDescriptionStyleDocTests
    {
        private const string StyleDocPath = "docs/content/BOOK_DESCRIPTION_STYLE.md";

        [Test]
        public void StyleDoc_QuotesTheEnforcedLengthWindow()
        {
            var doc = ReadDoc();
            var window = $"{BookDescriptionDraftValidator.MinReasonableLength}-" +
                         $"{BookDescriptionDraftValidator.MaxReasonableLength} characters";

            StringAssert.Contains(
                window,
                doc,
                $"{StyleDocPath} must state the window the validator enforces ('{window}').");
        }

        [Test]
        public void StyleDoc_QuotesTheOverlapThreshold()
        {
            var doc = ReadDoc();
            var threshold = $"{BookDescriptionDraftValidator.OverlapWindowWords} words";

            StringAssert.Contains(
                threshold,
                doc,
                $"{StyleDocPath} must state the clean-room overlap threshold ('{threshold}').");
        }

        [Test]
        public void StyleDoc_ListsTheCharactersTheRendererEats()
        {
            var doc = ReadDoc();

            foreach (var forbidden in new[] { "{", "}", "<", ">" })
            {
                StringAssert.Contains(
                    forbidden,
                    doc,
                    $"{StyleDocPath} must show that '{forbidden}' is forbidden.");
            }
        }

        [Test]
        public void StyleDoc_StatesTheFirstPersonRule()
        {
            var doc = ReadDoc();

            StringAssert.Contains(
                "first person",
                doc,
                $"{StyleDocPath} must state the first-person rule — the validator makes it an error.");
        }

        private static string ReadDoc()
        {
            Assert.IsTrue(File.Exists(StyleDocPath), $"{StyleDocPath} not found.");
            return File.ReadAllText(StyleDocPath);
        }
    }
}
