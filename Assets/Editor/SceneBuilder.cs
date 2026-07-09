using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Script de Editor: Sala de Control VR – FASE 3.5 Calco 1:1 del HTML.
/// Resolución base 1920x1080. Layout plano (flat). 
/// Corregido: emojis reemplazados por texto ASCII, fuentes calibradas, layout fiel a capturas.
/// </summary>
public static class SceneBuilder
{
    // =====================================================================
    // COLORES DEL CSS (index_new.html)
    // =====================================================================
    static readonly Color BG_BODY       = Hex("#0f1117");
    static readonly Color BG_RPANEL     = Hex("#15171e");
    static readonly Color BG_SECTION    = new Color(1f, 1f, 1f, 0.03f);
    static readonly Color BORDER        = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color TEXT_MAIN     = Hex("#e0e0e0");
    static readonly Color TEXT_TITLE    = Hex("#c0c0d0");
    static readonly Color TEXT_DIM      = Hex("#8a8a9a");
    static readonly Color ACCENT_BLUE   = Hex("#3b82f6");
    static readonly Color HUD_SPEED     = Hex("#38bdf8");
    static readonly Color GREEN_OK      = Hex("#4ade80");

    // CSS: background: linear-gradient(135deg, #1e6b3a, #2a9d52) – usamos el promedio
    static readonly Color BTN_GREEN     = Hex("#248446");
    // CSS: background: linear-gradient(135deg, #c0392b, #e74c3c) – promedio
    static readonly Color BTN_RED       = Hex("#d34233");

    // Botón STOP en la web: background: rgba(34, 197, 94, 0.85)
    static readonly Color STOP_GREEN_BG = new Color(0.13f, 0.77f, 0.37f, 0.85f);
    // Botón candado: background: rgba(34, 197, 94, 0.7)
    static readonly Color LOCK_GREEN    = new Color(0.13f, 0.77f, 0.37f, 0.70f);

    static readonly Color INPUT_BG      = new Color(1f, 1f, 1f, 0.05f);
    static readonly Color METER_BG      = new Color(0.09f, 0.12f, 0.18f, 1f);
    static readonly Color METER_HORIZON = Hex("#22c55e");
    static readonly Color TOGGLE_BG     = Hex("#3a3a4a");

    static readonly Color[] CAM_HDR = {
        Hex("#4260f5"), Hex("#2ebdae"), Hex("#ff6b36"), Hex("#730ab8"), Hex("#05d6a0"),
    };

    // Layout Plano (Flat)
    const float DIST = 2.5f;
    const float EYE  = 0f;

    static readonly Vector2 CANVAS_RES = new Vector2(2560, 1440);
    static readonly Vector2 CANVAS_SIZE_MTS = new Vector2(1.8f, 1.01f);

    // =====================================================================
    // ENTRY POINT
    // =====================================================================

