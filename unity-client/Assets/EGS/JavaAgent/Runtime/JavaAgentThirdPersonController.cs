using UnityEngine;

namespace EGS.JavaAgent.Runtime
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class JavaAgentThirdPersonController : MonoBehaviour
    {
        [SerializeField] private Camera followCamera;
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7.5f;
        [SerializeField] private float rotationSpeed = 540f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 2.2f, -4.5f);
        [SerializeField] private float cameraFollowSharpness = 12f;

        private CharacterController _controller;
        private float _verticalVelocity;

        public Camera FollowCamera
        {
            get => followCamera;
            set => followCamera = value;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (followCamera == null)
            {
                followCamera = Camera.main;
            }
        }

        private void Update()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 input = new Vector3(horizontal, 0f, vertical);
            input = Vector3.ClampMagnitude(input, 1f);

            Vector3 cameraForward = Vector3.forward;
            Vector3 cameraRight = Vector3.right;
            if (followCamera != null)
            {
                cameraForward = Vector3.ProjectOnPlane(followCamera.transform.forward, Vector3.up).normalized;
                cameraRight = Vector3.ProjectOnPlane(followCamera.transform.right, Vector3.up).normalized;
            }

            Vector3 move = cameraForward * input.z + cameraRight * input.x;
            if (move.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            if (_controller.isGrounded && Input.GetButtonDown("Jump"))
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _verticalVelocity += gravity * Time.deltaTime;
            float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;
            Vector3 velocity = move * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (followCamera == null)
            {
                return;
            }

            Vector3 targetPosition = transform.TransformPoint(cameraOffset);
            followCamera.transform.position = Vector3.Lerp(
                followCamera.transform.position,
                targetPosition,
                1f - Mathf.Exp(-cameraFollowSharpness * Time.deltaTime)
            );
            followCamera.transform.LookAt(transform.position + Vector3.up * 1.4f);
        }
    }
}
