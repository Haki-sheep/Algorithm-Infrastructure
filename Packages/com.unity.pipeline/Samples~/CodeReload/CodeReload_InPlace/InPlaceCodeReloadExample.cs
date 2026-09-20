using UnityEngine;
using Unity.Pipeline.CodeReload;

namespace Unity.Pipeline.Samples.CodeReload
{
    /// <summary>
    /// Example demonstrating in-place code reload editing workflow.
    ///
    /// Usage:
    /// 1. Add this component to a GameObject in the scene
    /// 2. Play the scene - you'll see the object rotating
    /// 3. Modify the Update() method below (try changing Vector3.up to Vector3.forward)
    /// 4. Run: reload_file Assets/Samples/CodeReload_InPlace/InPlaceCodeReloadExample.cs
    /// 5. See the changes take effect immediately without stopping play mode!
    ///
    /// Requirements:
    /// - Only use PUBLIC fields, properties, and methods
    /// - Private/internal members will cause validation errors
    /// </summary>
    public class InPlaceCodeReloadExample : MonoBehaviour
    {
        [Header("Code Reload Configuration")]
        public float rotationSpeed = 90f;
        public bool enableRotation = true;
        public Vector3 rotationAxis = Vector3.up;

        [Header("Movement Configuration")]
        public float moveSpeed = 2f;
        public bool enableMovement = false;

        [CodeReload]
        void Update()
        {
            // Edit this body live, then run: reload_file <this file>
            // Try changing rotationAxis (e.g. Vector3.up -> Vector3.forward) or the speed.
            if (enableRotation)
            {
                transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
            }

            if (enableMovement)
            {
                var movement = Mathf.Sin(Time.time) * moveSpeed;
                transform.position = new Vector3(movement, transform.position.y, transform.position.z);
            }
        }

        [CodeReload]
        public void ResetTransform()
        {
            // Another method you can code reload
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            Debug.Log("Transform reset via code reload!");
        }

        // Note: value-returning methods are not yet supported by in-place reload (void methods only),
        // so this one is a normal method.
        public float CalculateDistance(Vector3 targetPosition)
        {
            return Vector3.Distance(transform.position, targetPosition);
        }

        void Awake()
        {
            // Run the sample in a 640x480 window instead of fullscreen.
            Screen.SetResolution(640, 480, FullScreenMode.Windowed);
        }

        // Regular methods (not reloadable) - these work normally
        void Start()
        {
            Debug.Log($"InPlaceCodeReloadExample started. Try code reloading the Update method!");
            Debug.Log($"Command: reload_file {GetType().Name}.cs");
        }

        public void TriggerResetFromUI()
        {
            // This calls the reloadable method
            ResetTransform();
        }
    }
}