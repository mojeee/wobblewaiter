using UnityEditor;

namespace WobbleWaiter.EditorTools
{
    // Characters keep their animations as legacy clips so the game can play them by name
    // ("sprint", "sit", "holding-both"...). Food and furniture are static.
    public class ModelImportRules : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Resources/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            if (assetPath.Contains("/Models/Chars/"))
            {
                mi.animationType = ModelImporterAnimationType.Legacy;
                mi.importAnimation = true;
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.Contains("/Models/Chars/")) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.loopTime = true;
                c.wrapMode = UnityEngine.WrapMode.Loop;
            }
            mi.clipAnimations = clips;
        }
    }
}
