# 🥽 Control de Vehículo Autónomo en Realidad Virtual (Meta Quest)

Este repositorio contiene el proyecto en Unity encargado de generar y gestionar la sala de control inmersiva en Realidad Virtual (compatible con Meta Quest 2, 3 y Pro). Permite teleoperar un vehículo autónomo, visualizar cámaras en tiempo real con muy baja latencia, establecer rutas GPS interactivas y ver telemetría.

El objetivo principal de esta arquitectura es **replicar la interfaz web (HTML/JS) del vehículo dentro del entorno 3D**, asegurando compatibilidad total con el backend en ROS 2 y Flask.

---

## ⚙️ Fase 0: Instalación y Configuración del Entorno (CRÍTICO)

La configuración de entornos de Realidad Virtual para Android en Unity puede ser muy propensa a errores si no se respetan las versiones. A continuación se documenta paso a paso cómo preparar el proyecto, los motivos técnicos y los problemas a los que nos enfrentamos.

### 1. Requisitos y Versión de Unity
**Versión Obligatoria:** `Unity 2022.3.x LTS`
- **¿Por qué?** Meta XR SDK (v60+) es muy inestable en versiones más recientes (como Unity 2023 o Unity 6) debido a cambios en la arquitectura del sistema de plugins de XR y en la compilación de Gradle. Usar una versión `2022.3 LTS` garantiza el equilibrio perfecto entre estabilidad de Android Build Support y compatibilidad total con las librerías de Meta.

### 2. Módulos de Instalación (A través de Unity Hub)
Al instalar Unity, asegúrate de añadir los siguientes módulos marcando sus casillas:
- **Android Build Support**
- **OpenJDK**
- **Android SDK & NDK Tools**
*(Unity se encarga de enlazar los paths internos. No uses SDKs de Android Studio externos, ya que provocan desajustes de versión).*

### 3. Configuración Inicial del Proyecto
1. Ve a `File > Build Settings`. Selecciona **Android** y haz clic en **Switch Platform**. Esto compila los recursos en formatos compatibles con Quest (texturas ASTC).
2. Ve a `Edit > Project Settings > XR Plug-in Management`, instala el módulo y marca la casilla **Oculus** en la pestaña del androide.

### 4. SDK de Meta e Interacciones (Building Blocks)
No usamos las antiguas integraciones OVR heredadas. Usamos el paquete modular actualizado:
1. Desde la Asset Store / Package Manager, instala **Meta XR Core SDK** y **Meta XR Interaction SDK**.
2. **Uso de Building Blocks:** Meta incluye una pestaña llamada *Building Blocks* que agiliza la configuración. Usamos esto para añadir al jugador en escena:
   - **Camera Rig:** El bloque principal que representa la cabeza y posición del usuario.
   - **Mandos / Controller Tracking:** Añade la geometría física de los mandos de Quest.
   - **Interaction to Canvas:** Bloque vital. Por defecto, Unity usa un `GraphicRaycaster` que no entiende de punteros láser 3D. Tuvimos que inyectar un **`OVRRaycaster`** a nuestro lienzo (Canvas) principal y añadir el módulo de eventos de Oculus al `EventSystem`. Sin esto, los botones generados no reaccionaban a los clics de los mandos en VR.

### ⚠️ Problemas de Instalación a los que nos Enfrentamos
Durante el desarrollo inicial, chocamos con ciertos errores de entorno. Si tienes que reinstalar, ten cuidado con:
- **Pantalla negra al cargar el mapa embebido:** El visor de mapas requiere Chromium. Unity desactiva la aceleración por hardware en Android por defecto, lo que crashea los navegadores internos. **Solución:** Tuvimos que editar a mano el archivo `Assets/Plugins/Android/AndroidManifest.xml` añadiendo `android:hardwareAccelerated="true"` y `android:usesCleartextTraffic="true"`.
- **Fallo al reconocer interacciones de UI (Laser Pointers):** El láser atravesaba los botones. Tuvimos que asegurarnos de que el Canvas tuviese un componente `CanvasGroup` o `BoxCollider` según la versión del bloque, y que la cámara de eventos del Canvas estuviese asignada al `CenterEyeAnchor` de las Quest.
- **Errores de Keystore al compilar:** Al pasar el APK a las gafas, daba error de firma. Hubo que crear un *Development Keystore* en `Project Settings > Player > Publishing Settings`.

