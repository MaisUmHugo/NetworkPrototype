using UnityEngine;

namespace NetworkPrototype.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerLocomotionAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int MoveXId = Animator.StringToHash("MoveX");
        private static readonly int MoveZId = Animator.StringToHash("MoveZ");

        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Transform motionSpace;
        [SerializeField, Min(0.01f)] private float maxGroundSpeed = 5f;
        [SerializeField, Min(0f)] private float parameterDampTime = 0.12f;
        [SerializeField, Min(0.01f)] private float teleportDistance = 3f;
        [SerializeField, Min(0f)] private float directionDeadZone = 0.02f;

        private Vector3 lastPosition;
        private bool hasMotionSample;

        private void Awake()
        {
            if (motionSpace == null)
            {
                motionSpace = transform;
            }

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
                SetParameters(Vector2.zero, 0f);
                return;
            }

            Vector3 displacement = currentPosition - lastPosition;
            lastPosition = currentPosition;
            displacement.y = 0f;

            if (displacement.sqrMagnitude >= teleportDistance * teleportDistance)
            {
                SetParameters(Vector2.zero, 0f);
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

            Vector3 localVelocity = motionSpace.InverseTransformDirection(worldVelocity);
            Vector2 localDirection = new Vector2(localVelocity.x, localVelocity.z);
            if (localDirection.sqrMagnitude > directionDeadZone * directionDeadZone)
            {
                localDirection.Normalize();
            }
            else
            {
                localDirection = Vector2.zero;
            }

            SetParameters(localDirection, normalizedSpeed);
        }

        public void ResetMotionSample()
        {
            lastPosition = transform.position;
            hasMotionSample = false;
        }

        private void SetParameters(Vector2 localDirection, float normalizedSpeed)
        {
            float deltaTime = Time.deltaTime;
            characterAnimator.SetFloat(SpeedId, normalizedSpeed, parameterDampTime, deltaTime);
            characterAnimator.SetFloat(MoveXId, localDirection.x, parameterDampTime, deltaTime);
            characterAnimator.SetFloat(MoveZId, localDirection.y, parameterDampTime, deltaTime);
        }
    }
}