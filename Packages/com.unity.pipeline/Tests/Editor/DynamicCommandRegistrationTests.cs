using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor;
using UnityEditor;
using UnityEngine.TestTools;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Tests for registering commands dynamically, without a [CliCommand] attribute.
    /// A dynamic registration must be indistinguishable from an attribute-declared one:
    /// same metadata, same [CliArg] parameter discovery, same execution path.
    /// </summary>
    public class DynamicCommandRegistrationTests
    {
        private readonly List<string> m_Registered = new List<string>();

        [SetUp]
        public void SetUp()
        {
            CommandRegistry.SetDiscovery(new TypeCacheCommandDiscovery());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // A play-mode test that fails mid-session would otherwise leave the Editor playing and
            // poison every later test.
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();

            // Dynamic registrations outlive a test; leaking one would corrupt every later
            // test that enumerates the registry.
            foreach (var name in m_Registered)
                CommandRegistry.UnregisterCommand(name);
            m_Registered.Clear();
        }

        /// <summary>Register a command and remember its name so TearDown can clean it up.</summary>
        private void Register(string name, string description, Delegate handler,
            bool mainThreadRequired = true, bool runtimeOnly = false, string[] tags = null)
        {
            CommandRegistry.RegisterCommand(name, description, handler, mainThreadRequired, runtimeOnly, tags);
            m_Registered.Add(name);
        }

        [Test]
        public void RegisterCommand_StaticHandler_IsDiscoveredWithItsMetadata()
        {
            Register("test_dynamic_add", "Add two numbers",
                new Func<int, int, int>(DynamicHandlers.Add),
                mainThreadRequired: false, runtimeOnly: true, tags: new[] { "test/dynamic" });

            var command = CommandRegistry.DiscoverCommands().FirstOrDefault(c => c.Name == "test_dynamic_add");

            Assert.IsNotNull(command, "Dynamically registered command should be discoverable");
            Assert.AreEqual("Add two numbers", command.Description);
            Assert.IsFalse(command.MainThreadRequired, "MainThreadRequired should come from the register call");
            Assert.IsTrue(command.RuntimeOnly, "RuntimeOnly should come from the register call");
            CollectionAssert.AreEqual(new[] { "test/dynamic" }, command.Tags);
        }

        [Test]
        public void RegisterCommand_OmittedTags_YieldsEmptyTagList()
        {
            Register("test_dynamic_untagged", "No tags", new Action(DynamicHandlers.Noop));

            var command = CommandRegistry.DiscoverCommands().First(c => c.Name == "test_dynamic_untagged");

            Assert.IsNotNull(command.Tags, "Tags should never be null");
            Assert.AreEqual(0, command.Tags.Count);
        }

        [Test]
        public void RegisterCommand_HandlerParameters_ComeFromCliArgAttributes()
        {
            Register("test_dynamic_add", "Add two numbers",
                new Func<int, int, int>(DynamicHandlers.Add), tags: new[] { "test/dynamic" });

            var command = CommandRegistry.DiscoverCommands().First(c => c.Name == "test_dynamic_add");

            Assert.AreEqual(2, command.Parameters.Count);

            var left = command.Parameters[0];
            Assert.AreEqual("left", left.Name);
            Assert.AreEqual("Left operand", left.Description);
            Assert.IsTrue(left.Required, "Required should be read from the [CliArg] attribute");
            Assert.AreEqual(typeof(int), left.ParameterType);

            var right = command.Parameters[1];
            Assert.AreEqual("right", right.Name);
            Assert.AreEqual("Right operand", right.Description);
            Assert.IsFalse(right.Required, "A parameter with a default value is optional");
            Assert.AreEqual(10, right.DefaultValue);
        }

        [Test]
        public void RegisterCommand_DerivesPackageFromHandlerAssembly()
        {
            Register("test_dynamic_untagged", "No tags", new Action(DynamicHandlers.Noop));

            var command = CommandRegistry.DiscoverCommands().First(c => c.Name == "test_dynamic_untagged");

            Assert.AreEqual("Unity.Pipeline.Tests.Editor", command.Package);
        }

        [Test]
        public void RegisterCommand_InstanceHandler_CarriesTheBoundInstanceAsTarget()
        {
            var echo = new Echoer("prefix");

            Register("test_dynamic_echo", "Echo a message",
                new Func<string, string>(echo.Echo), tags: new[] { "test/dynamic" });

            var command = CommandRegistry.DiscoverCommands().First(c => c.Name == "test_dynamic_echo");

            Assert.AreSame(echo, command.Target,
                "An instance-method delegate must carry its receiver so the invoke site can bind it");
        }

        [Test]
        public void RegisterCommand_StaticHandler_HasNullTarget()
        {
            Register("test_dynamic_untagged", "No tags", new Action(DynamicHandlers.Noop));

            var command = CommandRegistry.DiscoverCommands().First(c => c.Name == "test_dynamic_untagged");

            Assert.IsNull(command.Target, "A static handler has no receiver");
        }

        [Test]
        public async Task RegisterCommand_InstanceHandler_ExecutesThroughApiExec()
        {
            var echo = new Echoer("prefix");
            Register("test_dynamic_echo", "Echo a message",
                new Func<string, string>(echo.Echo),
                mainThreadRequired: false, tags: new[] { "test/dynamic" });

            var server = new TestEditorPipelineServer();
            server.Start();
            var client = new Unity.Pipeline.Tests.Runtime.PipelineClient(server);
            try
            {
                var response = await client.ExecuteCommandAsync("test_dynamic_echo", new { message = "hello" });

                Assert.IsTrue(response.IsSuccess,
                    $"Exec should succeed, got: {response.Error} {response.RawResponse}");
                var json = JObject.Parse(response.RawResponse);
                Assert.AreEqual("prefix:hello", json["result"]?.ToString(),
                    "The command must be invoked on the bound instance, not on a null receiver");
            }
            finally
            {
                client.Dispose();
                server.Stop();
            }
        }

        [Test]
        public void RegisterCommand_NameAlreadyRegisteredDynamically_Throws()
        {
            Register("test_dynamic_untagged", "No tags", new Action(DynamicHandlers.Noop));

            Assert.Throws<InvalidOperationException>(() =>
                CommandRegistry.RegisterCommand("test_dynamic_untagged", "Duplicate",
                    new Action(DynamicHandlers.Noop)));
        }

        [Test]
        public void RegisterCommand_NameOfAttributeCommand_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CommandRegistry.RegisterCommand("log_editor", "Shadowing a shipped command",
                    new Action(DynamicHandlers.Noop)));
        }

        [Test]
        public void RegisterCommand_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                CommandRegistry.RegisterCommand(null, "Description", new Action(DynamicHandlers.Noop)));
            Assert.Throws<ArgumentNullException>(() =>
                CommandRegistry.RegisterCommand("test_dynamic_null", null, new Action(DynamicHandlers.Noop)));
            Assert.Throws<ArgumentNullException>(() =>
                CommandRegistry.RegisterCommand("test_dynamic_null", "Description", null));
        }

        [Test]
        public void RegisterCommand_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                CommandRegistry.RegisterCommand("   ", "Description", new Action(DynamicHandlers.Noop)));
        }

        [Test]
        public void UnregisterCommand_RemovesTheCommandFromDiscovery()
        {
            Register("test_dynamic_untagged", "No tags", new Action(DynamicHandlers.Noop));
            Assert.IsTrue(CommandRegistry.DiscoverCommands().Any(c => c.Name == "test_dynamic_untagged"),
                "Precondition: the command is registered");

            var removed = CommandRegistry.UnregisterCommand("test_dynamic_untagged");

            Assert.IsTrue(removed, "Unregistering a dynamic command should report success");
            Assert.IsFalse(CommandRegistry.DiscoverCommands().Any(c => c.Name == "test_dynamic_untagged"),
                "Command should be gone from discovery");
        }

        [Test]
        public void UnregisterCommand_UnknownName_ReturnsFalse()
        {
            Assert.IsFalse(CommandRegistry.UnregisterCommand("test_dynamic_never_registered"));
        }

        [Test]
        public void UnregisterCommand_AttributeCommand_ReturnsFalseAndKeepsIt()
        {
            var removed = CommandRegistry.UnregisterCommand("log_editor");

            Assert.IsFalse(removed, "Attribute-declared commands are not removable");
            Assert.IsTrue(CommandRegistry.DiscoverCommands().Any(c => c.Name == "log_editor"),
                "The attribute command must survive the unregister attempt");
        }

        [Test]
        public void RegisterCommand_SurvivesClearCache()
        {
            Register("test_dynamic_untagged", "No tags", new Action(DynamicHandlers.Noop));

            CommandRegistry.ClearCache();

            Assert.IsTrue(CommandRegistry.DiscoverCommands().Any(c => c.Name == "test_dynamic_untagged"),
                "Dynamic registrations must survive a cache clear (e.g. a discovery-mechanism swap)");
        }

        [Test]
        public void RegisterCommand_SurvivesSetDiscovery()
        {
            Register("test_dynamic_untagged", "No tags", new Action(DynamicHandlers.Noop));

            CommandRegistry.SetDiscovery(new TypeCacheCommandDiscovery());

            Assert.IsTrue(CommandRegistry.DiscoverCommands().Any(c => c.Name == "test_dynamic_untagged"),
                "Dynamic registrations must survive SetDiscovery");
        }

        [Test]
        public void RegisterCommand_DoesNotDisturbAttributeCommands()
        {
            var before = CommandRegistry.DiscoverCommands().Count(c => c.Name == "log_editor");

            Register("test_dynamic_untagged", "No tags", new Action(DynamicHandlers.Noop));

            Assert.AreEqual(before, CommandRegistry.DiscoverCommands().Count(c => c.Name == "log_editor"),
                "Registering a dynamic command must not duplicate or drop attribute commands");
        }

        [UnityTest]
        public IEnumerator RegisterCommand_InPlayMode_IsClearedOnPlayModeExit()
        {
            yield return new EnterPlayMode();

            Register("test_dynamic_play_session", "Registered from a Play session",
                new Action(DynamicHandlers.Noop));

            yield return new ExitPlayMode();

            Assert.IsFalse(CommandRegistry.DiscoverCommands().Any(c => c.Name == "test_dynamic_play_session"),
                "A registration made in Play Mode must not outlive the session: whatever registered " +
                "it (Awake/OnEnable) runs again on the next entry, and the surviving delegate would " +
                "be bound to that session's destroyed object.");

            // The reported symptom: with domain reload disabled, the second entry threw because the
            // first entry's registration was still there.
            yield return new EnterPlayMode();

            Assert.DoesNotThrow(
                () => Register("test_dynamic_play_session", "Registered from the next Play session",
                    new Action(DynamicHandlers.Noop)),
                "Re-registering the same name on a later Play Mode entry must not throw");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RegisterCommand_InEditMode_SurvivesAPlaySession()
        {
            Register("test_dynamic_edit_scoped", "Registered from Edit Mode",
                new Action(DynamicHandlers.Noop));

            yield return new EnterPlayMode();
            yield return new ExitPlayMode();

            Assert.IsTrue(CommandRegistry.DiscoverCommands().Any(c => c.Name == "test_dynamic_edit_scoped"),
                "A registration made in Edit Mode must survive a Play session: an " +
                "[InitializeOnLoadMethod] runs once per domain, so nothing would restore it.");
        }

        /// <summary>Static handlers used as dynamic command implementations.</summary>
        private static class DynamicHandlers
        {
            public static int Add(
                [CliArg("left", "Left operand", Required = true)] int left,
                [CliArg("right", "Right operand")] int right = 10)
            {
                return left + right;
            }

            public static void Noop()
            {
            }
        }

        /// <summary>Instance handler, to verify the receiver survives registration and invocation.</summary>
        private sealed class Echoer
        {
            private readonly string m_Prefix;

            public Echoer(string prefix)
            {
                m_Prefix = prefix;
            }

            public string Echo([CliArg("message", "The message to echo", Required = true)] string message)
            {
                return $"{m_Prefix}:{message}";
            }
        }
    }
}
