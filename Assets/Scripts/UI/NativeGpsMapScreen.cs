using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;

public class NativeGpsMapScreen : MonoBehaviour
{
    private RectTransform _arrowIcon;
    private bool _isRunning = false;
    private float _lastLat = 0f;
    private float _lastLon = 0f;
    private int _zoom = 18;
    
    private RectTransform _pathContainer;
    private List<RectTransform> _lineSegments = new List<RectTransform>();

    [System.Serializable]
    public class TelemetryResponse
    {
        public List<List<float>> position_history;
        public float orientation;
    }

    void Start()
    {
        // 1. Preparar la UI nativa
        // El fondo gris oscuro ahora lo proporciona el Image de SceneBuilder (Mask)
        if (TryGetComponent<UnityEngine.UI.Image>(out var bgImage))
        {
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f); // Fondo oscuro por si no hay internet
        }

        // Contenedor del rastro (debajo de la flecha)
        var pathObj = new GameObject("PathContainer");
        _pathContainer = pathObj.AddComponent<RectTransform>();
        _pathContainer.SetParent(transform, false);
        _pathContainer.anchorMin = new Vector2(0.5f, 0.5f);
        _pathContainer.anchorMax = new Vector2(0.5f, 0.5f);
        _pathContainer.anchoredPosition = Vector2.zero;

        // Crear el icono del vehículo (flecha)
        var arrowObj = new GameObject("VehicleArrow");
        _arrowIcon = arrowObj.AddComponent<RectTransform>();
        _arrowIcon.SetParent(transform, false);
        _arrowIcon.sizeDelta = new Vector2(150, 150); // MÁS GRANDE PARA VR
        
        var arrowImg = arrowObj.AddComponent<RawImage>();
        arrowImg.color = Color.white; // Para que se vea la textura
        
        Debug.Log("[NativeGps] Iniciando mapa nativo 100% C# sin WebView.");
        
