using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.Threading.Tasks;
using System.Threading;

/// <summary>
/// Fase 2: Auto-Discovery mediante UDP Broadcast.
/// Escucha en el puerto UDP 5555 los anuncios del servidor Flask.
/// Cuando recibe un anuncio, auto-configura la IP de FlaskApiClient.
/// </summary>
public class ServerDiscovery : MonoBehaviour
{
    UdpClient _udpClient;
    CancellationTokenSource _cts;

    // Se actualiza desde el hilo secundario, se lee en el principal
    string _detectedIp = "";
    int _detectedPort = 0;
    string _detectedVehicle = "";
    bool _hasNewServer = false;

    void Start()
    {
        Debug.Log("[ServerDiscovery] Iniciando escucha UDP en puerto 5555...");
        _cts = new CancellationTokenSource();
        Task.Run(() => ListenForBroadcast(_cts.Token));
    }

    void OnDestroy()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        if (_udpClient != null)
        {
            _udpClient.Close();
        }
    }

    void Update()
    {
        if (_hasNewServer)
        {
            _hasNewServer = false;
            
            // Si ya estamos conectados al mismo, no hacer nada
            if (FlaskApiClient.Instance != null && 
                FlaskApiClient.Instance.serverIp == _detectedIp &&
                FlaskApiClient.Instance.serverPort == _detectedPort)
                return;

            Debug.Log($"[ServerDiscovery] ¡Vehículo detectado! {_detectedVehicle} en {_detectedIp}:{_detectedPort}");

            if (FlaskApiClient.Instance != null)
            {
                FlaskApiClient.Instance.serverIp = _detectedIp;
                FlaskApiClient.Instance.serverPort = _detectedPort;
                Debug.Log($"[ServerDiscovery] FlaskApiClient actualizado a {_detectedIp}:{_detectedPort}");
                
                // Guardar en PlayerPrefs para el próximo arranque (evita pantalla negra antes del discovery)
                PlayerPrefs.SetString("ServerIP", _detectedIp);
                PlayerPrefs.SetString("ServerPort", _detectedPort.ToString());
                PlayerPrefs.Save();

                // Recargar el mapa si la interfaz ya está instanciada
                var controlPanel = FindObjectOfType<ControlPanelUI>();
                if (controlPanel != null)
                {
                    controlPanel.ReloadMap(_detectedIp, _detectedPort);
                }
            }
        }
    }

    async Task ListenForBroadcast(CancellationToken ct)
    {
        try
        {
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 5555));

            Debug.Log("[ServerDiscovery] Socket UDP abierto correctamente en puerto 5555.");

            while (!ct.IsCancellationRequested)
            {
                var result = await _udpClient.ReceiveAsync();
                string jsonStr = Encoding.UTF8.GetString(result.Buffer);

                Debug.Log($"[ServerDiscovery] Paquete UDP recibido desde {result.RemoteEndPoint}: {jsonStr}");

                try
                {
                    var data = JsonUtility.FromJson<DiscoveryData>(jsonStr);
                    if (data != null && data.service == "vehicle_control")
                    {
                        _detectedIp = data.ip;
                        _detectedPort = data.port;
                        _detectedVehicle = data.vehicle;
                        _hasNewServer = true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ServerDiscovery] Error parseando broadcast: {e.Message}");
                }
            }
        }
        catch (ObjectDisposedException) { /* Ignorar al cerrar */ }
        catch (SocketException e)
        {
            Debug.LogError($"[ServerDiscovery] Error de socket UDP (puede que el puerto 5555 esté ocupado): {e.Message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ServerDiscovery] Error en UDP listener: {e.Message}");
        }
    }

    [Serializable]
    class DiscoveryData
    {
        public string service;
        public string vehicle;
        public string ip;
        public int port;
    }
}
