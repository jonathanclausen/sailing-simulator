using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TillerTide.Editor
{
    /// <summary>
    /// One-click project setup so the repo only has to carry code: creates the URP pipeline asset, the main
    /// scene and the iOS player settings. Runs once automatically when the project is first opened, and again
    /// from the menu "Tiller & Tide > Set up project".
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string SettingsDir = "Assets/TillerTide/Settings";
        private const string ScenePath = "Assets/TillerTide/Scenes/Main.unity";
        private const string BundleId = "com.jonathanclausen.tillertide";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (GraphicsSettings.defaultRenderPipeline == null || !File.Exists(ScenePath)) Run();
            };
        }

        [MenuItem("Tiller & Tide/Set up project")]
        public static void Run()
        {
            SetUpPipeline();
            SetUpPlayer();
            SetUpScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Tiller & Tide: project set up. Press Play, or File > Build Settings > iOS > Build.");
        }

        private static void SetUpPipeline()
        {
            Directory.CreateDirectory(SettingsDir);
            string rendererPath = SettingsDir + "/TillerTide Renderer.asset";
            string pipelinePath = SettingsDir + "/TillerTide URP.asset";

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            pipeline.shadowDistance = 90f;
            pipeline.shadowCascadeCount = 2;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1f;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, false);
        }

        private static void SetUpPlayer()
        {
            PlayerSettings.companyName = "Jonathan Clausen";
            PlayerSettings.productName = "Tiller & Tide";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // The HUD and camera read touches through the classic Input class, so make sure it is enabled.
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets.Length > 0)
            {
                var so = new SerializedObject(assets[0]);
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue == 1)
                {
                    handler.intValue = 2; // Both
                    so.ApplyModifiedProperties();
                    Debug.LogWarning("Tiller & Tide: enabled the classic Input Manager. Restart Unity once for it to take effect.");
                }
            }
        }

        private static void SetUpScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                // The game builds everything at runtime (GameController), so the scene is empty.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (EditorSceneManager.GetActiveScene().path != ScenePath && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
