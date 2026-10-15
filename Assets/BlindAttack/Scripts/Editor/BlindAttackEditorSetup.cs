#if UNITY_EDITOR
using BlindAttack.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlindAttack.EditorTools
{
    /// <summary>
    /// Editör açıldığında oyunun kullandığı temel malzemeleri Resources klasörüne oluşturur.
    /// (Build alındığında URP shader'larının build'e girmesi için gerekli.)
    /// </summary>
    [InitializeOnLoad]
    static class BlindAttackEditorSetup
    {
        const string Folder = "Assets/BlindAttack/Resources";

        static BlindAttackEditorSetup()
        {
            EditorApplication.delayCall += () => { EnsureMaterials(); EnsureScene(false); };
            EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += () => EnsureScene(false);
        }

        // ---------------- Sahne kurulumu ----------------

        [MenuItem("Blind Attack/Sahneyi Kur")]
        static void SetupSceneMenu() => EnsureScene(true);

        /// <summary>
        /// Oyun sahnesinde Blind Attack, Harita, Çevre nesneleri yoksa ekler; kamera ve ışığı ayarlar.
        /// Böylece harita oyun çalışmıyorken de Hierarchy ve Scene penceresinde görünür.
        /// </summary>
        static void EnsureScene(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            bool isGameScene = EditorBuildSettings.scenes.Length > 0 && EditorBuildSettings.scenes[0].path == scene.path;
            if (!force && !isGameScene) return;
            if (!force && Object.FindAnyObjectByType<GameApp>() != null) return;

            bool changed = false;
            if (Object.FindAnyObjectByType<GameApp>() == null)
            {
                var go = new GameObject("Blind Attack");
                go.AddComponent<GameApp>();
                go.transform.SetSiblingIndex(0);
                changed = true;
            }
            if (Object.FindAnyObjectByType<MapView>() == null)
            {
                new GameObject("Harita").AddComponent<MapView>();
                changed = true;
            }
            if (Object.FindAnyObjectByType<EnvironmentView>() == null)
            {
                new GameObject("Çevre").AddComponent<EnvironmentView>();
                changed = true;
            }
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<IsoCamera>() == null)
            {
                cam.gameObject.AddComponent<IsoCamera>();
                changed = true;
            }
            GameApp.ApplyLook(cam);
            if (changed || force)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
                Debug.Log("[BlindAttack] Sahne kuruldu: Blind Attack, Harita ve Çevre nesneleri eklendi.");
            }
        }

        [MenuItem("Blind Attack/Malzemeleri Yeniden Oluştur")]
        static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/BlindAttack")) AssetDatabase.CreateFolder("Assets", "BlindAttack");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/BlindAttack", "Resources");
            Create("BlindAttackLit", "Universal Render Pipeline/Lit", Matte);
            Create("BlindAttackLitGlow", "Universal Render Pipeline/Lit", m => { Matte(m); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; });
            Create("BlindAttackUnlit", "Universal Render Pipeline/Unlit", null);
            Create("BlindAttackUnlitTransparent", "Universal Render Pipeline/Unlit", MakeTransparent);
            AssetDatabase.SaveAssets();
        }

        static void Matte(Material m)
        {
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        static void Create(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{Folder}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogWarning($"[BlindAttack] Shader bulunamadı: {shaderName}"); return; }
            var mat = new Material(shader);
            setup?.Invoke(mat);
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"[BlindAttack] {path} oluşturuldu.");
        }
    }
}
#endif
