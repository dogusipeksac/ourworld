using OurWorld.Input;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Binds the On-Foot touch "Vur" (Punch) button directly to InputHub.TriggerTouchAttack().
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TouchAttackButtonBinder : MonoBehaviour
    {
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }

        private void HandleClick()
        {
            if (InputHub.Instance != null)
            {
                InputHub.Instance.TriggerTouchAttack();
            }
        }
    }
}
