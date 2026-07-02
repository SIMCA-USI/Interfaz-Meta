# Meta Quest VR Vehicle Control Interface

Este repositorio contiene el proyecto de Unity para el control y teleoperación inmersiva en Realidad Virtual (Meta Quest 2/3/Pro) del vehículo autónomo. La interfaz 3D se genera dinámicamente para replicar la interfaz web (HTML/JS) del backend ROS 2, ofreciendo transmisión de vídeo de baja latencia y control total.

## 🚀 Instalación y Configuración (Fase 0)

Para que otra persona pueda clonar este repositorio y compilarlo en unas Meta Quest sin volverse loco, hay que seguir estos pasos estrictamente:

1. **Instalar Unity 2022.3 LTS** (o superior) desde Unity Hub.
2. Durante la instalación de Unity, asegúrate de marcar los módulos:
   - **Android Build Support**
   - **OpenJDK**, **Android SDK & NDK Tools**
3. Abre el proyecto. En Unity, ve a `File > Build Settings`. Selecciona **Android** y dale a **Switch Platform**.
4. Ve a `Edit > Project Settings > XR Plug-in Management` e instala el plugin de XR. Marca la pestaña de **Oculus** en la sección de Android.
5. *(Importante)* Descarga e importa el [Oculus Integration SDK (Meta XR Core SDK)](https://assetstore.unity.com/packages/tools/integration/meta-xr-core-sdk-269657) desde la Asset Store si no está ya cacheado en el proyecto.

## 🏗 Arquitectura del Proyecto (Fases 1-7)

Este proyecto no usa *Prefabs* estáticos para la interfaz principal, sino que se genera proceduralmente para asegurar consistencia milimétrica con la versión Web y evitar conflictos de Git en los archivos `.scene`.

### Fase 1: Generador Dinámico de la Sala 3D (`SceneBuilder.cs`)
En la barra superior de Unity verás un menú llamado `INSIA > Crear Sala de Control (Rediseño Web)`. Al pulsarlo, un script construye de cero:
- El entorno con iluminación plana y el `OVRCameraRig`.
- Los paneles curvos de vídeo (CAM 2, 3, 4, 5).
- El panel de control central (copia exacta 1:1 del panel HTML).
- La pantalla superior para el mapa GPS interactivo.

### Fase 2: Autodescubrimiento del Servidor (`ServerDiscovery.cs`)
Las Quest no tienen IP estática para el backend. Este script escucha en segundo plano broadcasts UDP (puerto 5051) enviados por el vehículo para **encontrar la IP del servidor Flask automáticamente** e inyectarla en el sistema sin necesidad de teclearla en VR.

### Fase 3: Puente de Comunicación (`FlaskApiClient.cs`)
Un cliente HTTP/REST optimizado que se encarga de realizar todas las peticiones `GET` y `POST` al backend de ROS 2:
- Carga de telemetría (velocidad, roll, pitch).
- Activación de paradas de emergencia (analizando múltiples razones simultáneas).
- Comandos de grabación de rutas, Follow Me y Waypoints.

### Fase 4 y 7: Controlador UI y VR Interactions (`ControlPanelUI.cs`)
Conecta los botones físicos virtuales con la API. 
- **Teclado VR Nativo**: Hemos acoplado a los `TMP_InputField` scripts para que el sistema operativo de Quest abra su teclado flotante.
- **ScrollRestricciones**: Implementado `NoDragScrollRect.cs` para evitar desplazamientos accidentales del panel al apuntar con el láser.

### Fase 5: Transmisión de Vídeo (MJPEG Streaming)
En lugar de usar WebRTC (muy pesado para VR en Unity), usamos MJPEG directo (`MjpegStreamReceiver.cs`). Extraemos los frames delimitados por `boundary` directamente de la red. Se usa `Array.IndexOf` a bajo nivel (C++) para no saturar la CPU de las Quest buscando el byte de inicio.

### Fase 6: Mapa GPS Interactivo 3D (`TLabWebView`)
Como el mapa usa `Leaflet` y `.pmtiles` vectoriales nativos de la web, inyectamos un navegador Chrome embebido dentro del motor Unity usando el plugin `TLabWebView`.

---

## ⚠️ Fallos Conocidos y Soluciones (Troubleshooting)

Si intentas modificar el proyecto, ten en cuenta estos *gotchas* descubiertos durante el desarrollo:

1. **El mapa GPS se ve negro o no carga**:
   - Unity por defecto compila para Android sin aceleración por hardware en los webviews. Ve a `Assets/Plugins/Android/AndroidManifest.xml` y asegúrate de que el tag `<application>` tiene `android:hardwareAccelerated="true"` y `android:usesCleartextTraffic="true"`.
2. **Lag masivo al activar múltiples cámaras**:
   - ¡Cuidado con el parsing de vídeo! Si intentas leer el bloque de vídeo MJPEG iterando `for` byte por byte en C#, los FPS caerán a 3. Siempre usa `System.Array.IndexOf`.
3. **El menú INSIA > Crear Sala de Control no hace nada**:
   - Si hay un error de compilación (texto rojo en la consola de Unity), Unity **congela** todos los scripts de Editor. Si tienes un error tipográfico, no podrás regenerar la escena hasta corregirlo.
4. **El botón se queda rojo al dejar de apuntar**:
   - Unity cambia al `NormalColor` por defecto cuando pierdes el *Hover*. El script ahora modifica el `ColorBlock` permanentemente para evitar esto, forzando la sincronización de colores con el estado real del backend.
