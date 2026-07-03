using UnityEngine;
using System.Collections;

/// <summary>
/// "Lazy Follow" HUD dinámico para VR.
/// Sigue la posición del usuario de forma exacta pero rota de forma suave (con zona muerta).
/// Si el usuario gira la cabeza más allá de `maxAngleDifference`, el panel "tira" de sí mismo
/// para volver a centrarse suavemente.
/// </summary>
public class LazyFollowUI : MonoBehaviour
{
    [Header("Posición (Caminar)")]
    public bool followPosition = true;
    [Tooltip("Distancia a la que flotan los paneles respecto a la cabeza")]
    public float distanceFromHead = 2.5f; 
    [Tooltip("Umbral de movimiento en metros antes de arrastrar los paneles (evita temblor al girar el cuello)")]
    public float maxPositionDifference = 0.15f; // 15 cm de zona muerta de traslación
    public float positionLerpSpeed = 6f;

    [Header("Rotación (Girar Cabeza)")]
    public bool followRotation = true;
    [Tooltip("Grados que puedes girar la cabeza sin que el panel se mueva (Zona Muerta)")]
    public float maxAngleDifference = 40f; 
    public float rotationLerpSpeed = 4f;

    private Transform _head;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    
    // Variables de smoothing
    private float _currentYaw;
    private Vector3 _currentPos;

    IEnumerator Start()
    {
        // Esperamos 0.5s a que el tracking de Meta Quest se estabilice
        yield return new WaitForSeconds(0.5f);

        if (Camera.main != null)
            _head = Camera.main.transform;

        // Inicializar instantáneo para evitar saltos raros al arrancar
        if (_head != null)
        {
            _currentYaw = _head.eulerAngles.y;
            _currentPos = _head.position;
            UpdateTargetTransforms();
            transform.position = _targetPosition;
            transform.rotation = _targetRotation;
        }
    }

    void Update()
    {
        if (_head == null) return;

        UpdateTargetTransforms();

        if (followPosition)
        {
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * positionLerpSpeed);
        }

        if (followRotation)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * rotationLerpSpeed);
        }
    }

    void UpdateTargetTransforms()
    {
        // 1. ROTACIÓN: Solo actualizar _currentYaw si la cabeza gira más del máximo permitido
        float headYaw = _head.eulerAngles.y;
        float angleDiff = Mathf.DeltaAngle(_currentYaw, headYaw);
        
        if (Mathf.Abs(angleDiff) > maxAngleDifference)
        {
            // Ajustar el yaw actual para que la diferencia nunca supere el maxAngle
            // Esto arrastra el panel suavemente mientras giramos la cabeza más allá del límite
            _currentYaw = headYaw - Mathf.Sign(angleDiff) * maxAngleDifference;
        }

        _targetRotation = Quaternion.Euler(0, _currentYaw, 0);

        // 2. POSICIÓN: Mantener el pivote central a `distanceFromHead` frente al usuario,
        // OJO: Como nuestros paneles hijos YA tienen un offset en Z (ej: Z=2.5) definido en el SceneBuilder,
        // si sumáramos offset aquí los alejaríamos el doble.
        // SOLUCIÓN: Solo hacemos que el contenedor padre iguale la posición X,Y,Z de la cabeza (con zona muerta).
        
        float dist = Vector3.Distance(_currentPos, _head.position);
        if (dist > maxPositionDifference)
        {
            // Tirar del panel hacia la cabeza, manteniendo el margen del umbral
            Vector3 direction = (_currentPos - _head.position).normalized;
            _currentPos = _head.position + direction * maxPositionDifference;
        }

        _targetPosition = _currentPos;
    }
}
