using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Conecta los botones generados por SceneBuilder.cs con el FlaskApiClient.
/// Replica EXACTAMENTE el comportamiento de publishOperationMode(), publishFollowP/V(),
/// publishTrackName(), publishMap(), stopPublishTrackName(), stopPublishMap()
/// y updateStatusMode() de index_new.html.
/// </summary>
public class ControlPanelUI : MonoBehaviour
{
    // ── Colores exactos del CSS de la web ──
    // Default: linear-gradient(135deg, #1e6b3a, #2a9d52) — usamos el punto medio
    static readonly Color GREEN_DEFAULT = HexColor("#248446");
    // Activo: linear-gradient(135deg, #c0392b, #e74c3c) — usamos el punto medio
    static readonly Color RED_SELECTED  = HexColor("#d4342c");
    // Disabled: gris cuando Record/PlayMap están activos
    static readonly Color GREY_DISABLED = HexColor("#555555");
    // Stop button: siempre rojo fijo
    static readonly Color RED_STOP_BG   = HexColor("#dc2626");

    // ── Referencias (se buscan automáticamente) ──
    Button btnStandby, btnAuto, btnTele, btnNav2;
    Button btnFollowPerson, btnFollowVehicle;
    Button btnStopEmerg;
    Button btnRecord, btnStopRec;
    Button btnPlayMap, btnStopMap;
    Button btnLock;
    // Toggles
    Button tglDynamicSpeed;
    bool _isDynamicSpeed = false; // default a dinámica (como en HTML a veces)
    
    Button tglFollowMe;
    bool _isFollowMe = false;
    
    Button tglOverrideSpeed;
    bool _isOverrideSpeed = false;
    
    TMP_InputField inpRouteName;
    TMP_InputField inpSpeed;
    Dropdown dropMapList;
    
    Button btnMapHand;
    bool _mapFreeMove = false;
    
    TLab.WebView.Browser mapBrowser;
    
    GameObject scrollViewContent;
    GameObject gpsMapPanel;

    // HUD references
    TextMeshProUGUI txtSpeed;
    TextMeshProUGUI txtEmergTitle;
    TextMeshProUGUI txtEmergReason;
    TextMeshProUGUI txtRollVal;
    TextMeshProUGUI txtPitchVal;
    TextMeshProUGUI txtProgress;
    TextMeshProUGUI txtWpInfo;

    // Estado interno (réplica de las variables JS de la web)
    int _lastPolledMode = -1;
    string _activeFollowType = null; // "person" o "vehicle"
    bool _isRecording = false;
    bool _isPlayingMap = false;

    void Start()
    {
        StartCoroutine(InitializeWhenReady());
    }

    IEnumerator InitializeWhenReady()
    {
        while (FlaskApiClient.Instance == null)
            yield return new WaitForSeconds(0.2f);

        FindAndBindButtons();
        FindHUDElements();
        FindPhase4Elements();

        FlaskApiClient.Instance.OnStateUpdated += UpdateUI;
        FlaskApiClient.Instance.OnConnectionLost += OnConnectionLost;
        FlaskApiClient.Instance.OnConnectionRestored += OnConnectionRestored;

        Debug.Log("[ControlPanelUI] Inicializado. Botones conectados.");
    }

    void OnDestroy()
    {
        if (FlaskApiClient.Instance != null)
        {
            FlaskApiClient.Instance.OnStateUpdated -= UpdateUI;
            FlaskApiClient.Instance.OnConnectionLost -= OnConnectionLost;
            FlaskApiClient.Instance.OnConnectionRestored -= OnConnectionRestored;
        }
    }

    // =====================================================================
    // BUSCAR Y VINCULAR BOTONES
    // =====================================================================