        StartCoroutine(DownloadArrow());
        StartCoroutine(PollGpsData());
    }

    IEnumerator DownloadArrow()
    {
        // Esperar a que FlaskApiClient esté conectado de verdad (Auto-Discovery terminado)
        while (FlaskApiClient.Instance == null || !FlaskApiClient.Instance.isConnected)
        {
            yield return new WaitForSeconds(0.5f);
        }

        string url = $"http://{FlaskApiClient.Instance.serverIp}:{FlaskApiClient.Instance.serverPort}/static/arrow.png";
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            // Timeout corto para que no se quede colgado si no hay servidor
            req.timeout = 3;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                if (_arrowIcon != null) 
                {
                    _arrowIcon.GetComponent<RawImage>().texture = tex;
                    _arrowIcon.GetComponent<RawImage>().color = Color.white;
                }
            }
            else
            {
                if (_arrowIcon != null) 
                {
                    _arrowIcon.GetComponent<RawImage>().color = Color.green; // Fallback pelota verde
                }
            }
        }
    }

    IEnumerator PollGpsData()
    {
        while (true)
        {
            if (FlaskApiClient.Instance != null && !string.IsNullOrEmpty(FlaskApiClient.Instance.serverIp))
            {
                string url = $"http://{FlaskApiClient.Instance.serverIp}:{FlaskApiClient.Instance.serverPort}/get_full_state";
                using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
                {
                    webRequest.timeout = 2; // EVITAR QUE SE QUEDE COLGADO SI LA IP ES INCORRECTA
                    yield return webRequest.SendWebRequest();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            string json = webRequest.downloadHandler.text;
                            
                            // Parse orientation
                            float orientation = 0f;
                            int oriIdx = json.IndexOf("\"orientation\":");
                            if (oriIdx != -1)
                            {
                                int start = oriIdx + 14;
                                int end = json.IndexOf(",", start);
                                if (end == -1) end = json.IndexOf("}", start);
                                string val = json.Substring(start, end - start).Trim();
                                float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out orientation);
                            }

                            // Parse last position in position_history
                            float lat = 0f;
                            float lon = 0f;
                            bool posFound = false;
                            
                            // Lista para guardar todo el historial y dibujar la línea azul
                            List<Vector2> history = new List<Vector2>();

                            int histIdx = json.IndexOf("\"position_history\"");
                            if (histIdx != -1)
                            {
                                int nextKeyIdx = json.IndexOf("\"", histIdx + 20); // Find next JSON key to bound search
                                if (nextKeyIdx == -1) nextKeyIdx = json.Length;
                                
                                string historyBlock = json.Substring(histIdx, nextKeyIdx - histIdx);
                                
                                // Extraer todos los pares de coordenadas usando Regex
                                var matches = System.Text.RegularExpressions.Regex.Matches(historyBlock, @"\[\s*(-?\d+\.\d+)\s*,\s*(-?\d+\.\d+)\s*\]");
                                foreach (System.Text.RegularExpressions.Match m in matches)
                                {
                                    if (m.Groups.Count >= 3)
                                    {
                                        float pLat, pLon;
                                        if (float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pLat) &&
                                            float.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pLon))
                                        {
                                            history.Add(new Vector2(pLat, pLon));
                                        }
                                    }
                                }

                                if (history.Count > 0)
                                {
                                    lat = history[history.Count - 1].x;
                                    lon = history[history.Count - 1].y;
                                    posFound = true;
                                }
                            }

                            if (posFound)
                            {
                                // 1. Actualizar rotación de la flecha
                                _arrowIcon.localRotation = Quaternion.Euler(0, 0, -orientation);

                                // 2. Dibujar el rastro (línea azul #3b82f6)
                                DrawPath(history);

                                // 3. Si nos movimos significativamente, descargar nuevo tile (opcional si hay internet)
                                if (Mathf.Abs(lat - _lastLat) > 0.0001f || Mathf.Abs(lon - _lastLon) > 0.0001f)
                                {
                                    _lastLat = lat;
                                    _lastLon = lon;
                                    StartCoroutine(DownloadOsmTile(lat, lon, _zoom));
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("[NativeGps] Error parseando JSON: " + e.Message);
                        }
                    }
                }
            }
            yield return new WaitForSeconds(0.5f); // Polling más rápido como el socket.io
        }
    }

    // ==========================================
    // MATEMÁTICA EXACTA WEB MERCATOR (Igual que Leaflet)
    // ==========================================
    Vector2 LatLonToMercator(float lat, float lon, int zoom)
    {
        float n = Mathf.Pow(2, zoom);
        float x = ((lon + 180f) / 360f) * n;
        float y = (1f - Mathf.Log(Mathf.Tan(lat * Mathf.PI / 180f) + 1f / Mathf.Cos(lat * Mathf.PI / 180f)) / Mathf.PI) / 2f * n;
        return new Vector2(x, y);
    }

    void DrawPath(List<Vector2> history)
    {
        if (history.Count < 2) return;
        
        // El centro de la pantalla es el último punto
        Vector2 centerLatLon = history[history.Count - 1];
        Vector2 centerMerc = LatLonToMercator(centerLatLon.x, centerLatLon.y, _zoom);
        float tileSize = 600f; // Mismo tamaño que usamos para los tiles

        // Crear/Reciclar segmentos
        while (_lineSegments.Count < history.Count - 1)
        {
            var seg = new GameObject("Segment");
            seg.transform.SetParent(_pathContainer, false);
            var img = seg.AddComponent<RawImage>();
            img.color = new Color(59f/255f, 130f/255f, 246f/255f, 0.7f);
            var rt = seg.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0, 0.5f);
            _lineSegments.Add(rt);
        }

        // Ocultar no usados
        for (int i = history.Count - 1; i < _lineSegments.Count; i++)
            _lineSegments[i].gameObject.SetActive(false);

        // Dibujar
        for (int i = 0; i < history.Count - 1; i++)
        {
            Vector2 p1Merc = LatLonToMercator(history[i].x, history[i].y, _zoom);
            Vector2 p2Merc = LatLonToMercator(history[i+1].x, history[i+1].y, _zoom);

            Vector2 p1 = new Vector2((p1Merc.x - centerMerc.x) * tileSize, -(p1Merc.y - centerMerc.y) * tileSize);
            Vector2 p2 = new Vector2((p2Merc.x - centerMerc.x) * tileSize, -(p2Merc.y - centerMerc.y) * tileSize);

            var rt = _lineSegments[i];
            rt.gameObject.SetActive(true);
            
            float distance = Vector2.Distance(p1, p2);
            float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;

            rt.localPosition = p1;
            rt.sizeDelta = new Vector2(distance, 12f);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }

    // Diccionario para almacenar los 9 RawImages de los tiles
    private Dictionary<Vector2Int, RawImage> _tileImages = new Dictionary<Vector2Int, RawImage>();

    IEnumerator DownloadOsmTile(float lat, float lon, int zoom)
    {
        // Calcular coordenadas exactas en Web Mercator para sacar el offset
        Vector2 exactMerc = LatLonToMercator(lat, lon, zoom);
        int centerX = (int)Mathf.Floor(exactMerc.x);
        int centerY = (int)Mathf.Floor(exactMerc.y);

        // Offset fraccional para colocar el vehículo EXACTAMENTE en su coordenada
        float fractionalX = exactMerc.x - centerX;
        float fractionalY = exactMerc.y - centerY;
        
        float tileSize = 600f; // Tamaño en píxeles de cada tile en la UI
        float offsetX = (0.5f - fractionalX) * tileSize;
        float offsetY = -(0.5f - fractionalY) * tileSize;

        // Descargar un grid de 3x3 tiles
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int x = centerX + dx;
                int y = centerY + dy;
                Vector2Int tileKey = new Vector2Int(x, y);

                // Si no existe el RawImage para este tile, lo creamos
                if (!_tileImages.ContainsKey(tileKey))
                {
                    GameObject tileObj = new GameObject($"Tile_{x}_{y}");
                    RectTransform tileRt = tileObj.AddComponent<RectTransform>();
                    
                    // Colocarlo debajo del contenedor de rutas
                    tileRt.SetParent(transform, false);
                    tileRt.SetSiblingIndex(0); 
                    
                    tileRt.sizeDelta = new Vector2(tileSize, tileSize);
                    
                    RawImage ri = tileObj.AddComponent<RawImage>();
                    ri.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                    _tileImages[tileKey] = ri;

                    StartCoroutine(FetchTileTexture(x, y, zoom, ri));
                }

                // Posicionar el tile con el offset preciso (se actualiza constantemente si el vehículo se mueve dentro del mismo tile central)
                if (_tileImages.TryGetValue(tileKey, out RawImage existingRi))
                {
                    existingRi.rectTransform.anchoredPosition = new Vector2(dx * tileSize + offsetX, -dy * tileSize + offsetY);
                }
            }
        }
        yield return null;
    }

    IEnumerator FetchTileTexture(int x, int y, int zoom, RawImage targetImage)
    {
        // Usamos CartoDB Voyager, que es muy parecido al OSM default pero más bonito y NO bloquea por 403 Forbidden a Unity
        string url = $"https://a.basemaps.cartocdn.com/rastertiles/voyager/{zoom}/{x}/{y}.png";
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            req.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success && targetImage != null)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                targetImage.texture = tex;
                targetImage.color = Color.white;
            }
        }
    }
}
