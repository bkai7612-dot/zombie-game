using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UZS.Gameplay;

namespace UZS.Tests
{
    /// <summary>Runs the end-to-end single-player smoke test inside the real Main scene.</summary>
    public class SmokePlayModeTest
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SinglePlayerLoopWorks()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.NotNull(GameBootstrap.Instance, "Main scene with GameBootstrap must be in the build settings (Tools > UZS > Set Up Project)");

            var smoke = GameBootstrap.Instance.gameObject.AddComponent<SmokeTest>();
            float end = Time.realtimeSinceStartup + 90f;
            while (!smoke.Finished && Time.realtimeSinceStartup < end) yield return null;

            Assert.IsTrue(smoke.Finished, "Smoke test timed out:\n" + smoke.Report);
            Assert.AreEqual(0, smoke.Failures, smoke.Report);
        }
    }
}
