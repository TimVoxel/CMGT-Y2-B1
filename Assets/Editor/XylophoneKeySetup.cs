using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

// Turns the xylophone's lane bars into the hit zones and the lane buttons into plain markers, then rebuilds the replay scene.
// Safe to run again. Works from the menu or headless:
// Unity.exe -batchmode -quit -projectPath <project> -executeMethod XylophoneKeySetup.Run
public static class XylophoneKeySetup
{
    private const string ScenePath = "Assets/Scenes/Concert.unity";
    private const string OldButtonPrefabPath = "Assets/Prefabs/Lane Hit button.prefab";
    private const string MarkerPrefabPath = "Assets/Prefabs/Lane Marker.prefab";
    private const string ParticlesPrefabPath = "Assets/Prefabs/Xylophone Key Particles.prefab";
    private const string SloppyParticlesName = "particles (sloppy)";
    private const string PerfectParticlesName = "particles (perfect)";
    private const float HitHeightAboveBar = 0.12f;
    private static readonly Color MistakeFlashColor = new Color(0.5471698f, 0.0f, 0.14879175f, 1.0f);

    [MenuItem("Tools/Xylophone/Set Up Key Hit Zones")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        ConvertButtonPrefab();
        var markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarkerPrefabPath);
        var particlesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ParticlesPrefabPath);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var trackPlayer = Object.FindFirstObjectByType<TrackPlayer>();
        var stand = Object.FindFirstObjectByType<XylophoneStand>();
        var laneBars = ReadLaneBars(stand);

        var zones = laneBars.Select((bar, lane) => SetUpKey(bar, lane, trackPlayer, particlesPrefab)).ToArray();

        var serializedPlayer = new SerializedObject(trackPlayer);
        serializedPlayer.FindProperty("_laneMarkerPrefab").objectReferenceValue = markerPrefab;
        var zonesProperty = serializedPlayer.FindProperty("_laneHitZones");
        zonesProperty.arraySize = zones.Length;
        for (var i = 0; i < zones.Length; i++)
        {
            zonesProperty.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
        }
        serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Xylophone keys set up: {zones.Length} lanes in {ScenePath}");

        ReplaySceneBuilder.Build();
    }

    // The old button prefab held the hit logic, effects and particles. The particles get a prefab of their own and
    // what's left becomes a plain marker sphere.
    private static void ConvertButtonPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(OldButtonPrefabPath) == null)
        {
            return;
        }

        var contents = PrefabUtility.LoadPrefabContents(OldButtonPrefabPath);
        var particlesRoot = new GameObject("Xylophone Key Particles");
        foreach (var particles in contents.GetComponentsInChildren<ParticleSystem>(true).Where(system => system.transform.parent == contents.transform))
        {
            Object.Instantiate(particles.gameObject, particlesRoot.transform).name = particles.name;
            Object.DestroyImmediate(particles.gameObject);
        }
        PrefabUtility.SaveAsPrefabAsset(particlesRoot, ParticlesPrefabPath);
        Object.DestroyImmediate(particlesRoot);

        DestroyAll<LaneHitZone>(contents);
        DestroyAll<ScalePunch>(contents);
        DestroyAll<ColorFlash>(contents);
        DestroyAll<BoxCollider>(contents);
        contents.name = "Lane Marker";
        PrefabUtility.SaveAsPrefabAsset(contents, OldButtonPrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);

        var error = AssetDatabase.RenameAsset(OldButtonPrefabPath, "Lane Marker");
        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static void DestroyAll<T>(GameObject root) where T : Component
    {
        foreach (var component in root.GetComponentsInChildren<T>(true))
        {
            Object.DestroyImmediate(component);
        }
    }

    private static Transform[] ReadLaneBars(XylophoneStand stand)
    {
        var property = new SerializedObject(stand).FindProperty("_laneBars");
        return Enumerable.Range(0, property.arraySize)
            .Select(i => (Transform)property.GetArrayElementAtIndex(i).objectReferenceValue)
            .ToArray();
    }

    private static LaneHitZone SetUpKey(Transform bar, int lane, TrackPlayer trackPlayer, GameObject particlesPrefab)
    {
        var barRenderer = bar.GetComponent<MeshRenderer>();
        var bounds = barRenderer.bounds;

        var collider = GetOrAdd<BoxCollider>(bar);
        collider.isTrigger = true;
        var worldMin = bounds.min;
        var worldMax = bounds.max + Vector3.up * HitHeightAboveBar;
        collider.center = bar.InverseTransformPoint((worldMin + worldMax) * 0.5f);
        collider.size = Abs(bar.InverseTransformVector(worldMax - worldMin));

        var punch = GetOrAdd<ScalePunch>(bar);
        Set(punch, "_amount", property => property.floatValue = 0.08f);
        Set(punch, "_shrinkAmount", property => property.floatValue = 0.06f);

        var flash = GetOrAdd<ColorFlash>(bar);
        Set(flash, "_renderer", property => property.objectReferenceValue = barRenderer);
        Set(flash, "_flashColor", property => property.colorValue = MistakeFlashColor);
        Set(flash, "_duration", property => property.floatValue = 0.2f);

        // Rocks the bar around the keyboard's length, so its ends dip towards and away from the player
        var wobble = GetOrAdd<RotationWobble>(bar);
        Set(wobble, "_localAxis", property => property.vector3Value = Vector3.up);

        var particles = bar.Find(particlesPrefab.name)
                        ?? ((GameObject)PrefabUtility.InstantiatePrefab(particlesPrefab, bar)).transform;
        particles.localPosition = bar.InverseTransformPoint(bounds.center + Vector3.up * bounds.extents.y);
        particles.localRotation = Quaternion.identity;
        var sloppy = particles.Find(SloppyParticlesName).GetComponent<ParticleSystem>();
        var perfect = particles.Find(PerfectParticlesName).GetComponent<ParticleSystem>();

        var zone = GetOrAdd<LaneHitZone>(bar);
        Set(zone, "_trackPlayer", property => property.objectReferenceValue = trackPlayer);
        Set(zone, "_laneIndex", property => property.intValue = lane);
        Set(zone, "_collider", property => property.objectReferenceValue = collider);
        Set(zone, "_minHitSpeed", property => property.floatValue = 0.1f);
        Set(zone, "_laneColoredParticles", property =>
        {
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).objectReferenceValue = sloppy.GetComponent<ParticleSystemRenderer>();
        });

        SetListeners(zone, "_onPressed", wobble.Wobble);
        SetListeners(zone, "_onNoteHit", punch.Punch, new UnityAction(sloppy.Play));
        SetListeners(zone, "_onPerfectHit", new UnityAction(perfect.Play));
        SetListeners(zone, "_onEmptyPress", punch.Shrink, flash.Flash);
        SetListeners(zone, "_onMissed", punch.Shrink, flash.Flash);
        EditorUtility.SetDirty(zone);
        return zone;
    }

    private static T GetOrAdd<T>(Transform target) where T : Component
    {
        return target.TryGetComponent<T>(out var existing) ? existing : target.gameObject.AddComponent<T>();
    }

    private static Vector3 Abs(Vector3 vector) => new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));

    private static void Set(Object target, string propertyName, Action<SerializedProperty> assign)
    {
        var serialized = new SerializedObject(target);
        assign(serialized.FindProperty(propertyName) ?? throw new MissingMemberException(target.GetType().Name, propertyName));
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetListeners(LaneHitZone zone, string eventName, params UnityAction[] listeners)
    {
        var field = typeof(LaneHitZone).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new MissingMemberException(nameof(LaneHitZone), eventName);
        var unityEvent = (UnityEvent)field.GetValue(zone);
        while (unityEvent.GetPersistentEventCount() > 0)
        {
            UnityEventTools.RemovePersistentListener(unityEvent, 0);
        }
        foreach (var listener in listeners)
        {
            UnityEventTools.AddPersistentListener(unityEvent, listener);
        }
    }
}
