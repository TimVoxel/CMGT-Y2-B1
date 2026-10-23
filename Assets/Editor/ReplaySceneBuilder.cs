using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Object = UnityEngine.Object;

// Builds the replay scene from the gameplay scene: live-only systems out, replay components in.
// Works from the menu or headless:
// Unity.exe -batchmode -quit -projectPath <project> -executeMethod ReplaySceneBuilder.Build
public static class ReplaySceneBuilder
{
    public const string ReplayScenePath = "Assets/Scenes/ReplayScene.unity";
    private const string SourceScenePath = "Assets/Scenes/Concert.unity";
    private const string ControllerModelPath = "Assets/Samples/XR Interaction Toolkit/3.0.11/Starter Assets/Models/UniversalController.fbx";
    private const string SpectatorRigPath = "Assets/Samples/XR Interaction Toolkit/3.0.11/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    private const string TeleportInteractionLayer = "Teleport";
    private static readonly string[] TeleportSurfacePaths = { "/unity - concert stage graybox colors 4fbx/Cube", "/Cylinder", "/floor test" };
    private const float ViewerStepBack = 1.5f;
    private static readonly Vector3 ControllerModelOffset = new Vector3(0.0f, 0.0f, -0.05f);
    private static readonly Quaternion ControllerModelRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);

    [MenuItem("Tools/Recording/Build Replay Scene")]
    public static void Build()
    {
        var exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ReplayScenePath) != null;
        if (exists && !Application.isBatchMode
            && !EditorUtility.DisplayDialog("Build Replay Scene", $"Rebuild {ReplayScenePath} from {SourceScenePath}? Changes made to it by hand are lost.", "Rebuild", "Cancel"))
        {
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        if (exists)
        {
            // Copying over the file keeps its .meta, so the GUID that build settings point at stays valid
            File.Copy(SourceScenePath, ReplayScenePath, true);
            AssetDatabase.ImportAsset(ReplayScenePath, ImportAssetOptions.ForceUpdate);
        }
        else if (!AssetDatabase.CopyAsset(SourceScenePath, ReplayScenePath))
        {
            Fail($"Couldn't copy {SourceScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ReplayScenePath, OpenSceneMode.Single);
        var trackPlayer = Find<TrackPlayer>();
        var audience = Find<AudienceSpawner>();
        var engagement = Find<EngagementHolder>();
        var cheer = Find<CrowdCheer>();
        var moshPit = Find<MoshPit>();
        var sceneTransition = Find<SceneTransition>();
        var concertSet = Find<ConcertSet>();
        var writer = Find<RecordingSessionWriter>();

        var recordingsSubPath = new SerializedObject(writer).FindProperty("_outputSubPath").stringValue;
        var setList = ReadObjects<Track>(concertSet, "_setList");
        var panelPosition = concertSet.transform.position;
        var panelRotation = concertSet.transform.rotation;

        RemoveLiveOnlySystems();

        Set(trackPlayer, "_playOnStart", property => property.boolValue = false);
        Set(audience, "_spawnOnStart", property => property.boolValue = false);
        Set(cheer, "_cheersOnItsOwn", property => property.boolValue = false);
        Set(moshPit, "_startsOnItsOwn", property => property.boolValue = false);
        Set(engagement, "_decayPerSecond", property => property.floatValue = 0.0f);

        var replay = new GameObject("Replay");
        var dispatcher = replay.AddComponent<ReplayEventDispatcher>();
        Set(dispatcher, "_recordingsSubPath", property => property.stringValue = recordingsSubPath);

        var song = replay.AddComponent<ReplaySong>();
        SetReference(song, "_dispatcher", dispatcher);
        SetReference(song, "_trackPlayer", trackPlayer);
        Set(song, "_tracks", property =>
        {
            property.arraySize = setList.Length;
            for (var i = 0; i < setList.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = setList[i];
            }
        });

        var crowd = replay.AddComponent<ReplayCrowd>();
        SetReference(crowd, "_dispatcher", dispatcher);
        SetReference(crowd, "_audience", audience);
        SetReference(crowd, "_engagement", engagement);
        SetReference(crowd, "_cheer", cheer);
        SetReference(crowd, "_moshPit", moshPit);

        var avatar = replay.AddComponent<ReplayAvatar>();
        SetReference(avatar, "_dispatcher", dispatcher);
        SetReference(avatar, "_head", CreateHeadVisual(replay.transform));
        SetReference(avatar, "_leftHand", CreateHandVisual(replay.transform, "Recorded Left Hand"));
        SetReference(avatar, "_rightHand", CreateHandVisual(replay.transform, "Recorded Right Hand"));

        var controls = new GameObject("Replay Controls");
        controls.transform.SetPositionAndRotation(panelPosition, panelRotation);
        var panel = controls.AddComponent<ReplayControlPanel>();
        SetReference(panel, "_dispatcher", dispatcher);
        SetReference(panel, "_sceneTransition", sceneTransition);

        ReplaceWithSpectatorRig();
        AddTeleportSurfaces();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings();
        Debug.Log($"Replay scene built: {ReplayScenePath} ({setList.Length} tracks, recordings from '{recordingsSubPath}')");
    }

    private static void RemoveLiveOnlySystems()
    {
        DestroyGameObjects<ConcertRecorder>();
        DestroyGameObjects<RecordingSessionWriter>();
        DestroyGameObjects<ConcertSet>();
        DestroyGameObjects<ConcertRevenue>();
        DestroyGameObjects<DebugTrackMenu>();
        DestroyGameObjects<LaneHitter>();
        DestroyComponents<AudienceGrowth>();
        DestroyComponents<NoteEngagementDriver>();
        DestroyComponents<LaneOffsetCalibrator>();
    }

    // The gameplay rig has locomotion stripped out, the spectator gets a stock rig that can move, turn and teleport
    private static void ReplaceWithSpectatorRig()
    {
        var oldOrigin = Find<XROrigin>();
        var oldRoot = oldOrigin.transform;
        var back = Vector3.ProjectOnPlane(-oldRoot.forward, Vector3.up).normalized;
        var position = oldRoot.position + back * ViewerStepBack;
        var rotation = oldRoot.rotation;
        var trackingOriginMode = oldOrigin.RequestedTrackingOriginMode;
        var clearFlags = oldOrigin.Camera.clearFlags;
        var backgroundColor = oldOrigin.Camera.backgroundColor;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpectatorRigPath)
                     ?? throw new InvalidOperationException($"Missing {SpectatorRigPath}");
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, oldOrigin.gameObject.scene);
        RemapReferences(oldRoot, rig.transform);
        Object.DestroyImmediate(oldOrigin.gameObject);

        if (Physics.Raycast(position + Vector3.up, Vector3.down, out var ground, 10.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            position.y = ground.point.y;
        }
        rig.transform.SetPositionAndRotation(position, rotation);

        var origin = rig.GetComponent<XROrigin>();
        origin.RequestedTrackingOriginMode = trackingOriginMode;
        origin.Camera.clearFlags = clearFlags;
        origin.Camera.backgroundColor = backgroundColor;
    }

    private static void RemapReferences(Transform oldRoot, Transform newRoot)
    {
        var counterparts = new Dictionary<Object, Object>();
        foreach (var part in newRoot.GetComponentsInChildren<Transform>(true))
        {
            foreach (var instance in part.GetComponents<Component>().Append<Object>(part.gameObject))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance);
                if (source != null)
                {
                    counterparts[source] = instance;
                }
            }
        }

        foreach (var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (component == null || component.transform.IsChildOf(oldRoot) || component.transform.IsChildOf(newRoot))
            {
                continue;
            }

            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || !IsInside(property.objectReferenceValue, oldRoot))
                {
                    continue;
                }

                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(property.objectReferenceValue);
                var counterpart = source != null && counterparts.TryGetValue(source, out var found) ? found : null;
                if (counterpart == null)
                {
                    Debug.LogWarning($"{component.GetType().Name}.{property.propertyPath} on {component.name} pointed at the gameplay rig and has no counterpart on the spectator rig");
                }
                property.objectReferenceValue = counterpart;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static bool IsInside(Object target, Transform root)
    {
        var transform = target switch
        {
            Component component => component.transform,
            GameObject gameObject => gameObject.transform,
            _ => null,
        };
        return transform != null && transform.IsChildOf(root);
    }

    private static void AddTeleportSurfaces()
    {
        var teleportLayers = InteractionLayerMask.GetMask(TeleportInteractionLayer);
        foreach (var path in TeleportSurfacePaths)
        {
            var surface = GameObject.Find(path) ?? throw new InvalidOperationException($"{SourceScenePath} has no teleport surface '{path}'");
            surface.AddComponent<TeleportationArea>().interactionLayers = teleportLayers;
        }

        // Their solid colliders would stop the teleport arc mid-air
        foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (collider.name.StartsWith("Laser"))
            {
                collider.isTrigger = true;
            }
        }
    }

    private static Transform CreateHeadVisual(Transform parent)
    {
        var head = CreatePrimitive(PrimitiveType.Sphere, "Recorded Head", parent);
        head.localScale = Vector3.one * 0.2f;

        var visor = CreatePrimitive(PrimitiveType.Cube, "Visor", head);
        visor.localPosition = new Vector3(0.0f, 0.05f, 0.4f);
        visor.localScale = new Vector3(0.8f, 0.35f, 0.3f);
        return head;
    }

    private static Transform CreateHandVisual(Transform parent, string name)
    {
        var hand = new GameObject(name).transform;
        hand.SetParent(parent, false);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ControllerModelPath);
        if (model != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, hand);
            instance.transform.SetLocalPositionAndRotation(ControllerModelOffset, ControllerModelRotation);
        }
        else
        {
            CreatePrimitive(PrimitiveType.Cube, "Controller", hand).localScale = Vector3.one * 0.06f;
        }
        return hand;
    }

    private static Transform CreatePrimitive(PrimitiveType type, string name, Transform parent)
    {
        var primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        Object.DestroyImmediate(primitive.GetComponent<Collider>());
        primitive.transform.SetParent(parent, false);
        return primitive.transform;
    }

    private static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(existing => existing.path != ReplayScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ReplayScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static T Find<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include)
               ?? throw new InvalidOperationException($"{SourceScenePath} has no {typeof(T).Name}");
    }

    private static void DestroyGameObjects<T>() where T : Component
    {
        foreach (var component in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (component != null)
            {
                Object.DestroyImmediate(component.gameObject);
            }
        }
    }

    private static void DestroyComponents<T>() where T : Component
    {
        foreach (var component in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(component);
        }
    }

    private static T[] ReadObjects<T>(Object source, string propertyName) where T : Object
    {
        var property = FindProperty(new SerializedObject(source), propertyName);
        return Enumerable.Range(0, property.arraySize)
            .Select(i => property.GetArrayElementAtIndex(i).objectReferenceValue as T)
            .Where(value => value != null)
            .ToArray();
    }

    private static void SetReference(Object target, string propertyName, Object value)
    {
        Set(target, propertyName, property => property.objectReferenceValue = value);
    }

    private static void Set(Object target, string propertyName, Action<SerializedProperty> assign)
    {
        var serialized = new SerializedObject(target);
        assign(FindProperty(serialized, propertyName));
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty FindProperty(SerializedObject serialized, string propertyName)
    {
        return serialized.FindProperty(propertyName)
               ?? throw new MissingMemberException(serialized.targetObject.GetType().Name, propertyName);
    }

    private static void Fail(string message)
    {
        Debug.LogError("Replay scene build failed: " + message);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }
}
