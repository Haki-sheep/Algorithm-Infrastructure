using NUnit.Framework;
using Unity.Pipeline.Config;
using Unity.Pipeline.Tests.Editor.ServerLifecyle;
using UnityEditor;
using UnityEngine;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Tests for RuntimePipelineBootstrap.Bootstrap(), the core logic behind the
    /// [RuntimeInitializeOnLoadMethod] entry point. Creates/deletes the authored ProjectSettings/
    /// JSON settings file per test since RuntimePipelineConfig.Load() has no override hook for
    /// testing in the Editor.
    /// </summary>
    class RuntimePipelineBootstrapTests
    {
        private GameObject m_CreatedDriverGameObject;

        [SetUp]
        public void SetUp()
        {
            GlobalServerStateGuard.Capture();

            RuntimePipelineResourceTestGuard.AssertNoConfigAssetExists();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_CreatedDriverGameObject != null)
                Object.DestroyImmediate(m_CreatedDriverGameObject);

            RuntimePipelineResourceTestGuard.DeleteConfigIfExists();

            RuntimePipelineBootstrap.Instance = null;

            GlobalServerStateGuard.Restore();
        }

        [TestCase(false, TestName = "Bootstrap_NoConfigAsset_ReturnsNullAndCreatesNothing")]
        [TestCase(true, TestName = "Bootstrap_ConfigDisabled_ReturnsNullAndCreatesNothing")]
        public void Bootstrap_NoUsableConfig_ReturnsNullAndCreatesNothing(bool createDisabledConfig)
        {
            if (createDisabledConfig)
                CreateTestConfig(enableInBuilds: false);

            var driver = RuntimePipelineBootstrap.Bootstrap();

            Assert.IsNull(driver, "Bootstrap should do nothing when there is no usable config");
        }

        [Test] // In-editor code reload must not depend on the runtime server (a player-build
               // feature): with no driver to run discovery, Bootstrap registers the targets itself.
        public void Bootstrap_NoUsableConfig_StillRegistersCodeReloadTargets()
        {
            CodeReload.CodeReloadRegistry.ClearAllForTesting();
            try
            {
                var driver = RuntimePipelineBootstrap.Bootstrap();

                Assert.IsNull(driver);
                var ids = CodeReload.CodeReloadRegistry.GetStats().ReloadableMethodIds;
                CollectionAssert.Contains(ids, "InPlaceReloadProbe.Compute",
                    "Entering Play Mode with the runtime server disabled must still register [CodeReload] targets");
            }
            finally
            {
                CodeReload.CodeReloadRegistry.ClearAllForTesting();
            }
        }

        [Test]
        public void Bootstrap_ConfigEnabled_CreatesDriverWithConfig()
        {
            CreateTestConfig(enableInBuilds: true, port: 7930);

            var driver = RuntimePipelineBootstrap.Bootstrap();
            m_CreatedDriverGameObject = driver != null ? driver.gameObject : null;

            Assert.IsNotNull(driver, "Bootstrap should create a driver when enableInBuilds is true");
            Assert.AreEqual(7930, driver.Config.port, "Driver should be initialized with the loaded config");
        }

        [Test]
        public void Bootstrap_CalledTwice_ReturnsSameDriver()
        {
            CreateTestConfig(enableInBuilds: true);

            var first = RuntimePipelineBootstrap.Bootstrap();
            var second = RuntimePipelineBootstrap.Bootstrap();
            m_CreatedDriverGameObject = first != null ? first.gameObject : null;

            Assert.AreSame(first, second, "A second Bootstrap() call must return the existing driver, not create a duplicate");
        }

        private static void CreateTestConfig(bool enableInBuilds, int port = 0)
        {
            var config = ScriptableObject.CreateInstance<RuntimePipelineConfig>();
            config.enableInBuilds = enableInBuilds;
            config.port = port;
            config.Save();
            Object.DestroyImmediate(config);
        }
    }
}
