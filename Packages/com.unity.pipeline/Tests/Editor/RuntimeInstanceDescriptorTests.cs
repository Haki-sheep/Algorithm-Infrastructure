using System.IO;
using NUnit.Framework;
using Unity.Pipeline.Models;
using UnityEngine;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// UUM-151968: on Android, Application.dataPath points inside the read-only APK install
    /// directory, so GetDescriptorFilePath() must not derive the descriptor location from it there.
    /// </summary>
    class RuntimeInstanceDescriptorTests
    {
        [TearDown]
        public void TearDown()
        {
            RuntimeInstanceDescriptor.OverridePlatform = null;
        }

        [Test]
        public void GetDescriptorFilePath_OnAndroid_UsesPersistentDataPath()
        {
            RuntimeInstanceDescriptor.OverridePlatform = RuntimePlatform.Android;

            var path = RuntimeInstanceDescriptor.GetDescriptorFilePath();

            var expectedDir = Path.GetFullPath(Application.persistentDataPath);
            var actualDir = Path.GetFullPath(Path.GetDirectoryName(path));
            Assert.AreEqual(expectedDir, actualDir);
        }
    }
}
