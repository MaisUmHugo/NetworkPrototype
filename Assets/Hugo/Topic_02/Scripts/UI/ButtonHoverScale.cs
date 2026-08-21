using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NetworkPrototype.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Min(1f)] private float hoverScale;
        [SerializeField, Min(0f)] private float transitionSpeed;

        private Button button;
        private Vector3 originalScale;
        private bool pointerInside;
        private bool initialized;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            pointerInside = false;
            RestoreOriginalScale();
        }

        private void Update()
        {
            bool canHover = pointerInside && button != null && button.isActiveAndEnabled && button.IsInteractable();
            Vector3 targetScale = canHover ? originalScale * hoverScale : originalScale;

            if (transitionSpeed <= 0f)
            {
                transform.localScale = targetScale;
                return;
            }

            float blend = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
            Vector3 nextScale = Vector3.Lerp(transform.localScale, targetScale, blend);
            transform.localScale = (nextScale - targetScale).sqrMagnitude <= 0.000001f
                ? targetScale
                : nextScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
        }

        private void OnDisable()
        {
            pointerInside = false;
            RestoreOriginalScale();
        }

        private void OnDestroy()
        {
            RestoreOriginalScale();
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            button = GetComponent<Button>();
            originalScale = transform.localScale;
            initialized = true;
        }

        private void RestoreOriginalScale()
        {
            if (initialized)
            {
                transform.localScale = originalScale;
            }
        }
    }
}