    [MenuItem("INSIA/Crear Sala de Control (Rediseño Web)")]
    public static void BuildControlRoom()
    {
        if (!EditorUtility.DisplayDialog("Crear Sala de Control",
            "Generando interfaz plana con layout calibrado.\n¿Continuar?", "Crear", "Cancelar"))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupEnvironment();
        SetupCameraRig();

        var root = new GameObject("[UI Panels]");

        BuildMainPanel(root.transform, FlatPos(0, 0));
        BuildSideCam(root.transform, "CAM 2", 2, FlatPos(-1.4f, 0.1f));
        BuildSideCam(root.transform, "CAM 4", 4, FlatPos(1.4f, 0.1f));
        BuildSideCam(root.transform, "CAM 3", 3, FlatPos(-0.5f, -0.8f));
        BuildSideCam(root.transform, "CAM 5", 5, FlatPos(0.5f, -0.8f));
        BuildGPS(root.transform, FlatPos(0, 0.88f));

        root.AddComponent<LazyFollowUI>();

        // ── Fase 4: Adjuntar scripts de comunicación ──
        // FlaskApiClient (Singleton HTTP) — en un objeto persistente
        var managers = new GameObject("[Managers]");
        var apiClient = managers.AddComponent<FlaskApiClient>();
        apiClient.serverIp = "10.170.183.110"; // IP del vehículo (auto-detectada por ServerDiscovery)
        apiClient.serverPort = 5050;

        // QuestControllerHandler — mapeo de controles del mando
        managers.AddComponent<QuestControllerHandler>();

        // ServerDiscovery — Auto-configura la IP (Fase 2)
        managers.AddComponent<ServerDiscovery>();

        // BrowserManager — REQUERIDO por TLab WebView para limpiar recursos nativos
        managers.AddComponent<TLab.WebView.BrowserManager>();

        // MjpegStreamReceiver — uno por cada cámara que tenga CameraPanelController
        // Se buscan todos los CameraPanelController y se les añade el receiver
        var allCamPanels = Object.FindObjectsByType<CameraPanelController>(FindObjectsSortMode.None);
        foreach (var camPanel in allCamPanels)
        {
            var mjpeg = camPanel.gameObject.AddComponent<MjpegStreamReceiver>();
            mjpeg.cameraId = camPanel.cameraId;
            mjpeg.targetDisplay = camPanel.videoDisplay;
        }

        SetupEventSystem();

        // ── Fase 8: Forzar configuración de Passthrough y Teclado ──
        var ovrManager = Object.FindFirstObjectByType<OVRManager>();
        if (ovrManager != null)
        {
            ovrManager.isInsightPassthroughEnabled = true;

            var pt = ovrManager.gameObject.GetComponent<OVRPassthroughLayer>();
            if (pt == null) pt = ovrManager.gameObject.AddComponent<OVRPassthroughLayer>();
            pt.projectionSurfaceType = OVRPassthroughLayer.ProjectionSurfaceType.Reconstructed;
            pt.hidden = true; // Arranca en VR (fijo)
            
            // Forzamos la cámara a Transparente para que el Validator de Meta NO se queje nunca.
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = new Color(0, 0, 0, 0);
            }
            
            // Forzamos la activación del Teclado Nativo en el Config Global de Meta
#if UNITY_EDITOR
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<OVRProjectConfig>("Assets/Oculus/OVRProjectConfig.asset");
            if (config == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("t:OVRProjectConfig");
                if (guids.Length > 0)
                    config = UnityEditor.AssetDatabase.LoadAssetAtPath<OVRProjectConfig>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (config != null)
            {
                config.requiresSystemKeyboard = true;
                UnityEditor.EditorUtility.SetDirty(config);
            }
#endif
            
            Debug.Log("[OK] Passthrough y SystemKeyboard inyectados en OVRManager y ProjectConfig.");
        }
        else
        {
            Debug.LogWarning("[!] No se encontró OVRManager. Asegúrate de añadir el Building Block de Camera Rig.");
        }

        string path = "Assets/Scenes/MainControlRoom.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log("[OK] Sala de Control (Calco HTML + Fase 4) creada en: " + path);
        Debug.Log("[!] RECUERDA: Anade Building Block Ray Interactions + Make Canvas Interactable manualmente.");
        Debug.Log("[i] Configura la IP del servidor en [Managers] > FlaskApiClient > Server Ip.");
    }

    // =====================================================================
    // PANEL PRINCIPAL
    // =====================================================================