    void FindAndBindButtons()
    {
        var allButtons = GetComponentsInChildren<Button>(true);
        int bound = 0;
        foreach (var btn in allButtons)
        {
            string id = btn.gameObject.name;
            switch (id)
            {
                case "btn_standby":
                    btnStandby = btn;
                    btn.onClick.AddListener(() => PublishOperationMode(0));
                    bound++;
                    break;
                case "btn_map_hand":
                    btnMapHand = btn;
                    btn.onClick.AddListener(ToggleMapHand);
                    bound++;
                    break;
                case "btn_auto":
                    btnAuto = btn;
                    btn.onClick.AddListener(() => PublishOperationMode(1));
                    bound++;
                    break;
                case "btn_tele":
                    btnTele = btn;
                    btn.onClick.AddListener(() => PublishOperationMode(2));
                    bound++;
                    break;
                case "btn_nav2":
                    btnNav2 = btn;
                    btn.onClick.AddListener(() => PublishOperationMode(4));
                    bound++;
                    break;
                case "btn_follow_person":
                    btnFollowPerson = btn;
                    btn.onClick.AddListener(PublishFollowPerson);
                    bound++;
                    break;
                case "btn_follow_vehicle":
                    btnFollowVehicle = btn;
                    btn.onClick.AddListener(PublishFollowVehicle);
                    bound++;
                    break;
                case "btn_stop_emerg":
                    btnStopEmerg = btn;
                    btn.onClick.AddListener(ToggleEmergencyStop);
                    bound++;
                    break;
                case "btn_lock":
                    btnLock = btn;
                    btn.onClick.AddListener(OnLockPressed);
                    bound++;
                    break;
                case "btn_record":
                    btnRecord = btn;
                    btn.onClick.AddListener(OnRecordPressed);
                    bound++;
                    break;
                case "btn_stop_rec":
                    btnStopRec = btn;
                    btn.onClick.AddListener(OnStopRecordPressed);
                    bound++;
                    break;
                case "btn_play_map":
                    btnPlayMap = btn;
                    btn.onClick.AddListener(OnPlayMapPressed);
                    bound++;
                    break;
                case "btn_stop_map":
                    btnStopMap = btn;
                    btn.onClick.AddListener(OnStopMapPressed);
                    bound++;
                    break;
                case "tgl_dynamic_speed":
                    tglDynamicSpeed = btn;
                    btn.onClick.AddListener(ToggleDynamicSpeed);
                    bound++;
                    break;
                case "tgl_follow_me":
                    tglFollowMe = btn;
                    btn.onClick.AddListener(ToggleFollowMe);
                    bound++;
                    break;
                case "tgl_override_speed":
                    tglOverrideSpeed = btn;
                    btn.onClick.AddListener(ToggleOverrideSpeed);
                    bound++;
                    break;
            }
        }
        
        // Inicializar aspecto visual de los toggles
        UpdateToggleVisual(tglDynamicSpeed, _isDynamicSpeed);
        UpdateToggleVisual(tglFollowMe, _isFollowMe);
        UpdateToggleVisual(tglOverrideSpeed, _isOverrideSpeed);
        
        Debug.Log($"[ControlPanelUI] Vinculados {bound} botones de {allButtons.Length} totales.");
    }

