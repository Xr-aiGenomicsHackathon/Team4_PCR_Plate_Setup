using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class LabEquipmentGuideEditor
{
    public const string Folder = "Assets/UI/LabEquipment/";
    public const string ScenePath = "Assets/Scenes/Lab  Preparation Guide.unity";
    [MenuItem("Tools/PCR Equipment/Install or repair guide")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before installing the guide.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open Lab  Preparation Guide before installing.");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var document = all.First(t => t.name == "PCR Guide UI").GetComponent<UIDocument>();
        var panel = all.First(t => t.name == "PCR Guide World Panel");
        Undo.RecordObject(document, "Install equipment guide");
        document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "LabEquipmentGuide.uxml");
        document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder + "LabEquipmentPanelSettings.asset");
        var settings = document.panelSettings;
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(Folder + "LabEquipmentGuide.renderTexture");
        settings.targetTexture = rt; settings.scaleMode = PanelScaleMode.ConstantPixelSize; settings.scale = 1;
        settings.clearColor = true; settings.colorClearValue = Color.clear;
        rt.Release(); rt.depth = 24; rt.Create();
        panel.GetComponentInChildren<UnityEngine.UI.RawImage>(true).texture = rt;
        panel.gameObject.SetActive(true);
        foreach (var behaviour in document.GetComponents<MonoBehaviour>().Concat(panel.GetComponents<MonoBehaviour>()))
            if (behaviour != null && new[] { "PCRGuideUIController", "PCRGuideWorldPanelControls", "PCRGuideWorldPanelButtonBridge" }.Contains(behaviour.GetType().Name))
            { Undo.RecordObject(behaviour, "Use equipment guide"); behaviour.enabled = false; }
        var controller = document.GetComponent<LabEquipmentGuideController>();
        if (controller == null) controller = Undo.AddComponent<LabEquipmentGuideController>(document.gameObject);
        var so = new SerializedObject(controller);
        so.FindProperty("document").objectReferenceValue = document;
        so.FindProperty("hitTargetRoot").objectReferenceValue = panel.Find("Hit Targets");
        so.FindProperty("nextScene").stringValue = "Assets/Scenes/StarterScene.unity";
        so.ApplyModifiedProperties();
        RenderModel("Pipette", "Assets/Prefabs/Pipette.fbx", "Multiface_pippete_scene_Multiface_pippete_scene_group3");
        RenderModel("PCRPlate", "Assets/Prefabs/pcr_plate3.fbx", "PCR_Plate_Rack");
        RenderModel("Tubes", "Assets/Prefabs/microcentrifuge_tubes.fbx", null);
        RenderModel("Reservoir", "Assets/Prefabs/low_poly_tray.fbx", null);
        RenderModel("Cover", "Assets/Prefabs/pcr_plate3.fbx", "Cover");
        var images = new[] { "Pipette", "PCRPlate", "Tubes", "Reservoir", "Cover" };
        so.Update();
        var slides = so.FindProperty("slides");
        if (slides.arraySize != images.Length) throw new InvalidOperationException("Expected the scene's five equipment slides.");
        for (int i = 0; i < images.Length; i++) slides.GetArrayElementAtIndex(i).FindPropertyRelative("image").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + images[i] + ".png");
        so.ApplyModifiedProperties();
        foreach (Transform child in panel.Find("Hit Targets"))
            if (child.name.StartsWith("Equipment Hit ")) child.gameObject.SetActive(false);
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(document); EditorUtility.SetDirty(settings); EditorUtility.SetDirty(rt);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Validate(controller);
        Debug.Log("Equipment guide installed: five slides, retake, and proceed to StarterScene. Navigation checks passed.");
        EquipmentGuidePreview.Open();
    }
    private static void RenderModel(string name, string path, string childName)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new InvalidOperationException("Missing model: " + path);
        var instance = UnityEngine.Object.Instantiate(asset);
        instance.hideFlags = HideFlags.HideAndDontSave;
        var source = childName == null ? instance.transform : instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == childName);
        if (source == null && childName == "Cover") source = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Cover");
        if (source == null) { UnityEngine.Object.DestroyImmediate(instance); throw new InvalidOperationException("Missing model part: " + childName); }
        var preview = new PreviewRenderUtility();
        try
        {
            var model = new GameObject("Equipment image") { hideFlags = HideFlags.HideAndDontSave };
            var bounds = new Bounds(); bool first = true;
            foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var part = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(model.transform);
                part.transform.position = renderer.transform.position; part.transform.rotation = renderer.transform.rotation; part.transform.localScale = renderer.transform.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                part.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                if (first) { bounds = part.GetComponent<Renderer>().bounds; first = false; } else bounds.Encapsulate(part.GetComponent<Renderer>().bounds);
            }
            if (first) { UnityEngine.Object.DestroyImmediate(model); throw new InvalidOperationException("Model has no meshes: " + path); }
            model.transform.position = -bounds.center;
            preview.AddSingleGO(model);
            float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
            preview.camera.orthographic = true; preview.camera.orthographicSize = radius * 1.1f;
            preview.camera.nearClipPlane = 0.001f; preview.camera.farClipPlane = radius * 20 + 10;
            preview.camera.transform.position = new Vector3(1, 0.8f, -1).normalized * radius * 4;
            preview.camera.transform.LookAt(Vector3.zero);
            preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(238f/255, 1, 242f/255, 1);
            preview.lights[0].intensity = 1.2f; preview.lights[0].transform.rotation = Quaternion.Euler(45, 30, 0);
            preview.lights[1].intensity = 0.8f; preview.ambientColor = Color.gray;
            preview.BeginStaticPreview(new Rect(0, 0, 800, 500)); preview.Render(true);
            var texture = preview.EndStaticPreview();
            File.WriteAllBytes(Folder + name + ".png", texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Folder + name + ".png", ImportAssetOptions.ForceSynchronousImport);
        }
        finally { preview.Cleanup(); UnityEngine.Object.DestroyImmediate(instance); }
    }
    private static void Validate(LabEquipmentGuideController controller)
    {
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "LabEquipmentGuide.uxml").CloneTree();
        controller.BindView(tree); controller.Retake(); controller.Previous();
        if (controller.CurrentSlideIndex != 0) throw new Exception("First-slide bound failed.");
        for (int i = 0; i < controller.SlideCount; i++)
        {
            if (controller.IsComplete || controller.CurrentSlideIndex != i || tree.Q<Label>("equipment-title").text != controller.Slides[i].title || tree.Q<Image>("equipment-image").image == null) throw new Exception("Slide validation failed: " + i);
            controller.Next();
        }
        if (!controller.IsComplete || tree.Q<VisualElement>("completion-view").ClassListContains("hidden")) throw new Exception("Completion screen failed.");
        controller.Next(); controller.Retake();
        if (controller.IsComplete || controller.CurrentSlideIndex != 0) throw new Exception("Retake failed.");
        controller.Next(); controller.Previous();
        if (controller.CurrentSlideIndex != 0) throw new Exception("Previous navigation failed.");
        controller.UnbindView();
        if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/StarterScene.unity")) throw new Exception("StarterScene is not enabled in build settings.");
    }
}

public sealed class EquipmentGuidePreview : EditorWindow
{
    private LabEquipmentGuideController controller;
    [MenuItem("Tools/PCR Equipment/Preview guide")]
    public static void Open()
    {
        var window = GetWindow<EquipmentGuidePreview>(); window.titleContent = new GUIContent("Equipment Guide Preview"); window.minSize = new Vector2(900, 650); window.Show();
    }
    public void CreateGUI()
    {
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LabEquipmentGuideEditor.Folder + "LabEquipmentGuide.uxml");
        if (tree == null) return;
        tree.CloneTree(rootVisualElement);
        controller = UnityEngine.Object.FindFirstObjectByType<LabEquipmentGuideController>();
        if (controller != null) { controller.BindView(rootVisualElement); controller.Retake(); }
    }
    private void OnDisable() { if (controller != null) controller.UnbindView(); }
}
