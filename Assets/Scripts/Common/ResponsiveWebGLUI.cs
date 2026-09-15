using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResponsiveWebGLUI : MonoBehaviour
{
    public Canvas canvas;

    void Start()
    {
        AdjustCanvasForScreen();
    }

    void AdjustCanvasForScreen()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null) return;

        float aspectRatio = (float)Screen.width / Screen.height;

        
        if (aspectRatio < 1.7f) // Typically mobile has a taller aspect ratio
        {
            Debug.Log("Mobile Screen Detected");
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.0f; // Prioritize height
        }
        else
        {
            Debug.Log("Desktop Screen Detected");
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1.0f; // Prioritize width
        }
    }
}
