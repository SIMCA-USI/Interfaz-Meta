using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Proporciona un efecto de carga sin necesidad de GIFs externos.
/// - Rota el transform (útil para iconos como ⏳ o 🔄)
/// - Hace pulsar el color (útil para el texto "Esperando...")
/// </summary>
public class LoadingAnimator : MonoBehaviour
{
    [Header("Rotación")]
    public bool enableRotation = true;
    public float rotationSpeed = -200f; // Grados por segundo (negativo = sentido horario)

    [Header("Latido (Opacidad)")]
    public bool enablePulsing = true;
    public float pulseSpeed = 3f;
    public float minAlpha = 0.3f;
    public float maxAlpha = 1.0f;

    private Graphic _graphic; // Puede ser un Text, TextMeshPro o Image
    private Color _baseColor;

    void Start()
    {
        _graphic = GetComponent<Graphic>();
        if (_graphic != null)
        {
            _baseColor = _graphic.color;
        }
    }

    void Update()
    {
        if (enableRotation)
        {
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        }

        if (enablePulsing && _graphic != null)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f; // normalizado de 0 a 1
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
            
            Color newColor = _graphic.color;
            newColor.a = _baseColor.a * alpha; // Escala la opacidad original
            _graphic.color = newColor;
        }
    }
}
