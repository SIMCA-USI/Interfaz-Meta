using UnityEngine;
using System.Collections;
using TLab.WebView;

/// <summary>
/// Gestiona la inicialización, actualización de frames y carga de URL para el
/// WebView de TLab que muestra el mapa del GPS (o cualquier otra web).
/// </summary>
public class GpsMapScreen : MonoBehaviour
{
    private Browser _browser;
    private bool _urlLoaded = false;
    private bool _initStarted = false;

    void Start()
    {
        _browser = GetComponent<Browser>();
        if (_browser == null)
            _browser = GetComponentInChildren<Browser>(true);

        if (_browser == null)
        {
            Debug.LogError("[GpsMapScreen] ERROR: No se encontró Browser de TLab en este GameObject.");
            return;
        }

        // DEBUG VISUAL INNEGABLE: Spawneamos un Cubo Rojo gigante frente a la pantalla
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(transform, false);
        cube.transform.localPosition = new Vector3(0, 0, -1f);
        cube.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        cube.GetComponent<Renderer>().material.color = Color.red;

        Debug.LogError("[GpsMapScreen] ¡SCRIPT ARRANCADO Y EJECUTÁNDOSE!");
        StartCoroutine(ConnectAndLoad());
    }

    void Update()
    {
        if (_browser != null && _browser.state == FragmentCapture.State.Initialized)
        {
            // CRÍTICO: Esto es lo que pinta la web en la textura de Unity en cada frame.
            _browser.UpdateFrame();
            _browser.DispatchMessageQueue();
        }
    }

    IEnumerator ConnectAndLoad()
    {
        // 1. Iniciar el motor de WebView (esto lo hacía BrowserSample en los ejemplos de TLab)
        if (_browser.state == FragmentCapture.State.None && !_initStarted)
        {
            Debug.Log("[GpsMapScreen] Llamando a _browser.Init()...");
            _initStarted = true;
            _browser.Init();
        }

        // 2. Esperar a que FlaskApiClient tenga la IP de la Raspberry/Servidor
        while (FlaskApiClient.Instance == null || string.IsNullOrEmpty(FlaskApiClient.Instance.serverIp))
        {
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log($"[GpsMapScreen] IP obtenida: {FlaskApiClient.Instance.serverIp}. Esperando a que el WebView termine de inicializarse...");

        // 3. Esperar a que el WebView nativo esté listo para recibir comandos
        while (_browser.state != FragmentCapture.State.Initialized)
        {
            yield return new WaitForSeconds(0.2f);
        }

        // 4. Cargar la URL de nuestro módulo de GPS (se le pasa un timestamp para evitar caché)
        string ip = FlaskApiClient.Instance.serverIp;
        int port = FlaskApiClient.Instance.serverPort;
        string t = System.DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        
        // El usuario pidió probar con YouTube para descartar problemas de WebGL/PMTiles en el navegador nativo de Quest.
        string url = "https://www.youtube.com";

        Debug.Log($"[GpsMapScreen] WebView Listo. Cargando URL: {url}");
        _browser.LoadUrl(url);
        _urlLoaded = true;
    }

    /// <summary>
    /// Permite recargar la página web bajo demanda (por ejemplo, al pulsar un botón).
    /// </summary>
    public void ForceReload()
    {
        if (!_urlLoaded || _browser == null || _browser.state != FragmentCapture.State.Initialized) 
        {
            Debug.LogWarning("[GpsMapScreen] No se puede recargar: WebView no está inicializado.");
            return;
        }

        string ip = FlaskApiClient.Instance.serverIp;
        int port = FlaskApiClient.Instance.serverPort;
        string t = System.DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        string url = $"http://{ip}:{port}/gps_only?t={t}";

        Debug.Log($"[GpsMapScreen] Forzando recarga de URL: {url}");
        _browser.LoadUrl(url);
    }
}
