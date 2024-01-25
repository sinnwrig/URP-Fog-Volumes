using UnityEngine;


// From https://forum.unity.com/threads/solved-rainbow-hue-shift-over-time-c-script.351751/

[RequireComponent(typeof(Light))]
public class RainbowLight : MonoBehaviour
{
    public float speed = 1;


    private Light _light;
    public Light Light
    {
        get
        {
            if (_light == null)
                _light = GetComponent<Light>();

            return _light;
        }
    }

 
    void Update()
    {
        // Assign HSV values to float h, s & v. (Since material.color is stored in RGB)
        float h, s, v;
        Color.RGBToHSV(Light.color, out h, out s, out v);

        Light.color = Color.HSVToRGB(h + Time.deltaTime * speed, s, v);
    }
}