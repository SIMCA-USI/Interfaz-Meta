using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Singleton HTTP — Centraliza toda la comunicación con el servidor Flask.
/// Se adjunta al mismo GameObject que la interfaz principal.
/// Endpoints basados en web_interface_pkg_8x8/web_node.py.
/// </summary>
public class FlaskApiClient : MonoBehaviour
{
    // ── Singleton ──
    public static FlaskApiClient Instance { get; private set; }

    // ── Configuración ──
    [Header("Conexión")]
    [Tooltip("IP del servidor Flask (se auto-detecta por UDP si ServerDiscovery está activo)")]
    public string serverIp = "10.170.183.110";

    [Tooltip("Puerto del servidor Flask (normalmente 5000 o 5050)")]
    public int serverPort = 5050;

    [Header("Polling")]
    [Tooltip("Intervalo de polling en segundos")]
    public float pollInterval = 0.5f;

    // ── Estado público (leído por la UI) ──
    [HideInInspector] public bool isConnected = false;
    [HideInInspector] public float speed = 0f;
    [HideInInspector] public int modeMission = 0; // 0=StandBy, 1=Auto, 2=Tele, 4=Nav2, 6=VR
    [HideInInspector] public float rollDeg = 0f;
    [HideInInspector] public float pitchDeg = 0f;
    [HideInInspector] public bool emergencyStopActive = false;
    [HideInInspector] public string emergencyReason = "None";
    [HideInInspector] public string waypointInfo = "";
    [HideInInspector] public float routeProgress = 0f;
    [HideInInspector] public int numCameras = 5;
    [HideInInspector] public List<string> availableMaps = new List<string>();
    [HideInInspector] public string currentTelemetryRaw = "";

    // ── Eventos (la UI se suscribe a estos) ──
    public event Action OnStateUpdated;
    public event Action OnConnectionLost;
    public event Action OnConnectionRestored;

    string BaseUrl => $"http://{serverIp}:{serverPort}";

    float _lastPollTime;
    bool _wasConnected = false;
    int _failCount = 0;
    const int MAX_FAILS_BEFORE_DISCONNECT = 6;

    // =====================================================================
    // LIFECYCLE
    // =====================================================================

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        Debug.Log($"[FlaskAPI] Inicializando. URL base: {BaseUrl}");
        Debug.Log($"[FlaskAPI] Esperando conexión con servidor Flask...");
        
