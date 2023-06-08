using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarfieldController : MonoBehaviour
{
    [SerializeField]
    Material spaceTextureMaterial;
    public float noiseScaleX = 1.0f;
    public float noiseScaleY = 1.0f;
    public float noiseStrength = 1.0f;

    readonly static int textureWidth = 512;
    readonly static int textureHeight = 512;
    //float starDensity = 0.1f;
    Texture2D spaceTexture;

    // Start is called before the first frame update
    void Start()
    {
        spaceTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
    }

    // Update is called once per frame
    void Update()
    {
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                //TODO try implementing poisson disk
                //spaceTexture.SetPixel(x, y, new Color(brightness, brightness, brightness));
            }
        }
        spaceTexture.Apply();
        spaceTextureMaterial.mainTexture = spaceTexture;
    }
}
