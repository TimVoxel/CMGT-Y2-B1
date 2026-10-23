using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Drops the gameplay XR rig onto the stage surface under it, together with the things placed for the player's height
// (menus and grabbables), so they keep the same height relative to the player. Then rebuilds the replay scene.
// Works from the menu or headless:
// Unity.exe -batchmode -quit -projectPath <project> -executeMethod PlayerRigGrounder.Run
public static class PlayerRigGrounder
{
    private const string ScenePath = "Assets/Scenes/Concert.unity";
    private const float Tolerance = 0.005f;

    [MenuItem("Tools/Stage/Ground Player Rig")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var origin = Object.FindFirstObjectByType<XROrigin>();
        var rig = origin.transform;

        var ground = Physics.RaycastAll(rig.position + Vector3.up * 0.5f, Vector3.down, 20.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            .Where(hit => !hit.collider.transform.IsChildOf(rig))
            .OrderBy(hit => hit.distance)
            .Select(hit => (RaycastHit?)hit)
            .FirstOrDefault();
        if (ground == null)
        {
            Debug.LogError("No ground under the player rig");
            return;
        }

        var drop = rig.position.y - ground.Value.point.y;
        if (Mathf.Abs(drop) < Tolerance)
        {
            Debug.Log($"Player rig already stands on {ground.Value.collider.name}");
            return;
        }

        foreach (var target in PlayerRelativeObjects(rig))
        {
            target.position += Vector3.down * drop;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            Debug.Log($"Lowered {target.name} by {drop:0.###} to y {target.position.y:0.###}");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        ReplaySceneBuilder.Build();
    }

    private static IEnumerable<Transform> PlayerRelativeObjects(Transform rig)
    {
        yield return rig;
        yield return Object.FindFirstObjectByType<ConcertSet>().transform;
        yield return Object.FindFirstObjectByType<DebugTrackMenu>().transform;
        foreach (var grabbable in Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None))
        {
            if (!grabbable.transform.IsChildOf(rig))
            {
                yield return grabbable.transform;
            }
        }
    }
}
