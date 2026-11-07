using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Arena
{
    /// <summary>Command-line A/B probe for wall silhouettes; never changes serialized renderer assets.</summary>
    public static class RenderDiagnostics
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplyCommandLine()
        {
            bool disable = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-no-xray") >= 0;
            bool review = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-review") >= 0;
            if (!disable && !review) return;
            int count = 0;
            foreach (var data in Resources.FindObjectsOfTypeAll<UniversalRendererData>())
            {
                foreach (var feature in data.rendererFeatures)
                {
                    if (feature == null || feature.name != "FighterXRay") continue;
                    count++;
                    if (disable) feature.SetActive(false);
                    var render = feature as RenderObjects;
                    var material = render != null ? render.settings.overrideMaterial : null;
                    Debug.Log("[RenderDiagnostics] " + data.name + "/" + feature.name + " active=" + feature.isActive
                        + " material=" + (material != null ? material.name : "MISSING")
                        + " shader=" + (material != null && material.shader != null ? material.shader.name : "MISSING")
                        + " supported=" + (material != null && material.shader != null && material.shader.isSupported)
                        + " passes=" + (material != null ? material.passCount : 0));
                }
            }
            Debug.Log("[RenderDiagnostics] XRay features=" + count + " disabled=" + disable);
        }
    }
}