    string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }

    void FindHUDElements()
    {
        var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in allTexts)
        {
            string path = GetPath(txt.transform);
            if (path.Contains("SpeedBlock") && path.Contains("Val") && txt.gameObject.name == "t")
                txtSpeed = txt;
            else if (path.Contains("EmergBlock") && path.Contains("Title") && txt.gameObject.name == "t")
                txtEmergTitle = txt;
            else if (path.Contains("EmergBlock") && path.Contains("Reason") && txt.gameObject.name == "t")
                txtEmergReason = txt;
            else if (path.Contains("RollContainer") && path.Contains("Val") && txt.gameObject.name == "t")
                txtRollVal = txt;
            else if (path.Contains("PitchContainer") && path.Contains("Val") && txt.gameObject.name == "t")
                txtPitchVal = txt;
            else if (txt.gameObject.name == "t" && path.Contains("wpPct"))
                txtProgress = txt;
            else if (txt.gameObject.name == "t" && path.Contains("wpInfo"))
                txtWpInfo = txt;
        }
    }

    void FindPhase4Elements()
    {
        var allInps = GetComponentsInChildren<TMP_InputField>(true);
        foreach (var inp in allInps)
        {
            string path = GetPath(inp.transform);
            if (path.Contains("SpeedInput"))
            {
                inpSpeed = inp;
            }
            else
            {
                inpRouteName = inp;
            }
        }
        dropMapList = GetComponentInChildren<Dropdown>(true);
        
        var sv = transform.Find("ScrollView");
        if (sv != null) scrollViewContent = sv.gameObject;
        
        Transform wv = transform.Find("[GPS Map Panel]/BG/WebViewContainer/MapBrowser");
        if (wv != null) {
            mapBrowser = wv.GetComponent<TLab.WebView.Browser>();
            if (mapBrowser != null) {
                string ip = PlayerPrefs.GetString("ServerIP", "192.168.1.100");
                string port = PlayerPrefs.GetString("ServerPort", "5050");
                mapBrowser.InitOption($"http://{ip}:{port}/gps_only", 30, new TLab.WebView.Download.Option());
                mapBrowser.Init(new Vector2Int(1920, 1080), new Vector2Int(1920, 1080));
            }
        }
        
        // Find [GPS Map Panel] globally
        var gps = GameObject.Find("[GPS Map Panel]");
        if (gps != null) gpsMapPanel = gps;
    }

    // =====================================================================
    // OPERATION MODE — Réplica exacta de publishOperationMode() de la web
    // =====================================================================

    /// <summary>
    /// Replica publishOperationMode(mode) del JS:
    /// 1. Resetea TODOS los botones de modo a verde por defecto
    /// 2. Pone en ROJO el botón del modo seleccionado
    /// 3. Envía POST /publish_modo_mision
    /// </summary>
    void PublishOperationMode(int mode)
    {
        // Reset ALL mode buttons to default green (como en JS: forEach(b => b.style.background = ""))
        SetBtnBg(btnStandby, GREEN_DEFAULT);
        SetBtnBg(btnAuto, GREEN_DEFAULT);
        SetBtnBg(btnTele, GREEN_DEFAULT);
        SetBtnBg(btnNav2, GREEN_DEFAULT);
        SetBtnBg(btnFollowPerson, GREEN_DEFAULT);
        SetBtnBg(btnFollowVehicle, GREEN_DEFAULT);

        // Highlight el seleccionado en ROJO
        if (mode == 0) { SetBtnBg(btnStandby, RED_SELECTED); _activeFollowType = null; }
        else if (mode == 1) { SetBtnBg(btnAuto, RED_SELECTED); _activeFollowType = null; }
        else if (mode == 2) { SetBtnBg(btnTele, RED_SELECTED); _activeFollowType = null; }
        else if (mode == 4) { SetBtnBg(btnNav2, RED_SELECTED); _activeFollowType = null; }

        // Enviar al backend
        FlaskApiClient.Instance?.SendModeMission(mode);
        Debug.Log($"[ControlPanelUI] Modo enviado: {mode}");
    }

    /// <summary>
    /// Replica publishFollowP() del JS:
    /// 1. FollowP = ROJO, FollowV = verde
    /// 2. Envía activate_follow_mode + modo 3
    /// </summary>
    void PublishFollowPerson()
    {
        // Reset todos a verde
        SetBtnBg(btnStandby, GREEN_DEFAULT);
        SetBtnBg(btnAuto, GREEN_DEFAULT);
        SetBtnBg(btnTele, GREEN_DEFAULT);
        SetBtnBg(btnNav2, GREEN_DEFAULT);
        SetBtnBg(btnFollowPerson, RED_SELECTED);
        SetBtnBg(btnFollowVehicle, GREEN_DEFAULT);

        _activeFollowType = "person";
        FlaskApiClient.Instance?.SendFollowMode("person");
        FlaskApiClient.Instance?.SendModeMission(3);
        Debug.Log("[ControlPanelUI] Follow Person activado (modo 3)");
    }

    /// <summary>
    /// Replica publishFollowV() del JS
    /// </summary>
    void PublishFollowVehicle()
    {
        SetBtnBg(btnStandby, GREEN_DEFAULT);
        SetBtnBg(btnAuto, GREEN_DEFAULT);
        SetBtnBg(btnTele, GREEN_DEFAULT);
        SetBtnBg(btnNav2, GREEN_DEFAULT);
        SetBtnBg(btnFollowPerson, GREEN_DEFAULT);
        SetBtnBg(btnFollowVehicle, RED_SELECTED);

        _activeFollowType = "vehicle";
        FlaskApiClient.Instance?.SendFollowMode("vehicle");
        FlaskApiClient.Instance?.SendModeMission(3);
        Debug.Log("[ControlPanelUI] Follow Vehicle activado (modo 3)");
    }

    // =====================================================================
    // RECORD — Réplica de publishTrackName() / stopPublishTrackName()
    // =====================================================================

    /// <summary>
    /// Web: event.target.disabled = true (el botón Record se deshabilita)
    /// </summary>
    void OnRecordPressed()
    {
        _isRecording = true;
        if (btnRecord != null)
        {
            btnRecord.interactable = false;
            SetBtnBg(btnRecord, GREY_DISABLED);
        }
        string trackName = inpRouteName != null && !string.IsNullOrEmpty(inpRouteName.text) ? inpRouteName.text : "vr_route";
        
        float speed = 10f;
        if (!_isDynamicSpeed && inpSpeed != null && float.TryParse(inpSpeed.text, out float parsed))
        {
            speed = parsed;
        }
        else if (_isDynamicSpeed)
        {
            speed = 0f;
        }
        
        FlaskApiClient.Instance?.SendRecordTrack(trackName, _isDynamicSpeed, speed);
        Debug.Log($"[ControlPanelUI] Grabando ruta (Dyn: {_isDynamicSpeed}, Speed: {speed}, Name: {trackName})");
    }

    void OnStopRecordPressed()
    {
        _isRecording = false;
        if (btnRecord != null)
        {
            btnRecord.interactable = true;
            SetBtnBg(btnRecord, GREEN_DEFAULT);
        }
        FlaskApiClient.Instance?.SendStopRecord();
        Debug.Log("[ControlPanelUI] Grabación detenida");
    }

    void OnPlayMapPressed()
    {
        _isPlayingMap = true;
        if (btnPlayMap != null)
        {
            btnPlayMap.interactable = false;
            SetBtnBg(btnPlayMap, GREY_DISABLED);
        }
        string mapName = "";
        if (dropMapList != null && dropMapList.options.Count > dropMapList.value)
            mapName = dropMapList.options[dropMapList.value].text;
        else if (FlaskApiClient.Instance != null && FlaskApiClient.Instance.availableMaps.Count > 0)
            mapName = FlaskApiClient.Instance.availableMaps[0];
        
        int mission = _isFollowMe ? 2 : 1;
        FlaskApiClient.Instance?.SendPlayMap(mapName, mission);
        Debug.Log($"[ControlPanelUI] Play Map: {mapName} (Mission: {mission})");
    }

    void OnStopMapPressed()
    {
        _isPlayingMap = false;
        if (btnPlayMap != null)
        {
            btnPlayMap.interactable = true;
            SetBtnBg(btnPlayMap, GREEN_DEFAULT);
        }
        FlaskApiClient.Instance?.SendStopMap();
        Debug.Log("[ControlPanelUI] Reproducción detenida");
    }
    
    // =====================================================================
    // TOGGLES
    // =====================================================================
    
    void ToggleDynamicSpeed()
    {
        _isDynamicSpeed = !_isDynamicSpeed;
        UpdateToggleVisual(tglDynamicSpeed, _isDynamicSpeed);
        FlaskApiClient.Instance?.SendUpdateUiState("speedTypeChecked", _isDynamicSpeed ? "true" : "false");
        Debug.Log($"[ControlPanelUI] Dynamic Speed toggled: {_isDynamicSpeed}");
    }
    
    void ToggleFollowMe()
    {
        _isFollowMe = !_isFollowMe;
        UpdateToggleVisual(tglFollowMe, _isFollowMe);
        FlaskApiClient.Instance?.SendUpdateUiState("followmeWayPointChecked", _isFollowMe ? "true" : "false");
        Debug.Log($"[ControlPanelUI] Follow Me (WP) toggled: {_isFollowMe}");
    }
    
    void ToggleOverrideSpeed()
    {
        _isOverrideSpeed = !_isOverrideSpeed;
        UpdateToggleVisual(tglOverrideSpeed, _isOverrideSpeed);
        FlaskApiClient.Instance?.SendUpdateUiState("signalOverrideChecked", _isOverrideSpeed ? "true" : "false");
        FlaskApiClient.Instance?.SendOverride(_isOverrideSpeed ? 1 : 0);
        Debug.Log($"[ControlPanelUI] Override Speed toggled: {_isOverrideSpeed}");
    }
    
    void UpdateToggleVisual(Button btn, bool isOn)
    {
        if (btn == null) return;
        
        var imgBg = btn.GetComponent<Image>();
        if (imgBg != null)
            imgBg.color = isOn ? GREEN_DEFAULT : new Color(0.1f, 0.1f, 0.1f, 1f);
            
        var knob = btn.transform.Find("Knob") as RectTransform;
        if (knob != null)
        {
            if (isOn)
            {
                knob.anchorMin = new Vector2(0.6f, 0.1f);
                knob.anchorMax = new Vector2(0.9f, 0.9f);
            }
            else
            {
                knob.anchorMin = new Vector2(0.1f, 0.1f);
                knob.anchorMax = new Vector2(0.4f, 0.9f);
            }
        }
    }

    // =====================================================================
    // EMERGENCY STOP / VR MODE / LOCK
    // =====================================================================

    bool _emergencyToggle = false;
    void ToggleEmergencyStop()
    {
        _emergencyToggle = !_emergencyToggle;
        FlaskApiClient.Instance?.SendEmergencyStop(_emergencyToggle ? 1 : 0);
        Debug.Log($"[ControlPanelUI] Emergency Stop UI Button: {(_emergencyToggle ? "PRESSED" : "RELEASED")}");
    }

    bool _isVideoUnlocked = false;
    void OnLockPressed()
    {
        _isVideoUnlocked = !_isVideoUnlocked;
        SetBtnBg(btnLock, _isVideoUnlocked ? RED_SELECTED : GREEN_DEFAULT);
        if (_isVideoUnlocked) 
        {
            FlaskApiClient.Instance?.SendModeMission(3); 
        }
        else 
        {
            FlaskApiClient.Instance?.SendUpdateUiState("activeFollowType", "");
        }
    }

    bool _vrModeActive = false;
    void ToggleVrMode()
    {
        _vrModeActive = !_vrModeActive;
        FlaskApiClient.Instance?.SendVrMode(_vrModeActive);
        Debug.Log($"[ControlPanelUI] VR Mode: {(_vrModeActive ? "ON" : "OFF")}");
    }
    
    void ToggleMapHand()
    {
        _mapFreeMove = !_mapFreeMove;
        SetBtnBg(btnMapHand, _mapFreeMove ? HexColor("#3b82f6") : HexColor("#1e202c"));
        Debug.Log($"[ControlPanelUI] Map Free Move: {_mapFreeMove}");
        if (mapBrowser != null) {
            mapBrowser.EvaluateJS("toggleMapInteraction();");
        }
    }

    // =====================================================================
    // UPDATE UI — Réplica de updateStatusMode() + telemetry polling
    // =====================================================================

    void UpdateUI()
    {
        if (FlaskApiClient.Instance == null) return;
        var api = FlaskApiClient.Instance;

        // ── Velocidad ──
        if (txtSpeed != null)
            txtSpeed.text = api.speed.ToString("F1");

        // ── Emergency Stop ──
        if (txtEmergReason != null)
        {
            txtEmergReason.text = api.emergencyReason;
            txtEmergReason.color = api.emergencyStopActive ? RED_SELECTED : HexColor("#4ade80");
        }
        if (txtEmergTitle != null)
            txtEmergTitle.color = api.emergencyStopActive ? RED_SELECTED : HexColor("#4ade80");

        // ── Roll / Pitch ──
        if (txtRollVal != null)
            txtRollVal.text = $"Roll: {api.rollDeg:F1}";
        if (txtPitchVal != null)
            txtPitchVal.text = $"Pitch: {api.pitchDeg:F1}";

        // ── Route progress ──
        if (txtProgress != null)
            txtProgress.text = $"Percentage traveled: {api.routeProgress:F2}%";

        // ── Waypoint info ──
        if (txtWpInfo != null)
            txtWpInfo.text = api.waypointInfo;

        // ── Sincronizar botones de Record y Play Map ──
        if (api.isRecording != _isRecording)
        {
            _isRecording = api.isRecording;
            if (btnRecord != null)
            {
                btnRecord.interactable = !_isRecording;
                SetBtnBg(btnRecord, _isRecording ? GREY_DISABLED : GREEN_DEFAULT);
            }
        }
        
        if (api.isPlayingMap != _isPlayingMap)
        {
            _isPlayingMap = api.isPlayingMap;
            if (btnPlayMap != null)
            {
                btnPlayMap.interactable = !_isPlayingMap;
                SetBtnBg(btnPlayMap, _isPlayingMap ? GREY_DISABLED : GREEN_DEFAULT);
            }
        }

        // ── Sincronizar Toggles y Switchers ──
        if (api.speedTypeChecked != _isDynamicSpeed) {
            _isDynamicSpeed = api.speedTypeChecked;
            if (tglDynamicSpeed != null) UpdateToggleVisual(tglDynamicSpeed, _isDynamicSpeed);
        }
        if (api.signalOverrideChecked != _isOverrideSpeed) {
            _isOverrideSpeed = api.signalOverrideChecked;
            if (tglOverrideSpeed != null) UpdateToggleVisual(tglOverrideSpeed, _isOverrideSpeed);
        }
        if (api.followmeWayPointChecked != _isFollowMe) {
            _isFollowMe = api.followmeWayPointChecked;
            if (tglFollowMe != null) UpdateToggleVisual(tglFollowMe, _isFollowMe);
        }
        
        // ── Sincronizar Freno de Emergencia ──
        if (api.emergencyBrake != _emergencyToggle)
        {
            _emergencyToggle = api.emergencyBrake;
            if (btnStopEmerg != null) SetBtnBg(btnStopEmerg, _emergencyToggle ? RED_SELECTED : GREEN_DEFAULT);
        }
        
        // Actualizar HUD text (razón)
        if (txtEmergReason != null)
        {
            txtEmergReason.text = api.emergencyReason;
            txtEmergReason.color = (api.emergencyReason == "None" || string.IsNullOrEmpty(api.emergencyReason)) ? HexColor("#4ade80") : HexColor("#ef4444"); // Rojo si hay frenada
        }

        // ── updateStatusMode() — Sincroniza los botones con el modo real del backend ──
        if (!string.IsNullOrEmpty(api.activeFollowType)) {
            _activeFollowType = api.activeFollowType;
        }
        UpdateModeFromServer(api.operationMode);
        
        // ── Phase 4: Maps ──
        UpdateDropdownOptions();
    }
    
    int _lastMapCount = -1;
    void UpdateDropdownOptions()
    {
        if (dropMapList == null || FlaskApiClient.Instance == null) return;
        var api = FlaskApiClient.Instance;
        
        if (api.availableMaps != null && api.availableMaps.Count != _lastMapCount)
        {
            _lastMapCount = api.availableMaps.Count;
            dropMapList.ClearOptions();
            var options = new System.Collections.Generic.List<Dropdown.OptionData>();
            foreach (var m in api.availableMaps)
            {
                options.Add(new Dropdown.OptionData(m));
            }
            dropMapList.AddOptions(options);
        }
    }

    /// <summary>
    /// Réplica exacta de updateStatusMode() de la web:
    /// - Solo actualiza si mode 0-4
    /// - Reset TODOS a verde
    /// - El activo se pone rojo
    /// - Si mode == 3, usa _activeFollowType para saber cuál Follow resaltar
    /// </summary>
    void UpdateModeFromServer(int mode)
    {
        if (mode == _lastPolledMode) return;
        _lastPolledMode = mode;

        // if (mode < 0 || mode > 4) return; // Como en la web
        // Reset todos a verde (como en JS: Object.values(btns).forEach(b => b.style.background = ''))
        SetBtnBg(btnStandby, GREEN_DEFAULT);
        SetBtnBg(btnAuto, GREEN_DEFAULT);
        SetBtnBg(btnTele, GREEN_DEFAULT);
        SetBtnBg(btnNav2, GREEN_DEFAULT);
        SetBtnBg(btnFollowPerson, GREEN_DEFAULT);
        SetBtnBg(btnFollowVehicle, GREEN_DEFAULT);

        // Resaltar el activo en rojo
        if (mode == 0) SetBtnBg(btnStandby, RED_SELECTED);
        else if (mode == 1) SetBtnBg(btnAuto, RED_SELECTED);
        else if (mode == 2) SetBtnBg(btnTele, RED_SELECTED);
        else if (mode == 3)
        {
            if (_activeFollowType == "person") SetBtnBg(btnFollowPerson, RED_SELECTED);
            else if (_activeFollowType == "vehicle") SetBtnBg(btnFollowVehicle, RED_SELECTED);
        }
        else if (mode == 4) SetBtnBg(btnNav2, RED_SELECTED);
    }

    // =====================================================================
    // CONNECTION EVENTS
    // =====================================================================

    void OnConnectionLost()
    {
        Debug.LogWarning("[ControlPanelUI] Conexión con el servidor perdida.");
    }

    void OnConnectionRestored()
    {
        Debug.Log("[ControlPanelUI] Conexión restaurada.");
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    /// <summary>
    /// Cambia el color del botón, actualizando el ColorBlock para evitar bugs de deselección.
    /// </summary>
    void SetBtnBg(Button btn, Color col)
    {
        if (btn == null) return;
        var cb = btn.colors;
        cb.normalColor = col;
        cb.selectedColor = col;
        btn.colors = cb;
        
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = col;
    }


    static Color HexColor(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }
}