---

## 🏗 Arquitectura y Funcionamiento del Código

El código está estructurado para ser puramente programático, minimizando el uso de *Prefabs* visuales en el editor para evitar conflictos graves al fusionar ramas en Git (Git Merge Conflicts en archivos `.scene` y `.prefab` binarios son casi imposibles de resolver a mano).

### 1. Generador Procedural de la UI (`SceneBuilder.cs`)
En lugar de arrastrar botones manualmente, hemos creado un script de Editor (disponible en la barra superior: `INSIA > Crear Sala de Control`). Este script lee directrices en C# y **construye toda la sala 3D, el Canvas, los paneles curvos de vídeo y la jerarquía de botones de forma programática**. Si se rompe la escena, un clic la regenera perfectamente calibrada y alineada a los ojos del usuario.

### 2. Autodescubrimiento del Servidor (`ServerDiscovery.cs`)
Las Meta Quest se conectan por WiFi al router del coche, pero la IP de la Jetson/Raspberry Pi del coche podría cambiar por DHCP.
- Funciona abriendo un socket UDP en el puerto `5051`.
- Escucha activamente el mensaje de *broadcast* que lanza el backend.
- Cuando intercepta la IP del servidor Flask, auto-reconfigura todos los módulos de Unity para apuntar a ella.

### 3. Streaming de Vídeo Nivel C++ (`MjpegStreamReceiver.cs`)
En un entorno web tradicional, las cámaras se ven simplemente con un tag `<img src="...">`. En Unity VR, había que pintar esa textura en paneles 3D curvados.
- Se descartó WebRTC por requerir servidores STUN/TURN masivos y mucha sobrecarga en Android.
- Nos conectamos directamente al Stream HTTP en bruto.
- Para evitar "freír" la CPU de las Quest iterando y comparando `bytes` para encontrar el fotograma JPEG (lo que tiraba los FPS a 5), extraemos los frames interceptando las cabeceras *Boundary* usando `System.Array.IndexOf` nativo a altísima velocidad. El resultado es vídeo en tiempo real sin lag.

### 4. Puente HTTP y Telemetría (`FlaskApiClient.cs` y `ControlPanelUI.cs`)
Toda la lógica de control está centralizada.
- `FlaskApiClient`: Implementa los `UnityWebRequest` asíncronos y convierte los datos a formato JSON.
- `ControlPanelUI`: Es el cerebro de la botonera. Captura clics físicos de la interfaz y los mapea a sus correspondientes llamadas REST (`/override`, `/emergencystop`, etc). También gestiona peculiaridades de la VR, como invocar dinámicamente al teclado del sistema operativo (Script `VRKeyboardFocus.cs`) cuando el usuario apunta a la caja de nombre de la ruta.

### 5. Mapa GPS Interactivo 3D (`TLabWebView`)
Dado que el mapa GPS del vehículo usa la librería `Leaflet` con vectores `.pmtiles` y tiles cacheadas (que Unity no puede renderizar de forma nativa fácilmente), se integró el plugin *TLabWebView*.
- Inyecta una pestaña real de Chrome invisible en la capa nativa de Android de las gafas.
- Renderiza esa pestaña sobre una textura dentro del juego.
- Redirige el puntero láser para emular un "Dedo Táctil" (Touch event) de Android, permitiendo hacer zoom y arrastrar el mapa tal cual lo harías en un móvil o en la web original.
