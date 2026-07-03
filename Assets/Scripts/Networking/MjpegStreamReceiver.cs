using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;

/// <summary>
/// Decodificador de stream MJPEG → Texture2D.
/// Se adjunta a cada panel de cámara (CameraPanelController).
/// Lee el endpoint /video_feed/{cam_id} del servidor Flask y decodifica
/// cada frame JPEG para mostrarlo en el RawImage del Canvas.
/// 
/// Espera a que FlaskApiClient esté conectado antes de intentar el stream.
/// Auto-reconecta si se pierde la conexión.
/// </summary>
public class MjpegStreamReceiver : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("ID de la cámara (1-5)")]
    public int cameraId = 1;

    [Tooltip("RawImage donde se mostrará el vídeo")]
    public RawImage targetDisplay;

    [Header("Estado")]
    public bool isStreaming = false;
    public float fps = 0f;

    // ── Internos ──
    Texture2D _tex;
    byte[] _latestFrame;
    bool _hasNewFrame = false;
    private readonly object _frameLock = new object();
    private float _lastDecodeTime = 0f;
    private const float MIN_DECODE_INTERVAL = 1f / 15f; // Limitar a 15 FPS para no saturar CPU
    CancellationTokenSource _cts;
    int _frameCount = 0;
    float _fpsTimer = 0f;
    string _currentUrl = "";

    // ── Buffer para lectura del stream ──
    const int BUFFER_SIZE = 512 * 1024; // 512 KB
    const int MAX_FRAME_SIZE = 1024 * 1024; // 1 MB max por frame JPEG

    public enum ConnectionState { Waiting, Connected, Reconnecting }
    private ConnectionState _connState = ConnectionState.Waiting;

    void OnEnable()
    {
        // IMPORTANTE: .NET/Mono tiene un límite de 2 conexiones simultáneas por defecto a la misma IP.
        // Si no lo subimos, las cámaras 3, 4 y 5 se quedarán atascadas para siempre.
        System.Net.ServicePointManager.DefaultConnectionLimit = 10;
        
        StartCoroutine(WaitForConnectionThenStream());
    }

    /// <summary>
    /// Espera a que FlaskApiClient exista Y esté conectado al servidor.
    /// Así no intenta abrir un stream a una IP incorrecta.
    /// </summary>
    IEnumerator WaitForConnectionThenStream()
    {
        // Esperar a que FlaskApiClient exista
        while (FlaskApiClient.Instance == null)
            yield return new WaitForSeconds(0.5f);

        Debug.Log($"[MJPEG] Cámara {cameraId}: Esperando conexión con servidor...");

        // Esperar a que FlaskApiClient se conecte realmente
        while (!FlaskApiClient.Instance.isConnected)
            yield return new WaitForSeconds(1f);

        // --- Carga en Cascada (Staggered Loading) ---
        // Retrasa la carga 0.6 segundos por cámara para no saturar la CPU al cargar las 5 a la vez
        float delay = (cameraId - 1) * 0.6f;
        if (delay > 0)
        {
            Debug.Log($"[MJPEG] Cámara {cameraId}: En cola de conexión (esperando {delay:F1}s)...");
            yield return new WaitForSeconds(delay);
        }

        Debug.Log($"[MJPEG] Cámara {cameraId}: Servidor conectado, iniciando stream.");
        _connState = ConnectionState.Connected;
        StartStreaming();
    }

    void OnDisable()
    {
        StopStreaming();
    }

    void OnDestroy()
    {
        StopStreaming();
        if (_tex != null) Destroy(_tex);
    }

    public void StartStreaming()
    {
        StopStreaming();

        if (FlaskApiClient.Instance == null) return;

        _currentUrl = $"http://{FlaskApiClient.Instance.serverIp}:{FlaskApiClient.Instance.serverPort}/video_feed/{cameraId}";
        
        _cts = new CancellationTokenSource();
        if (_tex == null)
            _tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        
        isStreaming = true;
        Debug.Log($"[MJPEG] Iniciando stream de cámara {cameraId}: {_currentUrl}");

        Task.Run(() => ReadMjpegStream(_currentUrl, _cts.Token));
    }

    public void StopStreaming()
    {
        isStreaming = false;
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    void Update()
    {
        // FPS counter
        _fpsTimer += Time.deltaTime;
        if (_fpsTimer >= 1f)
        {
            fps = _frameCount / _fpsTimer;
            _frameCount = 0;
            _fpsTimer = 0f;
        }

        // Aplicar frame nuevo al RawImage (solo en el hilo principal)
        if (_hasNewFrame && _latestFrame != null && targetDisplay != null)
        {
            if (Time.time - _lastDecodeTime >= MIN_DECODE_INTERVAL)
            {
                _lastDecodeTime = Time.time;
                try
                {
                    if (_tex == null)
                    {
                        _tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    }
                    
                    if (_tex.LoadImage(_latestFrame))
                    {
                        targetDisplay.texture = _tex;
                        targetDisplay.color = Color.white; // IMPORTANTE: Quitar el Color.black inicial
                        
                        _connState = ConnectionState.Connected;
                        
                        _frameCount++;
                        if (_frameCount % 50 == 0) Debug.Log($"[MJPEG] Cámara {cameraId}: Decodificados 50 frames correctamente (Tamaño: {_latestFrame.Length} bytes)");
                    }
                    else
                    {
                        Debug.LogWarning($"[MJPEG] Error: LoadImage devolvió false para cámara {cameraId}. Tamaño frame: {_latestFrame.Length} bytes");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[MJPEG] Error cargando frame cam {cameraId}: {e.Message}");
                }
                
                _hasNewFrame = false;
            }
        }

        // --- Mostrar Overlay si no hay frame ---
        if (targetDisplay != null && targetDisplay.transform != null)
        {
            var overlay = targetDisplay.transform.Find("Overlay");
            if (overlay != null)
            {
                bool shouldShow = (_connState != ConnectionState.Connected || (!isStreaming && _latestFrame == null));
                if (overlay.gameObject.activeSelf != shouldShow)
                {
                    overlay.gameObject.SetActive(shouldShow);
                }
                
                if (shouldShow)
                {
                    var txtObj = overlay.Find("t");
                    if (txtObj != null)
                    {
                        var tmpro = txtObj.GetComponent<TMPro.TextMeshProUGUI>();
                        string msg = (_connState == ConnectionState.Reconnecting) ? "Reconectando..." : "Esperando conexión...";
                        if (tmpro != null) tmpro.text = msg;
                    }
                }
            }
        }
    }

    // =====================================================================
    // HILO SECUNDARIO: Lee el stream MJPEG
    // =====================================================================

    async Task ReadMjpegStream(string url, CancellationToken ct)
    {
        byte[] buffer = new byte[32768];
        byte[] frameBuffer = new byte[MAX_FRAME_SIZE];
        byte[] boundary = System.Text.Encoding.ASCII.GetBytes("--frame");

        while (!ct.IsCancellationRequested)
        {
            System.Net.HttpWebRequest request = null;
            System.Net.HttpWebResponse response = null;
            Stream stream = null;

            try
            {
                request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                request.Timeout = 5000;
                request.ReadWriteTimeout = 5000;
                request.KeepAlive = true;

                response = (System.Net.HttpWebResponse)await Task.Run(() => request.GetResponse(), ct);
                stream = response.GetResponseStream();

                Debug.Log($"[MJPEG] Cámara {cameraId}: Stream conectado OK.");

                int frameWritePos = 0;

                while (!ct.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead <= 0) break;

                    if (frameWritePos + bytesRead > MAX_FRAME_SIZE)
                    {
                        frameWritePos = 0; // Overflow guard
                    }

                    Array.Copy(buffer, 0, frameBuffer, frameWritePos, bytesRead);
                    frameWritePos += bytesRead;

                    // Procesar todos los frames que haya en el buffer
                    while (true)
                    {
                        int boundaryIdx = -1;
                        int maxSearchIdx = frameWritePos - boundary.Length;
                        for (int i = 0; i <= maxSearchIdx; i++)
                        {
                            i = Array.IndexOf(frameBuffer, boundary[0], i, maxSearchIdx - i + 1);
                            if (i == -1) break;

                            bool match = true;
                            for (int j = 1; j < boundary.Length; j++)
                            {
                                if (frameBuffer[i + j] != boundary[j])
                                {
                                    match = false;
                                    break;
                                }
                            }
                            if (match)
                            {
                                boundaryIdx = i;
                                break;
                            }
                        }

                        if (boundaryIdx != -1)
                        {
                            int jpegStart = -1;
                            for (int i = 0; i < boundaryIdx - 1; i++)
                            {
                                if (frameBuffer[i] == 0xFF && frameBuffer[i + 1] == 0xD8)
                                {
                                    jpegStart = i;
                                    break;
                                }
                            }

                            if (jpegStart != -1)
                            {
                                int jpegLength = boundaryIdx - jpegStart;
                                
                                // Buscar el final real del JPEG (FF D9) para evitar basura al final (\r\n)
                                int jpegEnd = -1;
                                for (int i = boundaryIdx - 2; i > jpegStart; i--)
                                {
                                    if (frameBuffer[i] == 0xFF && frameBuffer[i + 1] == 0xD9)
                                    {
                                        jpegEnd = i + 2; // Incluir el marcador FF D9
                                        break;
                                    }
                                }

                                if (jpegEnd != -1)
                                {
                                    jpegLength = jpegEnd - jpegStart;
                                }

                                byte[] finalFrame = new byte[jpegLength];
                                Array.Copy(frameBuffer, jpegStart, finalFrame, 0, jpegLength);
                                _latestFrame = finalFrame;
                                _hasNewFrame = true;
                            }
                            int remainingStart = boundaryIdx + boundary.Length;
                            int remainingLength = frameWritePos - remainingStart;
                            if (remainingLength > 0)
                            {
                                Array.Copy(frameBuffer, remainingStart, frameBuffer, 0, remainingLength);
                                frameWritePos = remainingLength;
                            }
                            else
                            {
                                frameWritePos = 0;
                            }
                        }
                        else
                        {
                            // No hay más boundaries completos, necesitamos leer más de la red
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                if (!ct.IsCancellationRequested)
                {
                    Debug.LogWarning($"[MJPEG] Cámara {cameraId} error: {e.GetType().Name}: {e.Message}. Reintentando en 3s...");
                    _connState = ConnectionState.Reconnecting;
                    try { await Task.Delay(3000, ct); } catch { break; }
                }
            }
            finally
            {
                stream?.Dispose();
                response?.Dispose();
            }
        }

        Debug.Log($"[MJPEG] Stream cámara {cameraId} detenido.");
    }
}
