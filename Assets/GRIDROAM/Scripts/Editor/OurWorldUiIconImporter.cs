using UnityEditor;

namespace OurWorld.Editor
{
    /// <summary>
    /// Imports the Blender-rendered HUD icons (Assets/GRIDROAM/Resources/UI/Icons) as crisp UI sprites.
    /// </summary>
    public class OurWorldUiIconImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/UI/Icons/")) return;

            TextureImporter ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.sRGBTexture = true;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            ti.filterMode = UnityEngine.FilterMode.Bilinear;
            ti.maxTextureSize = 256;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
