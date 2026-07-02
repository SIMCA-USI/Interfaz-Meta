using UnityEngine;
using System.Collections;

/// <summary>
/// Fuerza a las pantallas a posicionarse frente al usuario (eje Z=0 del tracking real)
/// y a su altura exacta al arrancar la aplicación.
/// </summary>
public class RecenterOnStart : MonoBehaviour
{
    IEnumerator Start()
    {
        // Esperamos 0.5s a que el tracking de Meta Quest se estabilice
        yield return new WaitForSeconds(0.5f);

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // 1. Alineamos la altura: Ponemos los paneles a la altura exacta de los ojos del usuario
            Vector3 pos = transform.position;
            pos.y = mainCam.transform.position.y;
            transform.position = pos;

            // 2. Alineamos la rotación: Ponemos los paneles de frente
            float userYaw = mainCam.transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0, userYaw, 0);
            
            // Re-centramos el tracking de hardware (API nueva de Unity 6)
            var subsystems = new System.Collections.Generic.List<UnityEngine.XR.XRInputSubsystem>();
            SubsystemManager.GetSubsystems<UnityEngine.XR.XRInputSubsystem>(subsystems);
            foreach (var subsystem in subsystems)
            {
                subsystem.TryRecenter();
            }
            
            Debug.Log($"[RecenterOnStart] Paneles movidos a Y={pos.y} y rotados a {userYaw} grados.");
        }
    }
}
