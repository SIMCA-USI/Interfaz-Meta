using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class VRKeyboardFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    private TMP_InputField inputField;
    private TouchScreenKeyboard keyboard;
    
    public static bool isAnyKeyboardOpen = false;

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
            isAnyKeyboardOpen = true;
        }
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (keyboard != null)
        {
            keyboard.active = false;
            keyboard = null;
        }
        isAnyKeyboardOpen = false;
    }

    void Update()
    {
        if (keyboard != null)
        {
            if (keyboard.status == TouchScreenKeyboard.Status.Visible)
            {
                inputField.text = keyboard.text;
                isAnyKeyboardOpen = true;
            }
            else if (keyboard.status == TouchScreenKeyboard.Status.Done || keyboard.status == TouchScreenKeyboard.Status.Canceled)
            {
                keyboard = null;
                isAnyKeyboardOpen = false;
            }
        }
    }
}
