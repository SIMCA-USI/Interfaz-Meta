using UnityEngine;

/// <summary>
/// Mapea los controles del mando Meta Quest a funciones del vehículo.
/// Lee el input del mando derecho (joystick, botones, grip) y envía
/// comandos al FlaskApiClient.
/// 
/// Se adjunta a un objeto persistente en la escena (ej: [UI Panels] o el XR Rig).
/// 
/// Mapeo de controles:
///   Joystick Derecho (X/Y) → Control velocidad/dirección
///   Botón B (derecho)      → Toggle VR Mode ON/OFF
///   Grip Derecho            → Toggle Emergency Stop
///   Botón A (derecho)       → Toggle VR↔MR passthrough (local)
///   Trigger Derecho          → Click en paneles UI (manejado por XR Interaction)
/// </summary>
public class QuestControllerHandler : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Deadzone del joystick (igual que en la web: 0.15)")]
    public float deadzone = 0.15f;

    [Tooltip("Enviar joystick cada N segundos (para no saturar la red)")]
    public float joystickSendRate = 0.05f; // 20 Hz

    // ── Estado ──
    bool _vrModeActive = false;
    bool _emergencyStopActive = false;
    float _lastJoystickSend = 0f;

    // ── OVR Input ──
    // Usamos las claves de OVRInput del Meta XR SDK
    // Si OVRInput no está disponible, usamos Input.GetAxis como fallback

    void Update()
    {
        if (FlaskApiClient.Instance == null || !FlaskApiClient.Instance.isConnected) return;

        HandleJoystick();
        HandleButtons();
    }

    // =====================================================================
    // JOYSTICK DERECHO → Velocidad/Dirección del vehículo
    // =====================================================================

    void HandleJoystick()
    {
        // Leer joystick derecho
        Vector2 stick = Vector2.zero;

        #if UNITY_ANDROID && !UNITY_EDITOR
        // En Quest: usar OVRInput
        try
        {
            stick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        }
        catch
        {
            // OVRInput no disponible, usar fallback
            stick = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        }
        #else
        // En PC/Editor: usar ejes estándar del teclado/gamepad
        stick = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        #endif

        // Aplicar deadzone
        if (Mathf.Abs(stick.x) < deadzone) stick.x = 0;
        if (Mathf.Abs(stick.y) < deadzone) stick.y = 0;

        // Solo enviar si el joystick se ha movido y es hora
        if ((stick.x != 0 || stick.y != 0) && Time.time - _lastJoystickSend >= joystickSendRate)
        {
            _lastJoystickSend = Time.time;

            // Solo enviar si VR Mode está activo y no hay emergencia
            if (_vrModeActive && !_emergencyStopActive)
            {
                FlaskApiClient.Instance.SendJoystick(stick.x, stick.y);
            }
        }
    }

    // =====================================================================
    // BOTONES
    // =====================================================================

    void HandleButtons()
    {
        // Botón B (derecho) → Toggle VR Mode
        #if UNITY_ANDROID && !UNITY_EDITOR
        bool bPressed = OVRInput.GetDown(OVRInput.Button.Two); // B button
        #else
        bool bPressed = Input.GetKeyDown(KeyCode.V); // V = VR Mode en PC
        #endif

        if (bPressed)
        {
            _vrModeActive = !_vrModeActive;
            FlaskApiClient.Instance.SendVrMode(_vrModeActive);
            Debug.Log($"[QuestController] VR Mode: {(_vrModeActive ? "ON" : "OFF")}");
        }

        // Grip Derecho → Toggle Emergency Stop
        #if UNITY_ANDROID && !UNITY_EDITOR
        bool gripPressed = OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger);
        #else
        bool gripPressed = Input.GetKeyDown(KeyCode.Space); // Space = Emergency en PC
        #endif

        if (gripPressed)
        {
            _emergencyStopActive = !_emergencyStopActive;
            FlaskApiClient.Instance.SendVrEmergency(_emergencyStopActive);
            Debug.Log($"[QuestController] Emergency Stop: {(_emergencyStopActive ? "ACTIVADO" : "liberado")}");
        }

        // Botón A (derecho) → Toggle VR/MR Passthrough
        #if UNITY_ANDROID && !UNITY_EDITOR
        bool aPressed = OVRInput.GetDown(OVRInput.Button.One); // A button
        #else
        bool aPressed = Input.GetKeyDown(KeyCode.P); // P = Passthrough en PC
        #endif

        if (aPressed)
        {
            // Passthrough es local, no se envía al backend
            TogglePassthrough();
        }
    }

    // =====================================================================
    // PASSTHROUGH TOGGLE
    // =====================================================================

    void TogglePassthrough()
    {
        // Buscar el componente OVRPassthroughLayer en la escena
        var passthrough = FindFirstObjectByType<OVRPassthroughLayer>();
        if (passthrough != null)
        {
            passthrough.hidden = !passthrough.hidden;
            Debug.Log($"[QuestController] Passthrough: {(passthrough.hidden ? "OFF" : "ON")}");

            // Cambiar el fondo de la cámara principal
            Camera cam = Camera.main;
            if (cam != null)
            {
                if (!passthrough.hidden)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.clear;
                }
                else
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.01f, 0.01f, 0.03f);
                }
            }
        }
        else
        {
            Debug.LogWarning("[QuestController] OVRPassthroughLayer no encontrado. Passthrough no disponible.");
        }
    }
}
