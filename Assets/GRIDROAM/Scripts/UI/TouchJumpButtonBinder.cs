using Gridroam.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Binds the On-Foot touch "Atla" (Jump / Parkour Vault / Exit Pool) button to InputHub.TriggerTouchJump().
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TouchJumpButtonBinder : MonoBehaviour
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
                InputHub.Instance.TriggerTouchJump();
            }
        }
    }
}
