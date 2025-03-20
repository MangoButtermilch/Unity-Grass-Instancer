using UnityEngine;

namespace Acetix.Camera
{

    /// <summary>
    /// From https://gist.github.com/FreyaHolmer/650ecd551562352120445513efa1d952
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class FlyCamera : MonoBehaviour
    {
        public float acceleration = 50; // how fast you accelerate
        public float accSprintMultiplier = 4; // how much faster you go when "sprinting"
        public float lookSensitivity = 1; // mouse look sensitivity
        public float dampingCoefficient = 5; // how quickly you break to a halt after you stop your input
        public float rotationSmoothing = 10f; // how smooth the mouse movement is
        public bool focusOnEnable = true; // whether or not to focus and lock cursor immediately on enable

        Vector3 velocity; // current velocity
        Vector2 currentMouseDelta; // current smoothed mouse delta
        Vector2 targetMouseDelta; // target mouse delta
        Quaternion targetRotation; // target rotation for smooth movement

        static bool Focused
        {
            get => Cursor.lockState == CursorLockMode.Locked;
            set
            {
                Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = value == false;
            }
        }

        void OnEnable()
        {
            if (focusOnEnable) Focused = true;
            targetRotation = transform.rotation; // Initialize target rotation
        }

        void OnDisable() => Focused = false;

        void Update()
        {
            // Input
            if (Focused)
                UpdateInput();
            else if (Input.GetMouseButtonDown(0))
                Focused = true;

            // Physics
            velocity = Vector3.Lerp(velocity, Vector3.zero, dampingCoefficient * Time.deltaTime);
            transform.position += velocity * Time.deltaTime;

            // Smooth rotation interpolation
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothing * Time.deltaTime);
        }

        void UpdateInput()
        {
            // Position
            velocity += GetAccelerationVector() * Time.deltaTime;

            // Rotation: Smooth Mouse Movement
            targetMouseDelta = lookSensitivity * new Vector2(Input.GetAxis("Mouse X"), -Input.GetAxis("Mouse Y"));
            currentMouseDelta = Vector2.Lerp(currentMouseDelta, targetMouseDelta, rotationSmoothing * Time.deltaTime);

            Quaternion horiz = Quaternion.AngleAxis(currentMouseDelta.x, Vector3.up);
            Quaternion vert = Quaternion.AngleAxis(currentMouseDelta.y, Vector3.right);

            // Update target rotation
            targetRotation = horiz * targetRotation * vert;

            // Leave cursor lock
            if (Input.GetKeyDown(KeyCode.Escape))
                Focused = false;
        }

        Vector3 GetAccelerationVector()
        {
            Vector3 moveInput = default;

            void AddMovement(KeyCode key, Vector3 dir)
            {
                if (Input.GetKey(key))
                    moveInput += dir;
            }

            AddMovement(KeyCode.W, Vector3.forward);
            AddMovement(KeyCode.S, Vector3.back);
            AddMovement(KeyCode.D, Vector3.right);
            AddMovement(KeyCode.A, Vector3.left);
            AddMovement(KeyCode.Space, Vector3.up);
            AddMovement(KeyCode.LeftControl, Vector3.down);
            Vector3 direction = transform.TransformVector(moveInput.normalized);

            if (Input.GetKey(KeyCode.LeftShift))
                return direction * (acceleration * accSprintMultiplier); // "sprinting"
            if (Input.GetKey(KeyCode.CapsLock))
                return direction * (acceleration * accSprintMultiplier) * 10; // "fast sprint"
            return direction * acceleration; // "walking"
        }
    }
}