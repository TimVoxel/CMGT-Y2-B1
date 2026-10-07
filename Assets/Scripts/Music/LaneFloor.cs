using UnityEngine;

// Translucent checkerboard plane under the lane hit buttons, to help judge depth.
// Builds its own quad so one checker column lines up with each lane, and follows the TrackPlayer
// when calibration moves or widens the lanes.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LaneFloor : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;

    // How far below the button centres the plane sits (buttons are 0.2 spheres)
    [SerializeField] private float _dropBelowButtons = 0.15f;
    [SerializeField, Min(1)] private int _squaresPerLane = 1;
    // Extra squares left/right of the outer lanes
    [SerializeField, Min(0)] private int _sideMarginSquares = 1;
    // Rows on the side the notes come from, and on the player's side of the hit line
    [SerializeField, Min(0)] private int _rowsBeforeHitLine = 3;
    [SerializeField, Min(0)] private int _rowsAfterHitLine = 1;

    private Mesh _mesh;
    private float _builtSpacing = float.NaN;
    private int _builtLaneCount = -1;

    private void Awake()
    {
        _mesh = new Mesh { name = "LaneFloor" };
        _mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = _mesh;
    }

    private void OnDestroy()
    {
        Destroy(_mesh);
    }

    private void LateUpdate()
    {
        var rotation = _trackPlayer.LaneRotation;
        transform.SetPositionAndRotation(_trackPlayer.HitLineCenter - rotation * Vector3.up * _dropBelowButtons, rotation);
        transform.localScale = Vector3.one;

        if (_trackPlayer.LaneSpacing != _builtSpacing || _trackPlayer.LaneCount != _builtLaneCount)
        {
            RebuildMesh(_trackPlayer.LaneSpacing, _trackPlayer.LaneCount);
        }
    }

    private void RebuildMesh(float spacing, int laneCount)
    {
        _builtSpacing = spacing;
        _builtLaneCount = laneCount;

        var square = Mathf.Max(0.01f, Mathf.Abs(spacing)) / _squaresPerLane;
        var lanesHalfWidth = laneCount * square * _squaresPerLane * 0.5f;
        var x0 = -lanesHalfWidth - _sideMarginSquares * square;
        var x1 = lanesHalfWidth + _sideMarginSquares * square;
        // Hit line runs through the middle of a row
        var z0 = -(_rowsBeforeHitLine + 0.5f) * square;
        var z1 = (_rowsAfterHitLine + 0.5f) * square;

        // Texture holds 2x2 squares. Offsets put lane edges on square edges and the hit line mid-row.
        Vector2 Uv(float x, float z) => new Vector2((x + lanesHalfWidth) / (2.0f * square), (z + 0.5f * square) / (2.0f * square));

        _mesh.Clear();
        _mesh.vertices = new[]
        {
            new Vector3(x0, 0.0f, z0), new Vector3(x0, 0.0f, z1),
            new Vector3(x1, 0.0f, z1), new Vector3(x1, 0.0f, z0),
        };
        _mesh.uv = new[] { Uv(x0, z0), Uv(x0, z1), Uv(x1, z1), Uv(x1, z0) };
        _mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        _mesh.RecalculateBounds();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _trackPlayer = FindFirstObjectByType<TrackPlayer>();
    }
#endif
}
