using System.Threading;
using System.Threading.Tasks;
using Game.Tutorial;
using Game.Tutorial.Content;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialAwaitWindowStepTests
    {
        [Test]
        public async Task ExecuteAsync_WhenFailOpenTimeoutExpires_LogsAndCompletes()
        {
            var step = new TutorialAwaitWindowStep(
                "wait_missing_window",
                () => false,
                timeoutMs: 1,
                failOpen: true);

            LogAssert.Expect(
                UnityEngine.LogType.Warning,
                $"{TutorialLog.Prefix} await window step 'wait_missing_window' timed out after 1ms; auto-advancing.");

            await step.ExecuteAsync(CancellationToken.None);
        }
    }
}
