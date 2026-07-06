# Meta Quest Vehicle Control (VR/MR) 🥽🚙

**Cliente de Realidad Virtual y Realidad Mixta para la teleoperación inmersiva de vehículos autónomos (UGV).**

Aplicación desarrollada en **Unity 6 LTS** para visores **Meta Quest 3**, que se comunica bidireccionalmente con un backend en Python (Flask) conectado a **ROS 2**. Permite al operador controlar el vehículo, ver sus cámaras y monitorizar su estado desde un entorno VR inmersivo.

> Desarrollado para el **Instituto Universitario de Investigación del Automóvil (INSIA)** — Universidad Politécnica de Madrid.

---

## 📑 Tabla de Contenidos

1. [Características Principales](#-características-principales)
2. [Arquitectura del Sistema](#-arquitectura-del-sistema)
3. [Requisitos Previos](#-requisitos-previos)
4. [Instalación Paso a Paso (Desde Cero)](#-instalación-paso-a-paso-desde-cero)
5. [Estructura del Proyecto y Descripción de Archivos](#-estructura-del-proyecto-y-descripción-de-archivos)
6. [Controles del Usuario](#-controles-del-usuario)
7. [Endpoints de la API Flask](#-endpoints-de-la-api-flask)
8. [Problemas Conocidos y Soluciones (Troubleshooting)](#-problemas-conocidos-y-soluciones-troubleshooting)
9. [Testing y Verificación](#-testing-y-verificación)
10. [Configuración del Backend (Lado del Vehículo)](#-configuración-del-backend-lado-del-vehículo)
11. [Notas para Desarrolladores](#-notas-para-desarrolladores)
12. [Licencia y Créditos](#-licencia-y-créditos)

---

## 🌟 Características Principales

| Característica                     | Descripción                                                                                                                                                         |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Teleoperación Inmersiva**        | Control total del vehículo (aceleración, frenado y dirección) utilizando los joysticks de los mandos de Meta Quest.                                                 |
| **Streaming Multi-Cámara (MJPEG)** | Visualización simultánea de hasta 5 flujos de vídeo de baja latencia directamente desde el vehículo, con carga escalonada y reconexión automática.                  |
| **Mapa GPS Interactivo**           | Navegador web embebido (TLab WebView) que renderiza la interfaz GPS HTML directamente dentro del mundo VR.                                                          |
| **Realidad Mixta (Passthrough)**   | Intercala entre entorno 100% virtual y el modo "Passthrough" para que el piloto no pierda noción de su entorno físico real.                                         |
| **Auto-Descubrimiento UDP**        | Las gafas detectan automáticamente el servidor Flask del vehículo en la red local mediante broadcast UDP en puerto 5555.                                            |
| **Interfaz Generada por Código**   | Toda la escena 3D (paneles, botones, HUD, cámaras) se genera automáticamente desde código C# (`SceneBuilder.cs`), replicando el diseño CSS de la interfaz web HTML. |
| **Arquitectura Desacoplada**       | Comunicación vía HTTP REST (JSON) y flujos MJPEG. La IP del vehículo se configura externamente, facilitando el cambio sin recompilar.                               |

---

## 🏗️ Arquitectura del Sistema

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           META QUEST 3 (Unity 6 / C#)                       │
│                                                                             │
│  ┌──────────────┐   ┌───────────────┐   ┌──────────────────┐               │
│  │ Quest        │   │ FlaskApiClient│   │ MjpegStream      │               │
│  │ Controller   │──▶│ (Singleton    │   │ Receiver (x5)    │               │
│  │ Handler      │   │  HTTP Client) │   │ (Hilos separados)│               │
│  └──────────────┘   └───────┬───────┘   └────────┬─────────┘               │
│                             │                     │                         │
│  ┌──────────────┐   ┌───────┴───────┐             │                         │
│  │ Server       │   │ ControlPanel  │             │                         │
│  │ Discovery    │   │ UI            │             │                         │
│  │ (UDP:5555)   │   │ (728 líneas)  │             │                         │
│  └──────┬───────┘   └───────────────┘             │                         │
│         │                                         │                         │
└─────────┼─────────────────────────────────────────┼─────────────────────────┘
          │            WiFi / Ethernet               │
          │                                         │
┌─────────┼─────────────────────────────────────────┼─────────────────────────┐
│         ▼                                         ▼                         │
│  ┌──────────────┐   ┌───────────────┐   ┌──────────────────┐               │
│  │ UDP Broadcast│   │ Flask Server  │   │ /video_feed/{id} │               │
│  │ Puerto 5555  │   │ (web_node.py) │   │ MJPEG Streams    │               │
│  └──────────────┘   └───────┬───────┘   └──────────────────┘               │
│                             │                                               │
│                     ┌───────┴───────┐                                       │
│                     │    ROS 2      │                                       │
│                     │  (Topics,     │                                       │
│                     │   Services)   │                                       │
│                     └───────┬───────┘                                       │
│                             │                                               │
│                     ┌───────┴───────┐                                       │
│                     │   VEHÍCULO    │                                       │
│                     │  (Motores,    │                                       │
│                     │   Cámaras,    │                                       │
│                     │   Sensores)   │                                       │
│                     └───────────────┘                                       │
│                                                                             │
│              JETSON / PC CON ROS 2 (Python + Flask)                         │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Flujos de datos:

| Flujo          | Protocolo        | Dirección     | Descripción                                         |
| -------------- | ---------------- | ------------- | --------------------------------------------------- |
| **Telemetría** | HTTP GET (JSON)  | Quest ← Flask | Velocidad, modo, inclinación, waypoints, emergencia |
| **Comandos**   | HTTP POST (JSON) | Quest → Flask | Joystick VR, cambio de modo, emergencia, rutas      |
| **Vídeo**      | MJPEG sobre HTTP | Quest ← Flask | Hasta 5 streams simultáneos (`/video_feed/{1..5}`)  |
| **Discovery**  | UDP Broadcast    | Quest ← Flask | Auto-detección de IP del servidor (puerto 5555)     |
| **Mapa GPS**   | WebView HTTP     | Quest ← Flask | Página HTML `/gps_only` renderizada en TLab WebView |

---

## 📋 Requisitos Previos

### Hardware

| Componente               | Detalle                                                                                |
| ------------------------ | -------------------------------------------------------------------------------------- |
| **Meta Quest 3**         | Visor VR principal. Meta Quest 2 también funciona con limitaciones de rendimiento.     |
| **Cable USB-C**          | Para conectar las gafas al PC de desarrollo (depuración ADB + Build & Run).            |
| **PC de desarrollo**     | Ubuntu/Linux recomendado. Windows también funciona pero no se ha probado extensamente. |
| **Vehículo + Jetson/PC** | Ordenador embarcado con ROS 2 ejecutando el servidor Flask y las cámaras.              |
| **Red WiFi compartida**  | Las gafas y el vehículo deben estar en la **misma red** WiFi/Ethernet.                 |

### Software

| Software                  | Versión            | Notas                                                                                                    |
| ------------------------- | ------------------ | -------------------------------------------------------------------------------------------------------- |
| **Unity Hub**             | Última versión     | Gestor de versiones de Unity                                                                             |
| **Unity 6 LTS**           | **`6000.0.77f1`**  | ⚠️ **Versión exacta obligatoria.** Ver [sección de problemas](#por-qué-no-usar-unity-6000500-o-superior) |
| **Android Build Support** | Incluido en Unity  | Debe marcarse durante la instalación de Unity                                                            |
| **ADB**                   | Incluido con Unity | Android Debug Bridge para comunicación con las gafas                                                     |
| **Python 3 + Flask**      | 3.8+               | Lado del vehículo (servidor backend)                                                                     |
| **ROS 2**                 | Humble/Jazzy       | Lado del vehículo (middleware robótico)                                                                  |

---

## 🚀 Instalación Paso a Paso (Desde Cero)

> ⚠️ **IMPORTANTE:** Sigue estos pasos en orden. Cada paso depende del anterior. Si te saltas un paso, es probable que tengas errores difíciles de diagnosticar.

---

### Paso 1: Instalar Unity 6 LTS (6000.0.77f1)

#### ¿Por qué esta versión exacta?

Durante el desarrollo se descubrió que la versión **6000.5.x** (Preview/Beta) de Unity contiene cambios radicales en sus APIs internas que **rompen por completo el SDK oficial de Meta Quest**. Funciones como `GetInstanceID` fueron eliminadas o reemplazadas, causando más de 50 errores `CS0619` imposibles de parchear. La versión **LTS (Long Term Support) 6000.0.77f1** es la última versión estable compatible.

#### Instalación

1. **Instala Unity Hub** desde [unity.com/download](https://unity.com/download) si no lo tienes.

2. **Descarga Unity 6000.0.77f1** desde el archivo oficial de Unity:
   - Ve a [unity.com/releases/editor/archive](https://unity.com/releases/editor/archive)
   - Busca la versión **`6000.0.77f1`** (es la rama estable 6000.0)
   - Haz clic en **"INSTALL →"** para que se abra en Unity Hub

3. **Marca los módulos necesarios** durante la instalación:
   - ✅ **Android Build Support**
   - ✅ **OpenJDK** (viene incluido dentro de Android Build Support)
   - ✅ **Android SDK & NDK Tools** (viene incluido dentro de Android Build Support)

4. Espera a que termine la instalación (~5-10 GB de descarga).

---

### Paso 2: Clonar el Repositorio y Abrir en Unity

```bash
git clone <URL_DEL_REPOSITORIO>
```

1. Abre **Unity Hub** → Pestaña **Projects**.
2. Haz clic en **Add** (o **Open**) → Navega hasta la carpeta `MetaQuestVehcileControl/`.
3. Asegúrate de que el editor seleccionado es **6000.0.77f1**.
4. Haz clic en el proyecto para abrirlo.
5. **Espera pacientemente** — la primera vez que Unity importa el proyecto puede tardar **5-15 minutos** dependiendo de tu hardware. No lo cierres aunque la barra de progreso parezca atascada.

> **Nota:** Todos los paquetes (Meta SDK, TLab WebView, XR Interaction Toolkit, etc.) se instalan automáticamente porque están declarados en `Packages/manifest.json`.

---

### Paso 3: Arreglar Error CS1032 (Solo Linux)

Al abrir el proyecto por primera vez **en Linux**, aparecerán errores rojos en la consola de Unity:

```
error CS1032: Cannot define/undefine preprocessor symbols after first token in file
```

**¿Por qué ocurre?** Es un bug conocido del SDK de Meta. El archivo `RuntimeOptimizerPlugin.cs` tiene directivas `#define` / `#undef` en una posición que el compilador de Mono en Linux no tolera (en Windows no da problemas).

**Solución:**

1. Navega en Unity al archivo problemático. La ruta es:

   ```
   Packages/com.meta.xr.sdk.core/Scripts/RuntimeOptimizer/Core/RuntimeOptimizerPlugin.cs
   ```

2. Abre el archivo y busca las líneas `#define` o `#undef` que estén **después** de la primera línea de código.

3. **Comenta esas líneas** con `//` al principio:

   ```csharp
   // #define SOME_SYMBOL    ← comentar
   // #undef SOME_SYMBOL     ← comentar
   ```

4. Guarda el archivo y vuelve a Unity. Los errores desaparecerán.

> **Nota:** Este parche no afecta a la funcionalidad. Solo silencia un conflicto de sintaxis específico de la compilación en Linux.

---

### Paso 4: Cambiar Plataforma a Android

1. Ve a `File > Build Settings`.
2. Selecciona **Android** en la lista de plataformas.
3. Haz clic en **Switch Platform** (abajo a la derecha).
4. **Espera la reimportación** — puede tardar **5-10 minutos** (Unity reimporta todos los assets para la nueva plataforma).

---

### Paso 5: Configurar XR Plug-in Management (OpenXR)

1. Ve a `Edit > Project Settings > XR Plug-in Management`.

2. Haz clic en la pestaña **Android** (icono del robot verde).

3. Marca la casilla **OpenXR**.

4. Expande la sección **OpenXR** que aparece debajo:
   - Añade el **Oculus Touch Controller Profile** (perfil de los mandos de Meta Quest)
   - Añade el **Meta Quest Feature** (se llama "Meta Quest Support" o similar)

5. Ve a **Project Validation** (justo debajo de OpenXR en el menú lateral).

6. Haz clic en **Fix All** — esto corrige automáticamente configuraciones como:
   - Graphics API (Vulkan)
   - Minimum API Level
   - Scripting Backend (IL2CPP)
   - Target Architecture (ARM64)

> **Nota:** Si algún warning no se arregla con "Fix All", generalmente se puede ignorar (son recomendaciones, no errores bloqueantes).

---

### Paso 6: Configurar Input System

El proyecto usa **ambos** sistemas de input de Unity (el antiguo `Input.GetKeyDown` para atajos de teclado en PC, y el nuevo `InputSystem` para XR).

1. Ve a `Edit > Project Settings > Player`.
2. En la sección **Other Settings**, busca **Active Input Handling**.
3. Selecciona **Both** (no "Input System Package (New)" ni "Input Manager (Old)").
4. Unity pedirá reiniciar. **Acepta y reinicia.**

> **Si no haces este paso**, aparecerá el error:
>
> ```
> InvalidOperationException: You are trying to read Input using the UnityEngine.Input class,
> but you have switched active Input handling to Input System package in Player Settings.
> ```

---

### Paso 7: Verificar Paquetes Instalados

Todos los paquetes ya vienen declarados en `Packages/manifest.json` y se instalan automáticamente. Puedes verificarlos en `Window > Package Manager`:

| Paquete                       | Versión  | Fuente                                                   | Para qué sirve                                                  |
| ----------------------------- | -------- | -------------------------------------------------------- | --------------------------------------------------------------- |
| **Meta XR All-in-One SDK**    | 203.0.0  | Unity Registry                                           | SDK principal de Meta Quest (OVRManager, OVRInput, Passthrough) |
| **XR Interaction Toolkit**    | 3.0.11   | Unity Registry                                           | Sistema de interacción XR (rays, grabs, XR UI)                  |
| **XR Hands**                  | 1.7.3    | Unity Registry                                           | Tracking de manos (opcional, para futuro uso)                   |
| **OpenXR**                    | 1.16.1   | Unity Registry                                           | Backend de XR multiplataforma                                   |
| **Input System**              | 1.19.0   | Unity Registry                                           | Nuevo sistema de input de Unity                                 |
| **TLab WebView**              | git      | [GitHub](https://github.com/TLabAltoh/TLabWebView.git)   | Navegador web embebido Android-only (para mapa GPS)             |
| **TLab VKeyboard**            | git      | [GitHub](https://github.com/TLabAltoh/TLabVKeyborad.git) | Teclado virtual en VR (dependencia de TLab WebView)             |
| **Universal Render Pipeline** | 17.0.4   | Unity Registry                                           | Pipeline de render moderno                                      |
| **TextMesh Pro**              | built-in | Unity                                                    | Texto de alta calidad en la UI                                  |

### ¿Cómo se añadieron los paquetes Git?

Si necesitas reinstalar manualmente TLab WebView o VKeyboard:

1. `Window > Package Manager`
2. Haz clic en el botón **+** (arriba a la izquierda)
3. Selecciona **Add package from git URL...**
4. Pega la URL:
   - WebView: `https://github.com/TLabAltoh/TLabWebView.git`
   - VKeyboard: `https://github.com/TLabAltoh/TLabVKeyborad.git`

---

### Paso 8: Copiar Prefab de TLab WebView

El `SceneBuilder.cs` usa `Resources.Load<GameObject>("TLab/WebView/Browser")` para instanciar el navegador web. Sin embargo, Unity solo busca en `Assets/Resources/`, no en el `PackageCache`. Hay que copiar el prefab manualmente:

```bash
# Desde la raíz del proyecto Unity:
mkdir -p Assets/Resources/TLab/WebView/

# Copiar el prefab (la ruta exacta puede variar según el hash del commit de git):
cp "Library/PackageCache/com.tlabaltoh.webview@*/Resources/TLab/WebView/Browser.prefab" \
   "Assets/Resources/TLab/WebView/Browser.prefab"
```

> **¿Cómo encontrar la ruta exacta?** Si el wildcard `*` no funciona, busca manualmente:
>
> ```bash
> find Library/PackageCache/ -name "Browser.prefab" -path "*/TLab/*"
> ```

Después de copiar, vuelve a Unity y espera a que detecte el nuevo archivo.

---

### Paso 9: Generar la Escena (SceneBuilder)

La interfaz 3D completa se genera desde código, no manualmente. Esto asegura que la escena es siempre reproducible y consistente.

1. En la barra de menú superior de Unity, busca **INSIA**.
2. Haz clic en `INSIA > Crear Sala de Control (Rediseño Web)`.
3. Aparecerá un diálogo de confirmación. Haz clic en **Crear**.
4. Espera unos segundos. En la consola aparecerá:
   ```
   [OK] Sala de Control (Calco HTML + Fase 4) creada en: Assets/Scenes/MainControlRoom.unity
   ```

El `SceneBuilder.cs` habrá creado automáticamente:

- El **XR Camera Rig** (OVRCameraRig con OVRManager y Passthrough)
- El **panel principal** (vídeo de cámara 1 + panel de control lateral con todos los botones)
- **4 paneles de cámaras secundarias** (cámaras 2, 3, 4, 5)
- El **panel del mapa GPS** (con TLab WebView)
- El objeto **[Managers]** con `FlaskApiClient`, `QuestControllerHandler` y `ServerDiscovery`
- Los **`MjpegStreamReceiver`** adjuntos a cada panel de cámara
- El **EventSystem** con `InputSystemUIInputModule`
- El sistema **LazyFollowUI** para que los paneles sigan al usuario

---

### Paso 10: Configurar Interacción XR (Building Blocks)

> ⚠️ **Este paso es MANUAL y OBLIGATORIO.** Sin él, los mandos no podrán hacer clic en los botones de la interfaz.

Después de que `SceneBuilder` genere la escena, hay que añadir los componentes de interacción XR:

#### 10.1 — Añadir Ray Interactions (Building Blocks)

1. En la barra de menú: `Meta XR Tools > Building Blocks`.
2. Se abrirá una ventana con bloques disponibles.
3. Busca **"Ray Interactions"** (o "Controller Tracking" dependiendo de la versión del SDK).
4. Haz clic en él para añadirlo a la escena.

> Si no encuentras "Building Blocks" en el menú, también puedes buscarlo en `Window > Meta XR > Building Blocks`.

#### 10.2 — Hacer los Canvas Interactables

Para que el rayo del mando pueda pulsar botones en los canvas 3D:

1. En la **jerarquía de la escena**, selecciona el objeto `Main_Web_Interface`.
2. Haz clic derecho sobre él → busca la opción `XR > Make Canvas Interactable` (o utiliza Building Blocks para añadir un "XR Interactable Canvas").
3. Repite el proceso para el canvas `[GPS Map Panel]`.

Esto añade automáticamente los componentes necesarios (`TrackedDeviceGraphicRaycaster`, etc.) para que la UI responda al rayo XR.

#### 10.3 — Verificar

Tras añadir todo, la consola NO debe mostrar errores rojos. Si ves warnings amarillos, generalmente son informativos y no bloqueantes.

---

### Paso 11: Configurar ADB (Conexión USB con las Gafas)

#### En Linux — Configurar regla udev (una sola vez)

Por defecto, Linux no permite a usuarios normales acceder a dispositivos USB de desarrollo. Sin esta configuración, tendrás que usar `sudo` cada vez que conectes las gafas:

```bash
echo 'SUBSYSTEM=="usb", ATTR{idVendor}=="2833", MODE="0666", GROUP="plugdev"' | \
  sudo tee /etc/udev/rules.d/51-oculus.rules > /dev/null && \
  sudo udevadm control --reload-rules && \
  sudo udevadm trigger
```

> **`2833`** es el Vendor ID de Meta/Oculus en USB.

#### Conectar las gafas

1. Conecta las Meta Quest 3 por **cable USB-C** al PC.
2. **Ponte las gafas**: aparecerá un cuadro de diálogo dentro de las gafas preguntando si permites la depuración USB. **Acepta** (marca "Siempre permitir desde este equipo").
3. Verifica la conexión en terminal:
   ```bash
   adb devices
   ```
   Debe mostrar algo como:
   ```
   List of devices attached
   1WMHH815T10XXX  device
   ```

> **Si no aparece:** desconecta y reconecta el cable. Si sigue sin funcionar, ejecuta:
>
> ```bash
> adb kill-server && adb devices
> ```

---

### Paso 12: Conexión con el Servidor Flask del Vehículo

Las gafas detectan automáticamente el servidor Flask del vehículo gracias al **auto-descubrimiento UDP** implementado en `ServerDiscovery.cs`. **No necesitas configurar ninguna IP manualmente.**

#### ¿Cómo funciona?

1. El servidor Flask del vehículo (`web_node.py`, línea 1674) arranca un hilo que emite un **broadcast UDP** cada **2 segundos** en el puerto **5555** con un paquete JSON:
   ```json
   {"service": "vehicle_control", "vehicle": "<namespace_ROS2>", "ip": "<ip_real_del_servidor>", "port": <puerto_flask>}
   ```
   Donde:
   - `vehicle` = el namespace de ROS 2 del nodo activo (ej: `MUTT8x8`)
   - `ip` = la IP real de red del servidor (auto-detectada)
   - `port` = el puerto del servidor Flask (normalmente `5050`)
2. Las gafas escuchan en ese puerto (via `ServerDiscovery.cs`) y, al recibir un paquete con `"service": "vehicle_control"`, auto-configuran la IP y el puerto de `FlaskApiClient`.
3. La IP detectada se guarda en `PlayerPrefs` para que, en el próximo arranque, las gafas conecten directamente sin esperar al broadcast.

> **Requisito:** Las gafas y el vehículo deben estar en la **misma red WiFi**. Si están en redes distintas, el broadcast UDP no llegará.

#### Fallback: Cambiar la IP manualmente (en Unity Inspector)

Si por alguna razón el auto-descubrimiento no funciona (redes separadas, firewall bloqueando UDP, etc.), puedes configurar la IP a mano:

1. En la jerarquía de la escena, selecciona el objeto `[Managers]`.
2. En el Inspector, busca el componente `Flask Api Client`.
3. Cambia el campo **Server Ip** (por defecto `10.170.183.110`) y **Server Port** (por defecto `5050`).
4. Recompila el APK con `Build And Run`.

> **Nota:** Esta IP se hardcodea en la escena, así que si cambias de vehículo/red tendrás que modificarla y recompilar. Por eso el auto-descubrimiento UDP es mucho más práctico.

---

### Paso 13: Compilar y Ejecutar en las Gafas (Build & Run)

1. Ve a `File > Build Settings`.
2. Asegúrate de que:
   - La plataforma es **Android**.
   - La escena `Assets/Scenes/MainControlRoom.unity` está en la lista **Scenes In Build**. Si no está, haz clic en **Add Open Scenes**.
3. Conecta las gafas Meta Quest 3 por USB al PC.
4. Haz clic en **Build And Run**.
5. Unity te pedirá elegir dónde guardar el archivo `.apk`. Elige cualquier carpeta.
6. **Espera** — la compilación tarda entre 1 y 5 minutos la primera vez.
7. Cuando termine, la aplicación se instalará y ejecutará automáticamente en las gafas.

> **Nota:** El archivo `.apk` generado pesa ~90 MB. El `.gitignore` del proyecto ya excluye los `.apk` del control de versiones.

---

### Paso 14: Lanzar el Backend (Lado del Vehículo)

Para que las gafas tengan información que mostrar, el servidor Flask debe estar corriendo:

```bash
# En tu PC/Jetson con ROS 2 (asegúrate de hacer source al workspace):
source ~/ros2_ws/install/setup.bash
ros2 launch web_interface_pkg_8x8 <tu_launch_file>.launch.py
```

Si quieres simular cámaras para probar sin hardware real:

```bash
python3 src/mock_cameras.py
```

---

## 📂 Estructura del Proyecto y Descripción de Archivos

```
MetaQuestVehcileControl/
├── Assets/
│   ├── Editor/
│   │   └── SceneBuilder.cs           ← Generador automático de la escena 3D
│   ├── Scripts/
│   │   ├── Networking/
│   │   │   ├── FlaskApiClient.cs     ← Singleton HTTP (comunicación con Flask)
│   │   │   ├── MjpegStreamReceiver.cs← Decodificador de vídeo MJPEG
│   │   │   └── ServerDiscovery.cs    ← Auto-descubrimiento UDP
│   │   ├── Input/
│   │   │   └── QuestControllerHandler.cs ← Mapeo de mandos Quest
│   │   ├── UI/
│   │   │   ├── ControlPanelUI.cs     ← Lógica de la interfaz (botones, HUD)
│   │   │   ├── CameraPanelController.cs ← Contenedor de panel de cámara
│   │   │   ├── LoadingAnimator.cs    ← Animaciones de carga (rotación, pulso)
│   │   │   ├── NoDragScrollRect.cs   ← ScrollRect sin drag/scroll por joystick
│   │   │   └── VRKeyboardFocus.cs    ← Teclado nativo Android en VR
│   │   └── XR/
│   │       └── LazyFollowUI.cs       ← HUD dinámico "Lazy Follow"
│   ├── Oculus/                       ← Configuración de OVR (auto-generado)
│   ├── Plugins/                      ← Plugins nativos Android
│   ├── Resources/
│   │   └── TLab/WebView/Browser.prefab ← Prefab del navegador web (copiado)
│   ├── Samples/                      ← Samples de XR Interaction Toolkit
│   ├── Scenes/
│   │   └── MainControlRoom.unity     ← Escena principal (generada por SceneBuilder)
│   ├── Settings/                     ← Configuración de Input Actions
│   ├── StreamingAssets/              ← Vacío (la IP se auto-detecta por UDP)
│   ├── TextMesh Pro/                 ← Assets de TextMesh Pro
│   ├── XR/                           ← Configuración de XR
│   └── XRI/                          ← Configuración de XR Interaction Toolkit
├── Packages/
│   ├── manifest.json                 ← Lista de TODOS los paquetes (Meta SDK, TLab, etc.)
│   └── com.meta.xr.sdk.core/        ← SDK de Meta (local, para poder parchear CS1032)
├── ProjectSettings/
│   └── ProjectVersion.txt            ← Versión de Unity: 6000.0.77f1
└── .gitignore                        ← Excluye Library/, Builds/, APKs, logs
```

### Descripción Detallada de Cada Script

---

#### `Assets/Editor/SceneBuilder.cs` — Generador de Escena (805 líneas)

**¿Qué hace?** Genera automáticamente **toda** la jerarquía 3D de la sala de control desde código C#. Se ejecuta desde la barra de menú de Unity: `INSIA > Crear Sala de Control (Rediseño Web)`.

**¿Por qué existe?** Permite recrear la escena desde cero en cualquier momento, garantizando consistencia. Si se modifica el layout o se añaden elementos, basta con volver a ejecutar el script.

**Qué crea:**

- **[XR Camera Rig]**: Busca el prefab `OVRCameraRig` y lo instancia. Si no lo encuentra, crea una cámara básica a 1.6m de altura. Configura `OVRManager` con Passthrough habilitado y `OVRPassthroughLayer` oculto por defecto (arranca en modo VR).
- **Main_Web_Interface**: Canvas 3D principal (resolución **1280×720**, tamaño físico 1.8m × 1.01m) con:
  - Área de vídeo (78% izquierdo) con overlay de carga animado
  - Panel lateral derecho (22%) con ScrollView, botones de operación, input fields, toggles
  - HUD de telemetría (velocidad, parada de emergencia)
  - Botones de STOP y candado superpuestos en el vídeo
- **CameraPanel_2/3/4/5**: 4 paneles secundarios (resolución **1280×720**, tamaño 0.8m × 0.45m), cada uno con su header de color diferente y overlay de carga
- **[GPS Map Panel]**: Panel con TLab WebView (resolución **1920×1080**, tamaño 1.2m × 0.675m) para el mapa GPS interactivo
- **[Managers]**: Objeto con `FlaskApiClient`, `QuestControllerHandler` y `ServerDiscovery`
- **[UI Panels]**: Contenedor padre con `LazyFollowUI` para seguimiento de cabeza
- **EventSystem**: Con `InputSystemUIInputModule` para compatibilidad con el nuevo Input System

**Colores y diseño:** Los colores están tomados directamente del CSS de `index_new.html` de la interfaz web HTML del vehículo (por ejemplo, `#0f1117` para el fondo, `#248446` para botones verdes, `#d34233` para botones rojos). Esto asegura que la interfaz VR tenga el mismo aspecto visual que la web.

**Layout:** Todos los paneles se posicionan en un plano a `DIST = 2.5 metros` del usuario (definido en `FlatPos(x, y)` → `Vector3(x, EYE + y, 2.5)`). La distribución exacta es:

- **Panel principal** → centro `FlatPos(0, 0)`
- **CAM 2** → izquierda, ligeramente arriba `FlatPos(-1.4, 0.1)`
- **CAM 4** → derecha, ligeramente arriba `FlatPos(1.4, 0.1)`
- **CAM 3** → abajo-izquierda `FlatPos(-0.5, -0.8)`
- **CAM 5** → abajo-derecha `FlatPos(0.5, -0.8)`
- **GPS** → centrado arriba `FlatPos(0, 0.88)`

---

#### `Assets/Scripts/Networking/FlaskApiClient.cs` — Cliente HTTP (487 líneas)

**Singleton central** que gestiona TODA la comunicación HTTP con el servidor Flask de ROS 2.

**Polling (GET):** Cada 0.5 segundos consulta al servidor:

- `/get_telemetry` — Velocidad del vehículo
- `/get_mode_mission_status` — Modo de operación (0=StandBy, 1=Auto, 2=Tele, 4=Nav2, 6=VR)
- `/get_land_meter_status` — Roll/Pitch del inclinómetro
- `/get_waypoint_info` — Información de waypoints
- `/get_route_progress` — Porcentaje de ruta completado
- `/get_ui_state` — Estado sincronizado de la interfaz (botones, toggles, emergencia)
- `/get_num_cameras` — Número de cámaras disponibles (solo al inicio)
- `/get_available_maps` — Lista de mapas/rutas grabadas (solo al inicio)

**Comandos (POST):** Envía acciones al vehículo cuando el usuario interactúa:

- Joystick VR (velocidad/dirección)
- Cambio de modo de misión
- Parada de emergencia
- Grabar/reproducir rutas
- Toggles (Override Speed, Follow Me, etc.)

**Gestión de conexión:** Detecta pérdida de conexión tras 6 fallos consecutivos y emite eventos `OnConnectionLost` / `OnConnectionRestored` que la UI usa para mostrar feedback visual.

---

#### `Assets/Scripts/Networking/MjpegStreamReceiver.cs` — Vídeo MJPEG (352 líneas)

**Decodificador de streams MJPEG** que corre en un **hilo secundario** para no bloquear el hilo principal de Unity.

**Funcionamiento:**

1. Espera a que `FlaskApiClient` esté conectado (no intenta streams a una IP incorrecta)
2. Abre una conexión HTTP persistente a `/video_feed/{cam_id}`
3. Lee bytes continuamente buscando marcadores JPEG (`FF D8` = inicio, `FF D9` = final) separados por boundaries `--frame`
4. Cuando tiene un frame completo, lo pasa al hilo principal mediante un flag `_hasNewFrame`
5. El hilo principal decodifica el JPEG con `Texture2D.LoadImage()` y lo aplica al `RawImage`

**Optimizaciones críticas:**

- **`ServicePointManager.DefaultConnectionLimit = 10`**: Sin esto, .NET/Mono limita a 2 conexiones simultáneas a la misma IP. Las cámaras 3, 4 y 5 se quedarían atascadas para siempre.
- **Carga escalonada**: Cada cámara espera `(id - 1) * 0.6 segundos` antes de conectarse, para no saturar la CPU al cargar las 5 a la vez.
- **Limitación a 15 FPS**: `MIN_DECODE_INTERVAL = 1/15` para no sobrecargar el procesador del Quest.
- **Reconexión automática**: Si el stream falla, espera 3 segundos y reintenta.

---

#### `Assets/Scripts/Networking/ServerDiscovery.cs` — Auto-Descubrimiento UDP (134 líneas)

**Escucha broadcast UDP** en el puerto 5555 para detectar automáticamente el servidor Flask en la red.

**Funcionamiento:**

1. Al iniciar, abre un socket UDP en el puerto 5555 en un hilo secundario
2. Cuando recibe un paquete JSON como `{"service":"vehicle_control","ip":"192.168.1.50","port":5050,"vehicle":"MUTT_8x8"}`, extrae la IP y el puerto
3. En el hilo principal (vía `Update()`), actualiza `FlaskApiClient.Instance.serverIp` y guarda en `PlayerPrefs` para el próximo arranque
4. Si la UI ya está cargada, recarga el mapa GPS con la nueva IP

---

#### `Assets/Scripts/Input/QuestControllerHandler.cs` — Controles del Mando (191 líneas)

**Mapea los controles** físicos de los mandos Meta Quest a acciones del vehículo.

**Separación importante:** Los botones están divididos en dos grupos:

- **`HandleLocalButtons()`** — Se ejecutan SIEMPRE (no necesitan conexión):
  - Botón A → Toggle Passthrough (VR ↔ MR)
- **`HandleNetworkButtons()`** — Solo se ejecutan si hay conexión al servidor:
  - Botón B → Toggle VR Mode ON/OFF
  - Grip derecho → Toggle Parada de Emergencia

**Joystick:** Lee `OVRInput.RawAxis2D.RThumbstick` en las gafas, o `Input.GetAxis("Horizontal/Vertical")` en PC. Aplica deadzone de 0.15 y envía a 20 Hz. Solo envía si VR Mode está activo y no hay emergencia.

**Passthrough:** Busca `OVRPassthroughLayer` en la escena y alterna su visibilidad. Cambia `Camera.main.backgroundColor` entre transparente (Passthrough ON) y oscuro (Passthrough OFF).

---

#### `Assets/Scripts/UI/ControlPanelUI.cs` — Lógica de UI (728 líneas)

**Script central** que conecta los botones generados por `SceneBuilder` con `FlaskApiClient`. Es una **réplica exacta del JavaScript de `index_new.html`** de la interfaz web.

**Inicialización:** Busca todos los `Button` en la jerarquía por nombre (`btn_standby`, `btn_auto`, `btn_tele`, `btn_nav2`, `btn_follow_person`, `btn_follow_vehicle`, `btn_record`, `btn_stop_rec`, `btn_play_map`, `btn_stop_map`, `btn_lock`, `btn_stop_emerg`, `tgl_dynamic_speed`, `tgl_follow_me`, `tgl_override_speed`) y les asigna listeners.

**Sincronización bidireccional:** Cuando el usuario pulsa un botón, se envía el comando al backend. Simultáneamente, el polling de `FlaskApiClient` trae el estado del backend, y `UpdateUI()` actualiza los colores de los botones, el HUD (velocidad, roll/pitch, emergencia, progreso de ruta) y los toggles.

**Mapa GPS:** Usa una coroutine `InitMapWhenConnected()` que espera a que haya conexión real al servidor antes de inicializar TLab WebView con la URL correcta (`http://{ip}:{port}/gps_only`).

---

#### `Assets/Scripts/UI/CameraPanelController.cs` — Panel de Cámara (49 líneas)

**Componente simple** que almacena el ID de la cámara y la referencia al `RawImage` donde se muestra el vídeo. Sirve de puente entre el layout visual creado por `SceneBuilder` y el `MjpegStreamReceiver` que decodifica el stream.

---

#### `Assets/Scripts/UI/LoadingAnimator.cs` — Animaciones de Carga (52 líneas)

Proporciona dos efectos visuales para los overlays de "Esperando conexión..." **sin necesidad de GIFs externos**:

- **Rotación continua**: Para iconos como ⏳ (sentido horario, **150°/s** — el valor por defecto del script es 200°/s pero `SceneBuilder` lo overridea a `-150f`)
- **Pulsación de opacidad**: El texto parpadea suavemente entre 30% y 100% de opacidad (velocidad de pulso: 3 ciclos/s)

---

#### `Assets/Scripts/UI/NoDragScrollRect.cs` — Anti-Scroll (11 líneas)

**Problema que resuelve:** En VR, el joystick del mando genera eventos de scroll que hacen que el panel de control se desplace involuntariamente cuando el rayo apunta al panel.

**Solución:** Hereda de `ScrollRect` y bloquea `OnBeginDrag`, `OnDrag`, `OnEndDrag` y `OnScroll`. La scrollbar lateral sigue funcionando porque es un componente `Scrollbar` independiente que mueve la posición por drag directo, sin pasar por estos métodos.

---

#### `Assets/Scripts/UI/VRKeyboardFocus.cs` — Teclado Nativo VR (43 líneas)

Al seleccionar un campo de texto (`TMP_InputField`) con el rayo del mando, abre el **teclado nativo de Android** (Meta System Keyboard) usando `TouchScreenKeyboard.Open()`. Sincroniza lo que el usuario escribe de vuelta al campo en cada frame.

> Requiere que `requiresSystemKeyboard = true` esté configurado en `OVRProjectConfig.asset` (lo hace `SceneBuilder` automáticamente).

---

#### `Assets/Scripts/XR/LazyFollowUI.cs` — HUD Dinámico (101 líneas)

Los paneles de la interfaz **siguen al usuario** de forma suave y natural:

| Parámetro               | Valor        | Efecto                                                                                                    |
| ----------------------- | ------------ | --------------------------------------------------------------------------------------------------------- |
| `maxPositionDifference` | 0.15m (15cm) | Zona muerta de traslación — si la cabeza se mueve menos de 15cm, los paneles no se mueven (evita temblor) |
| `maxAngleDifference`    | 40°          | Zona muerta de rotación — puedes girar hasta 40° sin que los paneles se muevan                            |
| `positionLerpSpeed`     | 6            | Velocidad de interpolación de posición (lerp)                                                             |
| `rotationLerpSpeed`     | 4            | Velocidad de interpolación de rotación (lerp)                                                             |

> **Nota técnica:** El script declara `distanceFromHead = 2.5f` pero **no se utiliza** en el cálculo de posición (ver comentario en línea 86-88 del código). La distancia real entre el usuario y los paneles la define `DIST = 2.5f` en `SceneBuilder.FlatPos()`, que posiciona los paneles hijos a Z=2.5 en la jerarquía. `LazyFollowUI` solo mueve el contenedor padre `[UI Panels]` para igualar la posición X,Y,Z de la cabeza con zona muerta, sin añadir offset adicional.

**Comportamiento:** Si estás quieto, los paneles no se mueven. Si caminas y te separas más de 15cm de donde estaban, te siguen suavemente. Si giras la cabeza más de 40°, los paneles se arrastran para volver a centrarse frente a ti.

---

## 🕹️ Controles del Usuario

### Mandos de Meta Quest (en las gafas)

| Control                             | Acción                       | Requiere Conexión |
| ----------------------------------- | ---------------------------- | ----------------- |
| **Joystick Derecho** (arriba/abajo) | Acelerar / Frenar            | ✅ Sí             |
| **Joystick Derecho** (izq./der.)    | Girar el volante             | ✅ Sí             |
| **Botón A** (mano derecha)          | Toggle Passthrough (VR ↔ MR) | ❌ No (local)     |
| **Botón B** (mano derecha)          | Toggle Modo VR ON/OFF        | ✅ Sí             |
| **Grip (gatillo inferior)**         | Toggle Parada de Emergencia  | ✅ Sí             |
| **Trigger (gatillo superior)**      | Click en botones de la UI    | ❌ No (local)     |

### Teclado en PC (Solo para pruebas en el Editor de Unity)

| Tecla                      | Acción                      | Equivale a |
| -------------------------- | --------------------------- | ---------- |
| **↑ / ↓ / ← / →** (o WASD) | Acelerar, Frenar, Girar     | Joystick   |
| **V**                      | Toggle Modo VR              | Botón B    |
| **P**                      | Toggle Passthrough          | Botón A    |
| **Barra Espaciadora**      | Toggle Parada de Emergencia | Grip       |

> **Nota:** Los controles de teclado usan el sistema antiguo de Input (`Input.GetKeyDown`) y requieren que el Input System esté configurado en **Both** (ver Paso 6).

---

## 🔗 Endpoints de la API Flask

Todos los endpoints que `FlaskApiClient.cs` consume del servidor Flask (`web_node.py`):

### Endpoints de Lectura (GET) — Polling cada 0.5s

| Endpoint                   | Respuesta                                      | Usado por                                |
| -------------------------- | ---------------------------------------------- | ---------------------------------------- |
| `/get_telemetry`           | `{"status": {"speed": 12.5, "steering": 0.3}}` | HUD de velocidad                         |
| `/get_mode_mission_status` | `{"status": 2}`                                | Botones de modo (StandBy/Auto/Tele/Nav2) |
| `/get_land_meter_status`   | `{"status": {"x": 1.2, "y": -0.5}}`            | Medidores Roll/Pitch                     |
| `/get_waypoint_info`       | `{"status": "Waypoint 3 de 10"}`               | Info de waypoints en HUD                 |
| `/get_route_progress`      | `{"progress": 45.2}`                           | Porcentaje de ruta                       |
| `/get_ui_state`            | `{...estados de toggles, emergencia, modo...}` | Sincronización bidireccional de UI       |
| `/get_num_cameras`         | `{"num_cameras": 5}`                           | Configuración inicial                    |
| `/get_available_maps`      | `{"map_names": ["ruta1", "ruta2"]}`            | Dropdown de mapas                        |

### Endpoints de Escritura (POST) — Bajo demanda

| Endpoint                   | Body JSON                                       | Acción                                     |
| -------------------------- | ----------------------------------------------- | ------------------------------------------ |
| `/publish_modo_mision`     | `{"mode": 2}`                                   | Cambiar modo de misión                     |
| `/publish_stop_brakes`     | `{"value": true}`                               | Activar/desactivar freno de emergencia HMI |
| `/api/control`             | `{"boton":"joystick","x":0.5,"y":0.8}`          | Enviar joystick VR                         |
| `/api/control`             | `{"boton":"vr_mode","estado":true}`             | Toggle VR Mode                             |
| `/api/control`             | `{"boton":"vr_emergency","estado":true}`        | Emergencia desde VR                        |
| `/publish_signal_override` | `{"value": true}`                               | Override de señal de velocidad             |
| `/publish_followme`        | `{"type": "person"}`                            | Seleccionar Follow Person/Vehicle          |
| `/publish_track_name`      | `{"name":"ruta1","speed_type":true,"speed":10}` | Grabar ruta                                |
| `/publish_map`             | `{"map_name":"ruta1","mission":1,"mode":-1}`    | Reproducir mapa                            |
| `/api/update_ui_state`     | `{"key":"speedTypeChecked","value":"true"}`     | Sincronizar estado UI                      |

### Stream de Vídeo (GET — Conexión persistente)

| Endpoint                          | Tipo  | Descripción                    |
| --------------------------------- | ----- | ------------------------------ |
| `/video_feed/1`                   | MJPEG | Stream de cámara principal     |
| `/video_feed/2` a `/video_feed/5` | MJPEG | Streams de cámaras secundarias |

---

## ⚠️ Problemas Conocidos y Soluciones (Troubleshooting)

### Errores durante la Instalación

| #   | Problema                                                             | Causa                                                                    | Solución                                                                                                                      |
| --- | -------------------------------------------------------------------- | ------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Errores `CS0619` masivos** (50+ errores) al importar Meta SDK      | Unity **6000.5.x** (Preview/Beta) rompe APIs internas del SDK            | Usar Unity **6000.0.77f1** (LTS). Ver [Paso 1](#paso-1-instalar-unity-6-lts-600007f1)                                         |
| 2   | **`CS1032: Cannot define/undefine preprocessor symbols`**            | Bug del SDK de Meta en compiladores de Linux                             | Comentar las líneas `#define`/`#undef` en `RuntimeOptimizerPlugin.cs`. Ver [Paso 3](#paso-3-arreglar-error-cs1032-solo-linux) |
| 3   | **`InvalidOperationException: Input using UnityEngine.Input class`** | Input System configurado solo en "New"                                   | Cambiar a **Both** en `Project Settings > Player > Active Input Handling`. Ver [Paso 6](#paso-6-configurar-input-system)      |
| 4   | **`Package Manager Window Error: auth code`**                        | Bug intermitente de Unity Hub en Linux (token de autenticación caducado) | **Ignorar.** No afecta al proyecto. Dar clic en "Clear" en la consola                                                         |

### Errores en Tiempo de Ejecución

| #   | Problema                                                       | Causa                                                                               | Solución                                                                                    |
| --- | -------------------------------------------------------------- | ----------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| 5   | **Panel GPS siempre negro** (en PC)                            | TLab WebView es **Android-only**. En el Editor de Linux/Windows no puede renderizar | **Normal.** Funciona correctamente en las gafas Quest. No hay workaround para PC            |
| 6   | **`DllNotFoundException: OVRPlugin`** (errores rojos en Linux) | El SDK de Meta busca DLLs nativas de Windows/Android que no existen en Linux        | **Ignorar.** Todo funcionará correctamente al compilar el APK para las gafas                |
| 7   | **Solo se ven 2 de 5 cámaras**                                 | .NET/Mono limita a 2 conexiones HTTP simultáneas a la misma IP por defecto          | `MjpegStreamReceiver` ya lo soluciona con `ServicePointManager.DefaultConnectionLimit = 10` |
| 8   | **Joystick hace scroll en el panel de control**                | El ScrollRect intercepta eventos del eje vertical del joystick XR                   | `NoDragScrollRect` bloquea `OnScroll()`. Ya está implementado                               |
| 9   | **Botón A (Passthrough) no funciona sin conexión al servidor** | El guard clause bloqueaba TODOS los botones                                         | Ya solucionado: separación en `HandleLocalButtons()` y `HandleNetworkButtons()`             |
| 10  | **Botón B (VR Mode) no se refleja en la interfaz web**         | Usaba el endpoint incorrecto (`/publish_modo_mision` en vez de `/api/control`)      | Ya solucionado: `SendVrMode()` ahora usa `/api/control` con `"boton":"vr_mode"`             |

### Problemas de Conexión/Hardware

| #   | Problema                                       | Causa                                                        | Solución                                                                               |
| --- | ---------------------------------------------- | ------------------------------------------------------------ | -------------------------------------------------------------------------------------- |
| 11  | **ADB no detecta las gafas sin `sudo`**        | Falta regla udev para el Vendor ID de Meta (2833)            | Crear regla udev. Ver [Paso 11](#paso-11-configurar-adb-conexión-usb-con-las-gafas)    |
| 12  | **Meta XR Simulator no funciona en Linux**     | Solo disponible para Windows/Mac                             | Usar atajos de teclado (V, P, Espacio) + Scene View del editor                         |
| 13  | **Las gafas no se conectan al servidor Flask** | Las gafas y el PC del vehículo no están en la misma red WiFi | Verificar que ambos dispositivos están conectados a la misma red. Comprobar con `ping` |

### ¿Por qué NO usar Unity 6000.5.0.0 o superior?

La versión **6000.5** es una release **Preview/Beta** de Unity 6 que contiene cambios drásticos en sus APIs internas. Durante el desarrollo se instaló por error y se descubrió que:

1. Funciones como `GetInstanceID` fueron eliminadas o movidas de namespace
2. Más de **50 archivos internos** del SDK de Meta generaban errores `CS0619`
3. Parchear los archivos uno a uno era inviable (eran demasiados y nuevos errores aparecían)
4. La única solución viable fue desinstalar la versión 6000.5 e instalar la **LTS 6000.0.77f1**

> **Consejo:** Si al abrir Unity Hub ves versiones como 6000.5.x, 6001.x o similares, **NO las instales** para este proyecto. Usa siempre la rama **6000.0.x** (LTS).

---

## 🧪 Testing y Verificación

### Probar en PC (sin gafas)

Puedes probar parcialmente el proyecto sin necesidad de las Meta Quest:

1. **Lanza el servidor Flask** en el PC (o en la Jetson, accesible por red).
2. En Unity, dale a **Play** (botón ▶ arriba).
3. Usa los **atajos de teclado**:
   - `WASD` / `Flechas` → Joystick (solo funciona si VR Mode está activo)
   - `V` → Toggle VR Mode (equivale a Botón B)
   - `P` → Toggle Passthrough (equivale a Botón A)
   - `Espacio` → Toggle Emergency Stop (equivale a Grip)

**Limitaciones en PC:**

- El panel del **mapa GPS se verá vacío/negro** (TLab WebView es Android-only)
- Los errores rojos de `OVRPlugin` en la consola son normales en Linux
- No podrás probar la interacción por rayo (el cursor del ratón no simula el rayo XR a menos que instales XR Device Simulator)

### Probar en las Gafas

1. Haz `Build And Run` (Paso 13).
2. Ponte las gafas.
3. La interfaz debería aparecer frente a ti con todos los paneles.
4. Verifica:
   - ✅ Los paneles siguen tu cabeza suavemente (LazyFollow)
   - ✅ El rayo del mando derecho puede hacer clic en botones
   - ✅ Las cámaras muestran vídeo (si el servidor Flask está corriendo)
   - ✅ El botón A cambia entre VR y Passthrough
   - ✅ El mapa GPS muestra la página web (si el servidor Flask está corriendo)
   - ✅ El joystick envía comandos de dirección

### Verificar Endpoints del Servidor

Para comprobar que el servidor Flask funciona, abre un navegador en tu PC y visita:

- `http://<IP_SERVIDOR>:5050/get_telemetry` → Debería devolver JSON con velocidad
- `http://<IP_SERVIDOR>:5050/video_feed/1` → Debería mostrar el vídeo MJPEG de la cámara 1
- `http://<IP_SERVIDOR>:5050/gps_only` → Debería mostrar la página del mapa GPS

---

## 📡 Configuración del Backend (Lado del Vehículo)

El backend que alimenta a las gafas es un servidor **Flask** integrado con **ROS 2**. El código está en los paquetes:

- `web_interface_pkg` — Para vehículos de 3 cámaras
- `web_interface_pkg_8x8` — Para el vehículo 8x8 de 5 cámaras

### Lanzar el Backend

```bash
# 1. Source del workspace de ROS 2
source ~/ros2_ws/install/setup.bash

# 2. Lanzar el nodo Flask (ajusta el launch file según tu vehículo)
ros2 launch web_interface_pkg_8x8 <nombre>.launch.py
```

### UDP Broadcast

El servidor Flask ya incluye código que emite automáticamente un broadcast UDP cada pocos segundos con la información de conexión:

```json
{
  "service": "vehicle_control",
  "vehicle": "MUTT_8x8",
  "ip": "192.168.1.50",
  "port": 5050
}
```

Las gafas escuchan en el puerto UDP **5555** y auto-configuran la IP al recibir este paquete.

### Simular Cámaras (para pruebas)

Si no tienes cámaras reales conectadas:

```bash
python3 src/mock_cameras.py
```

Esto lanza un servidor MJPEG que genera imágenes de test en los endpoints `/video_feed/1` a `/video_feed/5`.

---

## 🛠️ Notas para Desarrolladores

### Regenerar la Escena

Si modificas `SceneBuilder.cs` (por ejemplo, para cambiar el layout, añadir un panel o ajustar colores):

1. Abre Unity.
2. Ejecuta `INSIA > Crear Sala de Control (Rediseño Web)`.
3. La escena anterior se sobreescribirá.
4. **Importante:** Después de regenerar, debes volver a hacer el [Paso 10](#paso-10-configurar-interacción-xr-building-blocks) (añadir Ray Interactions y Make Canvas Interactable).

### Convenciones de Nombres de Botones

`ControlPanelUI.cs` busca botones por nombre (`GameObject.name`). Si añades un botón nuevo en `SceneBuilder`, usa estos IDs:

| ID del Botón         | Función                            |
| -------------------- | ---------------------------------- |
| `btn_standby`        | Modo StandBy (0)                   |
| `btn_auto`           | Modo Autónomo (1)                  |
| `btn_tele`           | Modo Teleoperado (2)               |
| `btn_nav2`           | Modo Nav2 (4)                      |
| `btn_follow_person`  | Follow Person (3)                  |
| `btn_follow_vehicle` | Follow Vehicle (3)                 |
| `btn_record`         | Grabar ruta                        |
| `btn_stop_rec`       | Detener grabación                  |
| `btn_play_map`       | Reproducir mapa                    |
| `btn_stop_map`       | Detener reproducción               |
| `btn_lock`           | Candado de vídeo                   |
| `btn_stop_emerg`     | Parada de emergencia UI            |
| `btn_map_hand`       | Toggle interacción libre del mapa  |
| `tgl_dynamic_speed`  | Toggle velocidad dinámica/estática |
| `tgl_follow_me`      | Toggle Follow Me waypoints         |
| `tgl_override_speed` | Toggle override de señal           |

### Cómo Añadir una Cámara Nueva

1. En `SceneBuilder.cs`, añade una nueva llamada a `BuildSideCam()` con un `camId` de 6 (o el que corresponda):

   ```csharp
   BuildSideCam(root.transform, "CAM 6", 6, FlatPos(x, y));
   ```

2. El `SceneBuilder` ya asigna automáticamente un `MjpegStreamReceiver` con el `cameraId` correcto a cada panel que tenga `CameraPanelController`.

3. En el backend Flask, asegúrate de que existe el endpoint `/video_feed/6` para la nueva cámara.

### Pipeline de Render

El proyecto usa **Universal Render Pipeline (URP) 17.0.4**. Si necesitas cambiar materiales o shaders, asegúrate de que sean compatibles con URP (no con Built-in ni HDRP).

### Estructura del Input System

El proyecto tiene configurado `InputSystem_Actions.inputactions` en `Assets/` para las acciones de XR Interaction Toolkit. Los controles del vehículo (joystick, botones) se leen directamente via `OVRInput` (en gafas) o `Input.GetKeyDown` (en PC), **no** a través de este archivo de acciones.

---

## 📄 Licencia y Créditos

Desarrollado para el **Instituto Universitario de Investigación del Automóvil (INSIA)** — Universidad Politécnica de Madrid.

### Dependencias Externas

| Dependencia               | Licencia                | URL                                                                                              |
| ------------------------- | ----------------------- | ------------------------------------------------------------------------------------------------ |
| Meta XR All-in-One SDK    | Meta Platform License   | [developer.meta.com](https://developer.meta.com/)                                                |
| TLab WebView              | MIT License             | [github.com/TLabAltoh/TLabWebView](https://github.com/TLabAltoh/TLabWebView)                     |
| TLab VKeyboard            | MIT License             | [github.com/TLabAltoh/TLabVKeyborad](https://github.com/TLabAltoh/TLabVKeyborad)                 |
| XR Interaction Toolkit    | Unity Companion License | [docs.unity3d.com](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.0/)      |
| Universal Render Pipeline | Unity Companion License | [docs.unity3d.com](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/) |