        // Primer polling inmediato
        StartCoroutine(PollNumCameras());
        StartCoroutine(PollAvailableMaps());
    }

    void Update()
    {
        if (Time.time - _lastPollTime >= pollInterval)
        {
            _lastPollTime = Time.time;
            StartCoroutine(PollTelemetry());
            StartCoroutine(PollStatus());
            StartCoroutine(PollLandMeter());
            StartCoroutine(PollWaypointInfo());
            StartCoroutine(PollRouteProgress());
            StartCoroutine(PollUiState());
        }
    }

    // =====================================================================
    // POLLING COROUTINES (GET)
    // =====================================================================

    IEnumerator PollTelemetry()
    {
        using (var req = UnityWebRequest.Get(BaseUrl + "/get_telemetry"))
        {
            req.timeout = 3;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                MarkConnected();
                try
                {
                    currentTelemetryRaw = req.downloadHandler.text;
                    var json = JsonUtility.FromJson<TelemetryResponse>(req.downloadHandler.text);
                    if (json.status != null)
                    {
                        speed = json.status.speed;
                    }
                }
                catch (Exception e) 
                { 
                    Debug.LogWarning($"[FlaskAPI] Error parseando telemetry: {e.Message}"); 
                }
            }
            else
            {
                MarkFailed(req.error);
            }
        }
        
        OnStateUpdated?.Invoke();
    }

    IEnumerator PollStatus()
    {
        using (var req = UnityWebRequest.Get(BaseUrl + "/get_mode_mission_status"))
        {
            req.timeout = 3;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                MarkConnected();
                try
                {
                    var json = JsonUtility.FromJson<StatusIntResponse>(req.downloadHandler.text);
                    modeMission = json.status;
                }
                catch (Exception) { }
            }
        }
        OnStateUpdated?.Invoke();
    }

    IEnumerator PollLandMeter()
    {
        using (var req = UnityWebRequest.Get(BaseUrl + "/get_land_meter_status"))
        {
            req.timeout = 3;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var json = JsonUtility.FromJson<LandMeterResponse>(req.downloadHandler.text);
                    if (json.status != null)
                    {
                        rollDeg = json.status.x;
                        pitchDeg = json.status.y;
                    }
                }
                catch (Exception) { }
            }
        }
        OnStateUpdated?.Invoke();
    }

    IEnumerator PollWaypointInfo()
    {
        using (var req = UnityWebRequest.Get(BaseUrl + "/get_waypoint_info"))
        {
            req.timeout = 3;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var json = JsonUtility.FromJson<StatusStringResponse>(req.downloadHandler.text);
                    string rawStatus = json.status ?? "";
                    
                    // Eliminar etiquetas HTML para que se vea bien en VR
                    rawStatus = rawStatus.Replace("<div>", "").Replace("</div>", "\n").Trim();
                    
                    // Si tiene el div rojo de emergencia, lo limpiamos también
                    int emergIndex = rawStatus.IndexOf("<div style='margin-top: 12px;");
                    if (emergIndex >= 0)
                    {
                        rawStatus = rawStatus.Substring(0, emergIndex).Trim();
                    }
                    
                    waypointInfo = rawStatus;
                    
                    // Ya no parseamos el string HTML aquí, lo obtenemos de PollUiState que es más seguro.
                }
                catch (Exception) { }
            }
        }
        OnStateUpdated?.Invoke();
    }

    IEnumerator PollRouteProgress()
    {
        using (var req = UnityWebRequest.Get(BaseUrl + "/get_route_progress"))
        {
            req.timeout = 3;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var json = JsonUtility.FromJson<ProgressResponse>(req.downloadHandler.text);
                    routeProgress = json.progress;
                }
                catch (Exception) { }
            }
        }
        OnStateUpdated?.Invoke();
    }

    IEnumerator PollNumCameras()
    {
        using (var req = UnityWebRequest.Get(BaseUrl + "/get_num_cameras"))
        {
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var json = JsonUtility.FromJson<NumCamerasResponse>(req.downloadHandler.text);
                    numCameras = json.num_cameras;
                    Debug.Log($"[FlaskAPI] Cámaras detectadas: {numCameras}");
                }
                catch (Exception) { }
            }
        }
    }

    IEnumerator PollAvailableMaps()
    {
        using (var req = UnityWebRequest.Get(BaseUrl + "/get_available_maps"))
        {
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var json = JsonUtility.FromJson<AvailableMapsResponse>(req.downloadHandler.text);
                    if (json.map_names != null)
                    {
                        availableMaps = new List<string>(json.map_names);
                        Debug.Log($"[FlaskAPI] Mapas disponibles: {availableMaps.Count}");
                    }
                }
                catch (Exception) { }
            }
        }
    }

    [Header("UI State Sync")]
    public bool isRecording;
    public bool isPlayingMap;
    public bool speedTypeChecked;
    public bool signalOverrideChecked;
    public bool emergencyBrake;
    public int operationMode = -1;
    public string activeFollowType;
    public bool followmeWayPointChecked;
    public bool isVideoUnlocked;
    public string activeTab = "mission";

    [Serializable]
    class UiStateResponse
    {
        public string bRec;
        public string buttonPlayMap;
        public string speedTypeChecked;
        public string signalOverrideChecked;
        public bool emergencyBrake;
        public int operationMode;
        public string activeFollowType;
        public string followmeWayPointChecked;
        public string isVideoUnlocked;
        public bool system_emergency_stop;
        public string system_emergency_reason;
        public string activeTab;
    }

    IEnumerator PollUiState()
    {
        using (var req = UnityWebRequest.Get(BaseUrl + "/get_ui_state"))
        {
            req.timeout = 3;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var json = JsonUtility.FromJson<UiStateResponse>(req.downloadHandler.text);
                    if (json != null)
                    {
                        isRecording = (json.bRec == "true" || json.bRec == "True");
                        isPlayingMap = (json.buttonPlayMap == "true" || json.buttonPlayMap == "True");
                        speedTypeChecked = (json.speedTypeChecked == "true" || json.speedTypeChecked == "True");
                        signalOverrideChecked = (json.signalOverrideChecked == "true" || json.signalOverrideChecked == "True");
                        emergencyBrake = json.emergencyBrake;
                        operationMode = json.operationMode;
                        activeFollowType = json.activeFollowType;
                        followmeWayPointChecked = (json.followmeWayPointChecked == "true" || json.followmeWayPointChecked == "True");
                        isVideoUnlocked = (json.isVideoUnlocked == "true" || json.isVideoUnlocked == "True");
                        
                        emergencyStopActive = json.system_emergency_stop;
                        emergencyReason = json.system_emergency_reason ?? "None";
                        
                        if (!string.IsNullOrEmpty(json.activeTab))
                            activeTab = json.activeTab;
                    }
                }
                catch (Exception e) 
                {
                    Debug.Log($"[FlaskAPI] PollUiState Error: {e.Message}");
                }
            }
        }
        
        OnStateUpdated?.Invoke();
    }

    // =====================================================================
    // COMMAND METHODS (POST) — Llamados por la UI y por QuestControllerHandler
    // =====================================================================

    public void SendUpdateUiState(string key, string value)
    {
        StartCoroutine(PostJson("/api/update_ui_state", $"{{\"key\":\"{key}\",\"value\":\"{value}\"}}"));
    }

    /// <summary>Cambiar modo de misión: 0=StandBy, 1=Auto, 2=Tele, 4=Nav2, 6=VR</summary>
    public void SendModeMission(int mode)
    {
        StartCoroutine(PostJson("/publish_modo_mision", $"{{\"mode\":{mode}}}"));
    }

    /// <summary>Emergency brake HMI: 0=liberar, 1=activar</summary>
    public void SendEmergencyStop(int value)
    {
        string valStr = value == 1 ? "true" : "false";
        StartCoroutine(PostJson("/publish_stop_brakes", $"{{\"value\":{valStr}}}"));
    }

    /// <summary>Enviar joystick VR (x=steering, y=speed)</summary>
    public void SendJoystick(float x, float y)
    {
        StartCoroutine(PostJson("/api/control", 
            $"{{\"boton\":\"joystick\",\"x\":{x:F3},\"y\":{y:F3}}}"));
    }

    /// <summary>Toggle VR mode on/off</summary>
    public void SendVrMode(bool active)
    {
        StartCoroutine(PostJson("/publish_modo_mision", $"{{\"mode\":{(active ? 6 : 0)}}}"));
    }

    /// <summary>Set VR emergency brake</summary>
    public void SendVrEmergency(bool active)
    {
        string valStr = active ? "true" : "false";
        StartCoroutine(PostJson("/publish_stop_brakes", $"{{\"value\":{valStr}}}"));
    }

    /// <summary>Activar override signal (1 = true, 0 = false)</summary>
    public void SendOverride(int value)
    {
        string valStr = value == 1 ? "true" : "false";
        StartCoroutine(PostJson("/publish_signal_override", $"{{\"value\":{valStr}}}"));
    }

    /// <summary>Seleccionar si seguimos persona o vehiculo</summary>
    public void SendFollowMode(string followType)
    {
        StartCoroutine(PostJson("/publish_followme", $"{{\"type\":\"{followType}\"}}"));
    }

    /// <summary>Grabar ruta con nombre</summary>
    public void SendRecordTrack(string name, bool isDynamicSpeed, float speedVal)
    {
        string speedTypeStr = isDynamicSpeed ? "true" : "false";
        StartCoroutine(PostJson("/publish_track_name",
            $"{{\"name\":\"{name}\",\"speed_type\":{speedTypeStr},\"speed\":{speedVal}}}"));
    }

    /// <summary>Detener grabación</summary>
    public void SendStopRecord()
    {
        StartCoroutine(PostJson("/publish_track_name",
            "{\"name\":\"\",\"speed_type\":false,\"speed\":0}"));
    }

    /// <summary>Reproducir ruta seleccionada</summary>
    public void SendPlayMap(string mapName, int mission)
    {
        StartCoroutine(PostJson("/publish_map",
            $"{{\"map_name\":\"{mapName}\",\"mission\":{mission},\"mode\":-1}}"));
    }

    /// <summary>Detener reproducción de mapa</summary>
    public void SendStopMap()
    {
        StartCoroutine(PostJson("/publish_map",
            "{\"map_name\":\"\",\"mission\":0,\"mode\":-1}"));
    }

    // ── Helper: POST JSON genérico ──

    IEnumerator PostJson(string endpoint, string jsonBody)
    {
        using (var req = new UnityWebRequest(BaseUrl + endpoint, "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 3;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[FlaskAPI] POST {endpoint} failed: {req.error}");
            }
        }
    }

    // =====================================================================
    // CONNECTION STATE
    // =====================================================================

    void MarkConnected()
    {
        _failCount = 0;
        if (!isConnected)
        {
            isConnected = true;
            if (_wasConnected)
                OnConnectionRestored?.Invoke();
            _wasConnected = true;
            Debug.Log("[FlaskAPI] ¡CONECTADO al servidor! URL: " + BaseUrl);
        }
    }

    void MarkFailed(string error = "")
    {
        _failCount++;
        if (_failCount == 1)
        {
            Debug.LogWarning($"[FlaskAPI] Fallo de conexión #{_failCount} a {BaseUrl}: {error}");
        }
        if (_failCount >= MAX_FAILS_BEFORE_DISCONNECT && isConnected)
        {
            isConnected = false;
            OnConnectionLost?.Invoke();
            Debug.LogWarning("[FlaskAPI] Conexión PERDIDA con el servidor tras " + MAX_FAILS_BEFORE_DISCONNECT + " fallos.");
        }
    }

    // =====================================================================
    // JSON RESPONSE CLASSES (para JsonUtility)
    // =====================================================================

    [Serializable] class TelemetryResponse { public TelemetryData status; }
    [Serializable] class TelemetryData { public float speed; public float steering; }
    [Serializable] class StatusIntResponse { public int status; }
    [Serializable] class StatusStringResponse { public string status; }
    [Serializable] class LandMeterResponse { public LandMeterData status; }
    [Serializable] class LandMeterData { public float x; public float y; }
    [Serializable] class ProgressResponse { public float progress; }
    [Serializable] class NumCamerasResponse { public int num_cameras; }
    [Serializable] class AvailableMapsResponse { public string[] map_names; }
}