    static void BuildMainPanel(Transform parent, Vector3 pos)
    {
        var canvas = WorldCanvas("Main_Web_Interface", pos, CANVAS_SIZE_MTS, CANVAS_RES);
        canvas.transform.SetParent(parent, true);

        var bg = R(canvas.transform, "BG", V(0,0), V(1,1));
        bg.AddComponent<Image>().color = BG_BODY;

        // ── VIDEO AREA (70% izquierdo, 30% panel derecho) ──
        float split = 1350f / 1920f;
        var video = R(bg.transform, "VideoArea", V(0,0), V(split, 1));
        var vidImg = video.AddComponent<RawImage>(); vidImg.color = Color.black;

        var ov = R(video.transform, "Overlay", V(0,0), V(1,1));
        
        // Icono de carga giratorio
        var icon = Label(ov.transform, "icon", "⏳", 213, new Color(1,1,1,0.8f), TextAnchor.MiddleCenter, FontStyle.Normal);
        icon.rectTransform.anchorMin = V(0, 0.5f);
        icon.rectTransform.anchorMax = V(1, 1f);
        var animIcon = icon.gameObject.AddComponent<LoadingAnimator>();
        animIcon.enableRotation = true;
        animIcon.enablePulsing = false;
        animIcon.rotationSpeed = -150f;

        // Texto parpadeante
        var txtWait = Label(ov.transform, "t", "Conectando Cámara 1...", 74, new Color(1,1,1,0.6f), TextAnchor.MiddleCenter, FontStyle.Normal);
        txtWait.rectTransform.anchorMin = V(0, 0f);
        txtWait.rectTransform.anchorMax = V(1, 0.5f);
        var animTxt = txtWait.gameObject.AddComponent<LoadingAnimator>();
        animTxt.enableRotation = false;
        animTxt.enablePulsing = true;

        var ctrl = canvas.AddComponent<CameraPanelController>();
        ctrl.cameraId = 1; ctrl.videoDisplay = vidImg;

        // Botón CANDADO (arriba-izquierda) – texto "[L]" en vez de emoji
        var lockArea = R(video.transform, "LockBtn", V(0.01f, 0.91f), V(0.055f, 0.97f));
        Btn(lockArea.transform, "btn_lock", "[L]", LOCK_GREEN, 47);

        // Botón STOP (arriba-derecha) – texto "! STOP"
        var stopArea = R(video.transform, "StopBtn", V(0.86f, 0.91f), V(0.99f, 0.97f));
        Btn(stopArea.transform, "btn_stop_emerg", "! STOP", STOP_GREEN_BG, 47);

        // HUD Telemetría
        BuildHUD(video.transform);

        // ── PANEL DERECHO (Sidebar 22%) ──
        var rp = R(bg.transform, "RightPanel", V(split,0), V(1,1));
        rp.AddComponent<Image>().color = BG_RPANEL;
        R(rp.transform, "BorderL", V(0,0), V(0.003f,1)).AddComponent<Image>().color = BORDER;

        // Título del Panel de Control en lugar de Tabs
        var tabs = R(rp.transform, "Tabs", V(0, 0.954f), V(1, 1));
        tabs.AddComponent<Image>().color = new Color(1f,1f,1f,0.02f);
        Label(tabs.transform, "t", "CONTROL PANEL", 42, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

        // Footer (alto ~50px)
        var foot = R(rp.transform, "Footer", V(0, 0), V(1, 0.046f));
        R(foot.transform, "Bdr", V(0, 0.96f), V(1, 1)).AddComponent<Image>().color = BORDER;
        var btnLight = Btn(foot.transform, "btn_light_mode", "* Switch to Light Mode", new Color(0,0,0,0.01f), 31);
        btnLight.AddComponent<BoxCollider>().size = new Vector3(512, 50, 1);
        var lblLight = btnLight.transform.Find("Lbl").GetComponent<TMPro.TextMeshProUGUI>();
        lblLight.color = TEXT_DIM; lblLight.fontStyle = TMPro.FontStyles.Normal;
        
        var themeSw = rp.gameObject.AddComponent<ThemeSwitcher>();
        themeSw.myLabel = lblLight;
        btnLight.GetComponent<Button>().onClick.AddListener(themeSw.ToggleTheme);

        var sv = R(rp.transform, "ScrollView", V(0, 0.046f), V(0.95f, 0.954f)); // Dejamos 5% a la derecha para la scrollbar
        sv.AddComponent<RectMask2D>();
        var sr = sv.AddComponent<NoDragScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 15;
        sr.movementType = ScrollRect.MovementType.Clamped;
        
        // Scrollbar
        var sbarObj = R(rp.transform, "Scrollbar", V(0.95f, 0.046f), V(1, 0.954f));
        var sbarBg = sbarObj.AddComponent<Image>();
        sbarBg.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        var sbar = sbarObj.AddComponent<Scrollbar>();
        sbar.direction = Scrollbar.Direction.BottomToTop;
        
        var sbarSlidingArea = R(sbarObj.transform, "Sliding Area", V(0,0), V(1,1));
        var sbarHandle = R(sbarSlidingArea.transform, "Handle", V(0,0), V(1,1));
        var sbarHandleImg = sbarHandle.AddComponent<Image>();
        sbarHandleImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        
        sbar.targetGraphic = sbarHandleImg;
        sbar.handleRect = sbarHandle.GetComponent<RectTransform>();
        sr.verticalScrollbar = sbar;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        // Anclado arriba para que el ContentSizeFitter expanda hacia abajo
        var content = R(sv.transform, "Content", V(0, 1), V(1, 1));
        var crt = content.GetComponent<RectTransform>();
        crt.pivot = new Vector2(0.5f, 1f);
        sr.content = crt;

        var vl = content.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment = TextAnchor.UpperCenter;
        // CSS margin-bottom: 12px → spacing 12
        vl.spacing = 12;
        // CSS padding: 16px
        vl.padding = new RectOffset(16, 16, 16, 16);
        // FUNDAMENTAL: childControlHeight = true para que aplique los preferredHeight de cada sección
        vl.childControlHeight = true; vl.childControlWidth = true;
        vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── SECCIONES ──
        BuildSectionRouteName(content.transform);
        BuildSectionSelectMap(content.transform);
        BuildSectionOperationMode(content.transform);
        BuildSectionInclination(content.transform);

        canvas.AddComponent<BoxCollider>().size = new Vector3(CANVAS_RES.x, CANVAS_RES.y, 1f);

        // Fase 4: ControlPanelUI busca todos los botones por ID y los conecta a FlaskApiClient
        canvas.AddComponent<ControlPanelUI>();
    }

    // ── HUD ──────────────────────────────────────────────────────────

    static void BuildHUD(Transform videoParent)
    {
        var hud = R(videoParent, "TelemetryHUD", V(0.01f, 0.02f), V(0.32f, 0.14f));
        hud.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.72f);

        // SPEED block
        var sp = R(hud.transform, "SpeedBlock", V(0.03f, 0.05f), V(0.42f, 0.95f));
        Label(R(sp.transform, "Lbl", V(0,0.72f), V(1,1)).transform, "t",
            "SPEED", 10, new Color(1,1,1,0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
        Label(R(sp.transform, "Val", V(0,0.22f), V(1,0.72f)).transform, "t",
            "0.0", 36, HUD_SPEED, TextAnchor.MiddleCenter, FontStyle.Bold);
        Label(R(sp.transform, "Unit", V(0,0), V(1,0.22f)).transform, "t",
            "km/h", 10, new Color(1,1,1,0.5f), TextAnchor.MiddleCenter, FontStyle.Bold);

        // Separador vertical
        R(hud.transform, "Divider", V(0.45f, 0.15f), V(0.452f, 0.85f))
            .AddComponent<Image>().color = new Color(1,1,1,0.10f);

        // EMERGENCY STOP block
        var em = R(hud.transform, "EmergBlock", V(0.50f, 0.05f), V(0.97f, 0.95f));
        Label(R(em.transform, "Title", V(0,0.55f), V(1,0.95f)).transform, "t",
            "EMERGENCY STOP", 11, GREEN_OK, TextAnchor.MiddleLeft, FontStyle.Bold);
        Label(R(em.transform, "Reason", V(0,0.05f), V(1,0.5f)).transform, "t",
            "None", 14, GREEN_OK, TextAnchor.MiddleLeft, FontStyle.Bold);
    }

    // ── SECTION: ENTER ROUTE NAME ────────────────────────────────────
    // Estructura: Title(9%) | Input(17%) | gap(3%) | Btns+Dot(20%) | gap(3%) | Toggle(13%) | bottom(35% unused)
    // Pero usamos porcentaje invertido (0=bottom, 1=top) del cuadro de 220px

    static void BuildSectionRouteName(Transform parent)
    {
        var sec = Section(parent, "SecRouteName", "ENTER ROUTE NAME", 400);

        // Input field
        var inp = R(sec.transform, "inp_route_name", V(0.04f, 0.65f), V(0.96f, 0.82f));
        inp.AddComponent<Image>().color = INPUT_BG;
        AddBorder(inp.transform);
        
        var textArea = R(inp.transform, "TextArea", V(0,0), V(1,1));
        var rt = textArea.GetComponent<RectTransform>();
        rt.offsetMin = new Vector2(10, 0);
        rt.offsetMax = new Vector2(-10, 0);
        textArea.AddComponent<UnityEngine.UI.RectMask2D>();
        
        var textObj = R(textArea.transform, "Text", V(0,0), V(1,1));
        var textComp = textObj.AddComponent<TextMeshProUGUI>();
        textComp.fontSize = 37;
        textComp.color = TEXT_MAIN;
        textComp.alignment = TextAlignmentOptions.Left;
        
        var phObj = R(textArea.transform, "Placeholder", V(0,0), V(1,1));
        var phComp = phObj.AddComponent<TextMeshProUGUI>();
        phComp.text = "Nombre de la ruta...";
        phComp.fontSize = 37;
        phComp.color = TEXT_DIM;
        phComp.alignment = TextAlignmentOptions.Left;
        
        var inputF = inp.AddComponent<TMP_InputField>();
        inputF.textComponent = textComp;
        inputF.placeholder = phComp;
        inputF.textViewport = rt;
        
        inp.AddComponent<VRKeyboardFocus>();

        // Fila de botones Record + Stop + Dot
        Btn(R(sec.transform, "bRec", V(0.04f, 0.32f), V(0.38f, 0.62f)).transform,
            "btn_record", "Record", BTN_GREEN, 16);
        Btn(R(sec.transform, "bStop", V(0.40f, 0.32f), V(0.72f, 0.62f)).transform,
            "btn_stop_rec", "Stop", BTN_RED, 16);
        // Green status dot
        var dot1 = R(sec.transform, "StatusDot", V(0.74f, 0.43f), V(0.77f, 0.51f));
        dot1.AddComponent<Image>().color = GREEN_OK;

        // Toggle row: Static/Dynamic Speed
        var toggleRow = R(sec.transform, "ToggleRow", V(0.04f, 0.06f), V(0.96f, 0.28f));
        // Toggle pill
        var dynToggle = BuildTogglePill(toggleRow.transform, V(0, 0.15f), V(0.15f, 0.85f));
        dynToggle.name = "tgl_dynamic_speed";
        Label(R(toggleRow.transform, "lbl", V(0.17f,0), V(0.62f,1)).transform,
            "t", "Static/Dynamic Speed", 12, TEXT_DIM, TextAnchor.MiddleLeft, FontStyle.Normal);
        // Speed value box
        var spInput = R(toggleRow.transform, "SpeedInput", V(0.64f, 0.15f), V(0.78f, 0.85f));
        spInput.AddComponent<Image>().color = INPUT_BG;
        AddBorder(spInput.transform);
        
        var spTextArea = R(spInput.transform, "TextArea", V(0,0), V(1,1));
        var spRt = spTextArea.GetComponent<RectTransform>();
        spRt.offsetMin = new Vector2(10, 0);
        spRt.offsetMax = new Vector2(-10, 0);
        spTextArea.AddComponent<UnityEngine.UI.RectMask2D>();
        
        var spTextObj = R(spTextArea.transform, "Text", V(0,0), V(1,1));
        var spTextComp = spTextObj.AddComponent<TextMeshProUGUI>();
        spTextComp.fontSize = 37;
        spTextComp.color = TEXT_MAIN;
        spTextComp.alignment = TextAlignmentOptions.Center;
        spTextComp.text = "10";
        
        var spInputF = spInput.AddComponent<TMP_InputField>();
        spInputF.textComponent = spTextComp;
        spInputF.textViewport = spRt;
        spInputF.text = "10";
        
        spInput.AddComponent<VRKeyboardFocus>();
        
        Label(R(toggleRow.transform, "unit", V(0.80f,0), V(0.96f,1)).transform,
            "t", "Km/h", 12, TEXT_DIM, TextAnchor.MiddleLeft, FontStyle.Normal);
    }

    // ── SECTION: SELECT MAP ──────────────────────────────────────────

    static void BuildSectionSelectMap(Transform parent)
    {
        var sec = Section(parent, "SecSelectMap", "SELECT MAP", 480);

        // Dropdown (generado usando DefaultControls para asegurar que funciona en Quest)
        var dropObj = UnityEngine.UI.DefaultControls.CreateDropdown(new UnityEngine.UI.DefaultControls.Resources());
        dropObj.name = "drop_map_list";
        dropObj.transform.SetParent(sec.transform, false);
        var drt = dropObj.GetComponent<RectTransform>();
        drt.anchorMin = V(0.04f, 0.70f);
        drt.anchorMax = V(0.96f, 0.84f);
        drt.offsetMin = Vector2.zero;
        drt.offsetMax = Vector2.zero;
        dropObj.GetComponent<Image>().color = INPUT_BG;
        AddBorder(dropObj.transform);
        
        // Reemplazar textos por TMPro para no romper el layout si es necesario, 
        // pero DefaultControls.CreateDropdown usa UnityEngine.UI.Text.
        // Dado que esto es funcional, lo dejaremos como está (UI.Text) y ControlPanelUI lo leerá via UnityEngine.UI.Dropdown.


        // Botones Play Map + Stop + Dot
        Btn(R(sec.transform, "bPlay", V(0.04f, 0.42f), V(0.38f, 0.67f)).transform,
            "btn_play_map", "Play Map", BTN_GREEN, 16);
        Btn(R(sec.transform, "bStop", V(0.40f, 0.42f), V(0.72f, 0.67f)).transform,
            "btn_stop_map", "Stop", BTN_RED, 16);
        var dot2 = R(sec.transform, "StatusDot", V(0.74f, 0.50f), V(0.77f, 0.58f));
        dot2.AddComponent<Image>().color = GREEN_OK;

        // Follow Me toggle
        var fmRow = R(sec.transform, "FollowMeRow", V(0.04f, 0.26f), V(0.96f, 0.40f));
        var fmToggle = BuildTogglePill(fmRow.transform, V(0, 0.15f), V(0.15f, 0.85f));
        fmToggle.name = "tgl_follow_me";
        Label(R(fmRow.transform, "lbl", V(0.17f,0), V(0.8f,1)).transform,
            "t", "Follow Me", 12, TEXT_DIM, TextAnchor.MiddleLeft, FontStyle.Normal);

        // Info texts
        Label(R(sec.transform, "wpInfo", V(0.04f, 0.14f), V(0.96f, 0.25f)).transform,
            "t", "Waiting for Mission...", 16, TEXT_MAIN, TextAnchor.MiddleLeft, FontStyle.Bold);
        Label(R(sec.transform, "wpPct", V(0.04f, 0.03f), V(0.96f, 0.14f)).transform,
            "t", "Percentage traveled: 0.00%", 12, GREEN_OK, TextAnchor.MiddleLeft, FontStyle.Bold);
    }

    // ── SECTION: OPERATION MODE ──────────────────────────────────────

    static void BuildSectionOperationMode(Transform parent)
    {
        // 4 botones full-width + 2 half-width + override row = necesitamos ~480px
        var sec = Section(parent, "SecOpMode", "OPERATION MODE", 800);

        // 4 full-width buttons apilados
        string[] labels = { "Stand By", "Autonomous", "Teleoperated", "Nav2" };
        string[] ids    = { "btn_standby", "btn_auto", "btn_tele", "btn_nav2" };
        float btnH = 0.13f;   // cada botón ocupa 13% de la altura
        float gap  = 0.012f;  // hueco entre botones
        float y = 0.87f;      // empieza justo debajo del título

        for (int i = 0; i < 4; i++)
        {
            float top = y - i * (btnH + gap);
            Btn(R(sec.transform, ids[i]+"_area", V(0.04f, top - btnH), V(0.96f, top)).transform,
                ids[i], labels[i], BTN_GREEN, 16);
        }

        // 2-column row: Follow Person / Follow Vehicle
        float rowY = y - 4 * (btnH + gap);
        Btn(R(sec.transform, "bFollowP_area", V(0.04f, rowY - btnH), V(0.48f, rowY)).transform,
            "btn_follow_person", "Follow Person", BTN_GREEN, 14);
        Btn(R(sec.transform, "bFollowV_area", V(0.52f, rowY - btnH), V(0.96f, rowY)).transform,
            "btn_follow_vehicle", "Follow Vehicle", BTN_GREEN, 14);

        // Override row
        float orY = rowY - btnH - gap;
        var orRow = R(sec.transform, "OverrideRow", V(0.04f, orY - 0.08f), V(0.96f, orY));
        // Warning icon (triángulo ASCII)
        Label(R(orRow.transform, "Icon", V(0, 0), V(0.08f, 1)).transform,
            "t", "/!\\", 14, GREEN_OK, TextAnchor.MiddleCenter, FontStyle.Bold);
        // Toggle
        var orToggle = BuildTogglePill(orRow.transform, V(0.10f, 0.15f), V(0.24f, 0.85f));
        orToggle.name = "tgl_override_speed";
        Label(R(orRow.transform, "lbl", V(0.26f,0), V(0.7f,1)).transform,
            "t", "Override", 12, TEXT_DIM, TextAnchor.MiddleLeft, FontStyle.Normal);
    }

    // ── SECTION: INCLINATION METERS ──────────────────────────────────

    static void BuildSectionInclination(Transform parent)
    {
        var sec = Section(parent, "SecInclination", "INCLINATION METERS", 530);
        BuildCircleMeter(sec.transform, "Roll", V(0.06f, 0.05f), V(0.47f, 0.86f), "Roll: 0.0");
        BuildCircleMeter(sec.transform, "Pitch", V(0.53f, 0.05f), V(0.94f, 0.86f), "Pitch: 0.0");
    }

    static void BuildCircleMeter(Transform parent, string name, Vector2 aMin, Vector2 aMax, string valueText)
    {
        var container = R(parent, name + "Container", aMin, aMax);

        // Disco/dial de fondo
        var disc = R(container.transform, "Disc", V(0.08f, 0.25f), V(0.92f, 0.95f));
        disc.AddComponent<Image>().color = METER_BG;

        // Línea horizonte (verde)
        R(disc.transform, "Horizon", V(0, 0.47f), V(1, 0.53f)).AddComponent<Image>().color = METER_HORIZON;

        // Coche dibujado con bloques UI para que quede limpio y nítido
        BuildCarIcon(disc.transform);

        // Marca de grados alrededor (simuladas con líneas)
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f;
            float rad = angle * Mathf.Deg2Rad;
            float cx = 0.5f + 0.42f * Mathf.Sin(rad);
            float cy = 0.5f + 0.42f * Mathf.Cos(rad);
            var tick = R(disc.transform, "tick_" + i,
                V(cx - 0.02f, cy - 0.02f), V(cx + 0.02f, cy + 0.02f));
            tick.AddComponent<Image>().color = new Color(1f,1f,1f,0.25f);
        }

        // Valor debajo
        var valBg = R(container.transform, "ValBG", V(0.1f, 0), V(0.9f, 0.22f));
        valBg.AddComponent<Image>().color = INPUT_BG;
        AddBorder(valBg.transform);
        Label(valBg.transform, "Val", valueText, 37, TEXT_MAIN, TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    static void BuildCarIcon(Transform parent)
    {
        // Contenedor central para el coche
        var car = R(parent, "CarIcon", V(0.35f, 0.42f), V(0.65f, 0.58f));
        
        // Techo (mitad superior, centrado)
        R(car.transform, "Top", V(0.2f, 0.5f), V(0.8f, 0.9f)).AddComponent<Image>().color = TEXT_MAIN;
        // Chasis (mitad inferior, ancho completo)
        R(car.transform, "Body", V(0f, 0.2f), V(1f, 0.5f)).AddComponent<Image>().color = TEXT_MAIN;
        // Rueda izquierda
        R(car.transform, "WheelL", V(0.1f, 0f), V(0.3f, 0.2f)).AddComponent<Image>().color = TEXT_DIM;
        // Rueda derecha
        R(car.transform, "WheelR", V(0.7f, 0f), V(0.9f, 0.2f)).AddComponent<Image>().color = TEXT_DIM;
    }

    // =====================================================================
    // CÁMARAS SECUNDARIAS Y GPS
    // =====================================================================

    static void BuildSideCam(Transform parent, string title, int camId, Vector3 pos)
    {
        Vector2 px = new Vector2(1280, 720);
        Vector2 mt = new Vector2(0.8f, 0.45f);
        var canvas = WorldCanvas("CameraPanel_" + camId, pos, mt, px);
        canvas.transform.SetParent(parent, true);

        var bg = R(canvas.transform, "BG", V(0,0), V(1,1));
        bg.AddComponent<Image>().color = BG_BODY;

        var hdr = R(bg.transform, "Header", V(0, 0.88f), V(1,1));
        hdr.AddComponent<Image>().color = CAM_HDR[camId - 1];
        Label(hdr.transform, "Title", "  " + title, 24, Color.white,
            TextAnchor.MiddleLeft, FontStyle.Bold);

        var vid = R(bg.transform, "VideoDisplay", V(0,0), V(1, 0.88f));
        vid.AddComponent<RawImage>().color = Color.black;

        var ov = R(vid.transform, "Overlay", V(0,0), V(1,1));
        
        // Icono de carga giratorio
        var icon = Label(ov.transform, "icon", "⏳", 159, new Color(1,1,1,0.8f), TextAnchor.MiddleCenter, FontStyle.Normal);
        icon.rectTransform.anchorMin = V(0, 0.5f);
        icon.rectTransform.anchorMax = V(1, 1f);
        var animIcon = icon.gameObject.AddComponent<LoadingAnimator>();
        animIcon.enableRotation = true;
        animIcon.enablePulsing = false;
        animIcon.rotationSpeed = -150f;

        // Texto parpadeante
        var txtWait = Label(ov.transform, "t", "Conectando Cámara " + camId + "...", 53, new Color(1,1,1,0.6f), TextAnchor.MiddleCenter, FontStyle.Normal);
        txtWait.rectTransform.anchorMin = V(0, 0f);
        txtWait.rectTransform.anchorMax = V(1, 0.5f);
        var animTxt = txtWait.gameObject.AddComponent<LoadingAnimator>();
        animTxt.enableRotation = false;
        animTxt.enablePulsing = true;

        var ctrl = canvas.AddComponent<CameraPanelController>();
        ctrl.cameraId = camId; ctrl.videoDisplay = vid.GetComponent<RawImage>();
    }

    static void BuildGPS(Transform parent, Vector3 pos)
    {
        Vector2 px = new Vector2(1920, 1080);
        Vector2 mt = new Vector2(1.2f, 0.675f);
        var canvas = WorldCanvas("[GPS Map Panel]", pos, mt, px);
        canvas.transform.SetParent(parent, true);

        var bg = R(canvas.transform, "BG", V(0,0), V(1,1));
        bg.AddComponent<Image>().color = BG_BODY;

        var hdr = R(bg.transform, "Header", V(0, 0.92f), V(1,1));
        hdr.AddComponent<Image>().color = ACCENT_BLUE;
        Label(hdr.transform, "Title", "GPS MAP", 28, Color.white,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        var ph = R(bg.transform, "WebViewContainer", V(0,0), V(1,0.92f));
        // Add placeholder text in case the WebView fails to load or while it's loading
        var txt = Label(ph.transform, "t", "SATELLITE MAP\nCargando mapa interactivo...", 95, TEXT_DIM, TextAnchor.MiddleCenter, FontStyle.Normal);
        
        // ==========================================
        // 100% NATIVE C# MAP (NO WEBVIEW, NO CRASH)
        // ==========================================
        var mapContainer = new GameObject("NativeMapContainer");
        mapContainer.transform.SetParent(ph.transform, false);
        
        var rt = mapContainer.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        
        // AÑADIR MÁSCARA INFALIBLE (Image + Mask) PARA QUE EL MAPA NO SE SALGA DEL CUADRADO
        var maskImg = mapContainer.AddComponent<UnityEngine.UI.Image>();
        maskImg.color = new Color(1, 1, 1, 1);
        var mask = mapContainer.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = false; // Oculta el fondo blanco, solo recorta
        
        mapContainer.AddComponent<NativeGpsMapScreen>();
        
        // Remove the placeholder text 
        Object.DestroyImmediate(txt.gameObject);

        // Contenedor para botones de mapa (bottom right)
        var mapCtrls = R(canvas.transform, "MapControls", V(0.92f, 0.05f), V(0.98f, 0.12f)); // ajustado para un solo boton
        
        var btnHand = R(mapCtrls.transform, "btn_map_hand", V(0, 0), V(1, 1));
        btnHand.AddComponent<Image>().color = INPUT_BG;
        btnHand.AddComponent<Button>();
        AddBorder(btnHand.transform);
        Label(btnHand.transform, "t", "✋", 79, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    static Vector3 FlatPos(float x, float y)
    {
        return new Vector3(x, EYE + y, DIST);
    }

    /// <summary>Crea una sección con título, borde y LayoutElement.</summary>
    static GameObject Section(Transform parent, string name, string title, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var layoutElem = go.AddComponent<LayoutElement>();
        layoutElem.preferredHeight = h;
        layoutElem.flexibleHeight = 0;

        go.AddComponent<Image>().color = BG_SECTION;

        // Bordes finos (1% del alto/ancho)
        R(go.transform, "bt", V(0,0.995f), V(1,1)).AddComponent<Image>().color = BORDER;
        R(go.transform, "bb", V(0,0), V(1,0.005f)).AddComponent<Image>().color = BORDER;
        R(go.transform, "bl", V(0,0), V(0.005f,1)).AddComponent<Image>().color = BORDER;
        R(go.transform, "br", V(0.995f,0), V(1,1)).AddComponent<Image>().color = BORDER;

        // Título de sección
        Label(R(go.transform, "Title", V(0.04f, 0.88f), V(0.96f, 0.98f)).transform,
            "t", title, 14, TEXT_TITLE, TextAnchor.MiddleLeft, FontStyle.Bold);
        return go;
    }

    /// <summary>Toggle pill visual (óvalo oscuro con un círculo blanco).</summary>
    static GameObject BuildTogglePill(Transform parent, Vector2 aMin, Vector2 aMax)
    {
        var pill = R(parent, "TogglePill", aMin, aMax);
        var img = pill.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 1f); // Dark bg

        var knob = R(pill.transform, "Knob", new Vector2(0.1f, 0.1f), new Vector2(0.4f, 0.9f));
        knob.AddComponent<Image>().color = Color.white;
        
        pill.AddComponent<Button>();
        
        return pill;
    }

    /// <summary>Borde fino alrededor de un elemento (simula border: 1px solid rgba(255,255,255,.1)).</summary>
    static void AddBorder(Transform parent)
    {
        Color bc = new Color(1f,1f,1f,0.10f);
        R(parent, "bT", V(0,0.96f), V(1,1)).AddComponent<Image>().color = bc;
        R(parent, "bB", V(0,0), V(1,0.04f)).AddComponent<Image>().color = bc;
        R(parent, "bL", V(0,0), V(0.01f,1)).AddComponent<Image>().color = bc;
        R(parent, "bR", V(0.99f,0), V(1,1)).AddComponent<Image>().color = bc;
    }

    static void SetupEnvironment()
    {
        var lg = new GameObject("Directional Light");
        var l = lg.AddComponent<Light>();
        l.type = LightType.Directional; l.intensity = 0.3f;
        l.color = new Color(0.85f, 0.85f, 1f);
        lg.transform.rotation = Quaternion.Euler(50, -30, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.05f);
        RenderSettings.skybox = null;
    }

    static void SetupCameraRig()
    {
        string[] guids = AssetDatabase.FindAssets("OVRCameraRig t:Prefab");
        foreach (var guid in guids)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.GetComponent("OVRCameraRig") != null)
            {
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                rig.name = "[XR Camera Rig]";
                rig.transform.position = Vector3.zero;
                return;
            }
        }
        var xr = new GameObject("[XR Camera Rig]");
        var cam = new GameObject("Main Camera");
        cam.tag = "MainCamera"; cam.transform.SetParent(xr.transform);
        cam.transform.localPosition = new Vector3(0, 1.6f, 0);
        var c = cam.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0, 0, 0, 0); // Transparente para evitar error del Validator de Meta
        cam.tag = "MainCamera";

        // Añadir gestor de Passthrough
        var manager = xr.AddComponent<OVRManager>();
        manager.isInsightPassthroughEnabled = true;

        var pt = xr.AddComponent<OVRPassthroughLayer>();
        pt.projectionSurfaceType = OVRPassthroughLayer.ProjectionSurfaceType.Reconstructed;
        pt.hidden = true; // Arranca oculto para modo VR estándar
    }

    static void SetupEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        // Añadir InputSystemUIInputModule en lugar de StandaloneInputModule para Unity 6
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    static GameObject WorldCanvas(string name, Vector3 pos, Vector2 mt, Vector2 px)
    {
        var go = new GameObject(name);
        go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        
        var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 4f;
        scaler.referencePixelsPerUnit = 100f;
        
        go.GetComponent<RectTransform>().sizeDelta = px;
        go.transform.localScale = new Vector3(mt.x / px.x, mt.y / px.y, 1f);
        go.transform.position = pos;
        // Plano: sin rotación
        go.transform.rotation = Quaternion.identity;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    static GameObject R(Transform p, string n, Vector2 amin, Vector2 amax)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return go;
    }

    static GameObject Btn(Transform p, string id, string label, Color col, int fs = 16)
    {
        var go = new GameObject(id); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>(); img.color = col;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var cs = btn.colors;
        cs.normalColor = col;
        cs.highlightedColor = col * 1.15f;
        cs.pressedColor = col * 0.8f;
        btn.colors = cs;

        Label(go.transform, "Lbl", label, fs, Color.white,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        return go;
    }

    static TextMeshProUGUI Label(Transform p, string n, string text, int fs,
        Color col, TextAnchor align, FontStyle style)
    {
        var go = R(p, n, Vector2.zero, Vector2.one);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.color = col; 

        // Mapeo de alineación
        if (align == TextAnchor.MiddleCenter) t.alignment = TextAlignmentOptions.Center;
        else if (align == TextAnchor.MiddleLeft) t.alignment = TextAlignmentOptions.Left;
        else if (align == TextAnchor.UpperCenter) t.alignment = TextAlignmentOptions.Top;
        else t.alignment = TextAlignmentOptions.Center; // Fallback

        // Mapeo de estilo
        if (style == FontStyle.Bold) t.fontStyle = FontStyles.Bold;
        else t.fontStyle = FontStyles.Normal;

        // Autoajuste nítido
        t.enableAutoSizing = true;
        t.fontSizeMin = 8;
        t.fontSizeMax = Mathf.Max(fs * 2, 32);
        return t;
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);

    static Color Hex(string h)
    {
        Color c; ColorUtility.TryParseHtmlString(h, out c); return c;
    }
}
