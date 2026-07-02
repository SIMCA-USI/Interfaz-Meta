using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente que se adjunta a cada panel de cámara en la sala de control VR.
/// Guarda el ID de la cámara y la referencia al RawImage donde se mostrará el vídeo.
/// En la Fase 4 se conectará con MjpegStreamReceiver para mostrar el stream de vídeo.
/// </summary>
public class CameraPanelController : MonoBehaviour
{
    [Header("Configuración de Cámara")]
    [Tooltip("ID de la cámara (1-5) que corresponde al endpoint Flask /video_feed/{id}")]
    public int cameraId = 1;

    [Header("Referencias UI")]
    [Tooltip("RawImage donde se mostrará el stream de vídeo")]
    public RawImage videoDisplay;

    [Header("Estado")]
    [Tooltip("Si esta cámara está recibiendo vídeo actualmente")]
    public bool isStreaming = false;

    /// <summary>
    /// Actualiza la textura del panel con un frame nuevo.
    /// Será llamado por MjpegStreamReceiver en la Fase 4.
    /// </summary>
    public void UpdateFrame(Texture2D frame)
    {
        if (videoDisplay != null && frame != null)
        {
            videoDisplay.texture = frame;
            isStreaming = true;
        }
    }

    /// <summary>
    /// Muestra el mensaje de "sin señal" cuando no hay stream.
    /// </summary>
    public void ShowNoSignal()
    {
        if (videoDisplay != null)
        {
            videoDisplay.texture = null;
            videoDisplay.color = new Color(0.12f, 0.12f, 0.18f);
            isStreaming = false;
        }
    }
}
