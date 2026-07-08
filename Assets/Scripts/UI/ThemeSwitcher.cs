using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ThemeSwitcher : MonoBehaviour
{
    private bool isLightMode = false;

    // Colores Originales (Oscuros)
    private Color darkPanelBg = new Color(0.082f, 0.090f, 0.118f); // #15171e
    private Color darkSectionBg = new Color(0.110f, 0.118f, 0.149f); // #1c1e26 (Aproximado)
    private Color darkBorder = new Color(0.173f, 0.192f, 0.251f); // #2c3140
    
    // Colores Claros
    private Color lightPanelBg = new Color(0.95f, 0.95f, 0.96f);
    private Color lightSectionBg = new Color(1f, 1f, 1f);
    private Color lightBorder = new Color(0.8f, 0.8f, 0.85f);

    private List<Image> backgrounds = new List<Image>();
    private List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
    
    // Diccionarios para cachear el color EXACTO (incluyendo alfa) original
    private Dictionary<Image, Color> originalImageColors = new Dictionary<Image, Color>();
    private Dictionary<TextMeshProUGUI, Color> originalTextColors = new Dictionary<TextMeshProUGUI, Color>();

    public TextMeshProUGUI myLabel;

    void Start()
    {
        // Buscar todos los componentes Image y Text hijos de este panel (RightPanel)
        Image[] imgs = GetComponentsInChildren<Image>(true);
        foreach (var img in imgs)
        {
            // Ignorar las imágenes que son botones (tienen componente Button) para no estropear los colores verde/rojo
            if (img.GetComponent<Button>() != null) continue;
            // Ignorar barras de scroll
            if (img.GetComponent<Scrollbar>() != null) continue;
            
            backgrounds.Add(img);
            originalImageColors[img] = img.color; // Guardar color pixel-perfect
        }

        TextMeshProUGUI[] txts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in txts)
        {
            // Ignorar textos dentro de botones de acción
            if (txt.GetComponentInParent<Button>() != null && txt.transform.parent != this.transform) continue;
            
            labels.Add(txt);
            originalTextColors[txt] = txt.color; // Guardar color exacto original
        }

        // Encontrar el botón y vincular el evento en tiempo de ejecución
        var btnTransform = transform.Find("Footer/btn_light_mode");
        if (btnTransform != null)
        {
            var btn = btnTransform.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(ToggleTheme);
                
            var lblTrans = btnTransform.Find("Lbl");
            if (lblTrans != null)
                myLabel = lblTrans.GetComponent<TextMeshProUGUI>();
        }
    }

    public void ToggleTheme()
    {
        isLightMode = !isLightMode;

        foreach (var img in backgrounds)
        {
            if (isLightMode)
            {
                if (IsClose(originalImageColors[img], darkPanelBg)) img.color = lightPanelBg;
                else if (IsClose(originalImageColors[img], darkBorder)) img.color = lightBorder;
                else img.color = lightSectionBg; // Por defecto secciones claras
            }
            else
            {
                // Volver EXACTAMENTE a su color original (preserva alfas sutiles)
                img.color = originalImageColors[img];
            }
        }

        foreach (var txt in labels)
        {
            if (isLightMode)
            {
                if (originalTextColors[txt].r > 0.5f) txt.color = new Color(0.1f, 0.1f, 0.1f, originalTextColors[txt].a);
            }
            else
            {
                // Restaurar color exacto
                txt.color = originalTextColors[txt];
            }
        }

        if (myLabel != null)
        {
            myLabel.text = isLightMode ? "* Switch to Dark Mode" : "* Switch to Light Mode";
        }
    }

    bool IsClose(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.1f && Mathf.Abs(a.g - b.g) < 0.1f && Mathf.Abs(a.b - b.b) < 0.1f;
    }
}
