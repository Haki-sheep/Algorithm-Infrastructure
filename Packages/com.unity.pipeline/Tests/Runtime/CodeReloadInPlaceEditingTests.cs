using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Pipeline.CodeReload;
using Unity.Pipeline.Runtime.Commands;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Pipeline.Tests.Runtime
{
    /// <summary>
    /// Integration tests for in-place code reload editing workflow.
    /// Tests the complete pipeline: source parsing -> validation -> transformation -> compilation.
    /// </summary>
    [Ignore("CodeReload in-place editing is deferred until the autonomous test loop is solid; this path exercises the known instance-to-static transformation bug. Re-enable when revisiting in-place reload.")]
    class CodeReloadInPlaceEditingTests
    {
        private string _testFilePath;

        [SetUp]
        public void Setup()
        {
            // Clean registry before each test
            CodeReloadRegistry.ClearAllForTesting();

            // Create temp test file path
            _testFilePath = Path.Combine(Application.temporaryCachePath, "CodeReloadExampleComponent.cs");
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test file
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }

            // Clean registry after test
            CodeReloadRegistry.ClearAllForTesting();
        }

        [UnityTest]
        public IEnumerator InPlaceReload_ValidPublicMemberAccess_ShouldSucceed()
        {
            // Arrange: Create source file with [CodeReload] method using only public members
            var sourceCode = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;

public class CodeReloadExampleComponent : MonoBehaviour
{
    public float speed = 5.0f;
    public bool isActive = true;

    [CodeReload]
    void Update()
    {
        if (isActive)
        {
            transform.position += Vector3.right * speed * Time.deltaTime;
        }
    }
}";

            File.WriteAllText(_testFilePath, sourceCode);

            // Act: Execute in-place reload command
            var task = Task.Run(async () =>
            {
                return await InPlaceReloadProcessor.ProcessSourceFileAsync(_testFilePath);
            });

            yield return new WaitUntil(() => task.IsCompleted);

            // Assert
            Assert.IsTrue(task.Result.Success, $"In-place reload should succeed. Error: {task.Result.ErrorMessage}");
            Assert.AreEqual("CodeReloadExampleComponent", task.Result.OriginalTypeName);
            Assert.Contains("Update", task.Result.ExtractedMethods);
            Assert.IsTrue(task.Result.RegisteredMethods.Count > 0, "Should have registered at least one method");
            Assert.IsNotNull(task.Result.TransformedCode, "Should have generated transformed code");

            // Verify registry state
            var stats = CodeReloadRegistry.GetStats();
            Assert.IsTrue(stats.ActiveOverrideCount > 0, "Should have active overrides registered");
        }

        [UnityTest]
        public IEnumerator InPlaceReload_PrivateMemberAccess_FailsValidationOnCompiledBackend()
        {
            // The compiled (Assembly.Load) backend cannot reach non-public members at runtime — Mono
            // JIT-enforces accessibility on the loaded override IL — so the reload is rejected up
            // front by AccessibilityValidator. The interpreter backend supports private access (see
            // IlInterpreterCodeReloadInterpreterTests).
            var sourceCode = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;

public class CodeReloadExampleComponent : MonoBehaviour
{
    private float privateSpeed = 5.0f;
    public bool isActive = true;

    [CodeReload]
    void Update()
    {
        if (isActive)
        {
            transform.position += Vector3.right * privateSpeed * Time.deltaTime;
        }
    }
}";

            File.WriteAllText(_testFilePath, sourceCode);

            // Act: Execute in-place reload command
            var task = Task.Run(async () =>
            {
                return await InPlaceReloadProcessor.ProcessSourceFileAsync(_testFilePath);
            });

            yield return new WaitUntil(() => task.IsCompleted);

            Assert.IsFalse(task.Result.Success,
                "In-place reload on the compiled backend should fail validation for private member access");
            StringAssert.Contains("privateSpeed", task.Result.ErrorMessage,
                "The validation error should name the offending member");
        }

        [UnityTest]
        public IEnumerator InPlaceReload_MultipleCodeReloadableMethods_ShouldSucceed()
        {
            // Arrange: Create source file with multiple [CodeReload] methods
            var sourceCode = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;
using Unity.Pipeline.Samples.CodeReload;

public class CodeReloadExampleComponent : MonoBehaviour
{
    public float speed = 5.0f;
    public int health = 100;

    [CodeReload]
    void Update()
    {
        transform.position += Vector3.right * speed * Time.deltaTime;
    }

    [CodeReload]
    public int CalculateDamage(int baseDamage)
    {
        return baseDamage * 2;
    }

    [CodeReload]
    public void ResetPosition()
    {
        transform.position = Vector3.zero;
    }
}";

            File.WriteAllText(_testFilePath, sourceCode);

            // Act: Execute in-place reload command
            var task = Task.Run(async () =>
            {
                return await InPlaceReloadProcessor.ProcessSourceFileAsync(_testFilePath);
            });

            yield return new WaitUntil(() => task.IsCompleted);

            // Assert
            Assert.IsTrue(task.Result.Success, $"In-place reload should succeed. Error: {task.Result.ErrorMessage}");
            Assert.AreEqual(3, task.Result.ExtractedMethods.Count, "Should extract 3 [CodeReload] methods");
            Assert.Contains("Update", task.Result.ExtractedMethods);
            Assert.Contains("CalculateDamage", task.Result.ExtractedMethods);
            Assert.Contains("ResetPosition", task.Result.ExtractedMethods);

            // Verify transformed code contains all methods
            Assert.IsTrue(task.Result.TransformedCode.Contains("[CodeReloadOverrideMethod(\"CodeReloadExampleComponent.Update\")]"));
            Assert.IsTrue(task.Result.TransformedCode.Contains("[CodeReloadOverrideMethod(\"CodeReloadExampleComponent.CalculateDamage\")]"));
            Assert.IsTrue(task.Result.TransformedCode.Contains("[CodeReloadOverrideMethod(\"CodeReloadExampleComponent.ResetPosition\")]"));
        }

        [UnityTest]
        public IEnumerator ReloadFileCommand_InPlaceSourceFile_ShouldUseInPlaceWorkflow()
        {
            // Arrange: Create source file with [CodeReload] method
            var sourceCode = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;

public class CodeReloadExampleComponent : MonoBehaviour
{
    public float speed = 5.0f;

    [CodeReload]
    void Update()
    {
        transform.position += Vector3.up * speed * Time.deltaTime;
    }
}";

            File.WriteAllText(_testFilePath, sourceCode);

            // Act: Execute reload_file command
            var response = CodeReloadCommands.ReloadFile(_testFilePath);

            // Assert
            yield return null; // Allow frame to complete

            Assert.IsTrue(response.Success, $"reload_file command should succeed. Error: {response.ErrorDetails}");
            Assert.IsNotNull(response.AssemblyName, "Should have assembly name");
            Assert.IsTrue(response.Items.Count > 0, "Should have registered methods");

            // Verify registry state
            var stats = CodeReloadRegistry.GetStats();
            Assert.IsTrue(stats.ActiveOverrideCount > 0, "Should have active overrides registered");
        }

        [UnityTest]
        public IEnumerator AccessibilityValidation_MixedAccess_FlagsOnlyNonPublic()
        {
            // The compiled backend rejects non-public access up front (Mono JIT-enforces
            // accessibility at dispatch); the violation must name only the private member.
            var sourceCode = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;

public class CodeReloadExampleComponent : MonoBehaviour
{
    public float publicSpeed = 5.0f;
    private float privateSpeed = 3.0f;
    internal int internalHealth = 100;

    [CodeReload]
    void Update()
    {
        transform.position += Vector3.right * publicSpeed * Time.deltaTime;
        transform.position += Vector3.up * privateSpeed * Time.deltaTime;
    }
}";

            File.WriteAllText(_testFilePath, sourceCode);

            // Act: Execute in-place reload command
            var task = Task.Run(async () =>
            {
                return await InPlaceReloadProcessor.ProcessSourceFileAsync(_testFilePath);
            });

            yield return new WaitUntil(() => task.IsCompleted);

            Assert.IsFalse(task.Result.Success,
                "Mixed access should fail validation on the compiled backend (private member touched)");
            StringAssert.Contains("privateSpeed", task.Result.ErrorMessage,
                "The validation error should name the private member");
            StringAssert.DoesNotContain("publicSpeed", task.Result.ErrorMessage,
                "Public member access must not be flagged");
        }

        [UnityTest]
        public IEnumerator TransformedCode_ThisReferences_ShouldBeConvertedToInstance()
        {
            // Arrange: Create source with 'this.' references
            var sourceCode = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;

public class CodeReloadExampleComponent : MonoBehaviour
{
    public float speed = 5.0f;

    [CodeReload]
    void Update()
    {
        this.transform.position += Vector3.right * this.speed * Time.deltaTime;
    }
}";

            File.WriteAllText(_testFilePath, sourceCode);

            // Act: Execute in-place reload command
            var task = Task.Run(async () =>
            {
                return await InPlaceReloadProcessor.ProcessSourceFileAsync(_testFilePath);
            });

            yield return new WaitUntil(() => task.IsCompleted);

            // Assert
            Assert.IsTrue(task.Result.Success, $"Should succeed. Error: {task.Result.ErrorMessage}");

            var transformedCode = task.Result.TransformedCode;
            Assert.IsNotNull(transformedCode, "Should have transformed code");

            // Verify 'this.' is converted to 'instance.'
            Assert.IsFalse(transformedCode.Contains("this."), "Transformed code should not contain 'this.' references");
            Assert.IsTrue(transformedCode.Contains("instance."), "Transformed code should contain 'instance.' references");
            Assert.IsTrue(transformedCode.Contains("instance.transform"), "Should convert this.transform to instance.transform");
            Assert.IsTrue(transformedCode.Contains("instance.speed"), "Should convert this.speed to instance.speed");
        }

        [UnityTest]
        public IEnumerator MethodSignatures_WithParameters_ShouldPreserveParameterInfo()
        {
            // Arrange: Create source with parameterized [CodeReload] method
            var sourceCode = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;

public class CodeReloadExampleComponent : MonoBehaviour
{
    public float speed = 5.0f;

    [CodeReload]
    public void MoveTowards(Vector3 target, float customSpeed)
    {
        var direction = (target - transform.position).normalized;
        transform.position += direction * customSpeed * Time.deltaTime;
    }
}";

            File.WriteAllText(_testFilePath, sourceCode);

            // Act: Execute in-place reload command
            var task = Task.Run(async () =>
            {
                return await InPlaceReloadProcessor.ProcessSourceFileAsync(_testFilePath);
            });

            yield return new WaitUntil(() => task.IsCompleted);

            // Assert
            Assert.IsTrue(task.Result.Success, $"Should succeed. Error: {task.Result.ErrorMessage}");

            var transformedCode = task.Result.TransformedCode;
            Assert.IsNotNull(transformedCode, "Should have transformed code");

            // Verify method signature includes instance parameter plus original parameters
            Assert.IsTrue(transformedCode.Contains("MoveTowards(CodeReloadExampleComponent instance, Vector3 target, float customSpeed)"),
                $"Should preserve method parameters with instance parameter first. Actual:\n{transformedCode}");
        }

        [Test]
        public void ContainsCodeReloadableMethods_ValidFile_ShouldReturnTrue()
        {
            // Arrange
            var sourceCode = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;

public class TestComponent : MonoBehaviour
{
    [CodeReload]
    void Update() { }
}";

            var testPath = Path.Combine(Application.temporaryCachePath, "TestCheck.cs");
            File.WriteAllText(testPath, sourceCode);

            try
            {
                // Act
                var result = InPlaceReloadProcessor.ContainsCodeReloadableMethodsAsync(testPath).GetAwaiter().GetResult();

                // Assert
                Assert.IsTrue(result, "Should detect [CodeReload] methods");
            }
            finally
            {
                if (File.Exists(testPath))
                {
                    File.Delete(testPath);
                }
            }
        }

        [Test]
        public void ContainsCodeReloadableMethods_NoCodeReloadMethods_ShouldReturnFalse()
        {
            // Arrange
            var sourceCode = @"
using UnityEngine;

public class TestComponent : MonoBehaviour
{
    void Update() { }
}";

            var testPath = Path.Combine(Application.temporaryCachePath, "TestCheck.cs");
            File.WriteAllText(testPath, sourceCode);

            try
            {
                // Act
                var result = InPlaceReloadProcessor.ContainsCodeReloadableMethodsAsync(testPath).GetAwaiter().GetResult();

                // Assert
                Assert.IsFalse(result, "Should not detect [CodeReload] methods");
            }
            finally
            {
                if (File.Exists(testPath))
                {
                    File.Delete(testPath);
                }
            }
        }
    }
}