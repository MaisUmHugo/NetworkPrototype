using UnityEngine;

namespace NetworkPrototype.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerLocomotionAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");

        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0.01f)] private float maxGroundSpeed = 5f;
        [SerializeField, Min(0f)] private float parameterDampTime = 0.12f;
        [SerializeField, Min(0.01f)] private float teleportDistance = 3f;
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [SerializeField, Min(0f)] private float turnDeadZone = 0.02f;

        private Vector3 lastPosition;
        private bool hasMotionSample;

        private void Awake()
        {
            if (characterAnimator != null)
            {
                characterAnimator.applyRootMotion = false;
            }
        }

        private void OnEnable()
        {
            ResetMotionSample();
        }

        private void LateUpdate()
        {
            if (characterAnimator == null || !characterAnimator.isActiveAndEnabled)
            {
                ResetMotionSample();
                return;
            }

            Vector3 currentPosition = transform.position;
            if (!hasMotionSample)
            {
                lastPosition = currentPosition;
                hasMotionSample = true;
                SetSpeed(0f);
                return;
            }

            Vector3 displacement = currentPosition - lastPosition;
            lastPosition = currentPosition;
            displacement.y = 0f;

            if (displacement.sqrMagnitude >= teleportDistance * teleportDistance)
            {
                SetSpeed(0f);
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= Mathf.Epsilon)
            {
                return;
            }

            Vector3 worldVelocity = displacement / deltaTime;
            float groundSpeed = worldVelocity.magnitude;
            float normalizedSpeed = Mathf.Clamp01(groundSpeed / maxGroundSpeed);

            if (visualRoot != null && groundSpeed > turnDeadZone)
            {
                Quaternion targetRotation = Quaternion.LookRotation(worldVelocity, Vector3.up);
                visualRoot.rotation = Quaternion.RotateTowards(
                    visualRoot.rotation,
                    targetRotation,
                    turnSpeed * deltaTime);
            }

            SetSpeed(normalizedSpeed);
        }

        public void ResetMotionSample()
        {
            lastPosition = transform.position;
            hasMotionSample = false;
        }

        private void SetSpeed(float normalizedSpeed)
        {
            float deltaTime = Time.deltaTime;
            characterAnimator.SetFloat(SpeedId, normalizedSpeed, parameterDampTime, deltaTime);
        }
    }
}
