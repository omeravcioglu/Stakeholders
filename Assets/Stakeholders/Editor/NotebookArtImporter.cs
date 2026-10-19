using UnityEditor;
using UnityEngine;

namespace Stakeholders.EditorTools
{
    /// <summary>
    /// Import settings for the notebook UI art (Tools/NotebookArt renders it at 2x). Keeps the true pixel size
    /// (no power-of-two rescaling, which would break the 9-slice margins in Notebook.uss), full quality, and
    /// mipmaps so icons and faces stay smooth when drawn small. Tiles repeat; everything else clamps.
    /// </summary>
    public class NotebookArtImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Stakeholders/Resources/StakeholdersUI/Notebook/";

        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = assetPath.Contains("/tiles/") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.isReadable = false;
        }
    }
}
