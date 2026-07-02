using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class VRKeyboardFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    private TMP_InputField inputField;
    private TouchScreenKeyboard keyboard;

    void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
        if (inputField != null)
        {
            inputField.shouldHideMobileInput = false;
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (inputField != null && (keyboard == null || !keyboard.active))
        {
            keyboard = TouchScreenKeyboard.Open(inputField.text, TouchScreenKeyboardType.Default);
        }
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (keyboard != null && keyboard.active)
        {
            keyboard.active = false;
        }
    }

    void Update()
    {
        if (keyboard != null && keyboard.status == TouchScreenKeyboard.Status.Visible)
        {
            inputField.text = keyboard.text;
        }
    }
}
