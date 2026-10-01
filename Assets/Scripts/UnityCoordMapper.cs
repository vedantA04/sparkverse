using UnityEngine;

/// <summary>
/// Unity's 3D coordinates use x,z as the ground plane, and
/// y as the vertical axis. This class centralises mapping
/// between Unity's coordinates and 'external' coordinates,
/// which use x,y as the ground plane and z as the vertical
/// axis.
/// </summary>
public static class UnityCoordMapper
{
    public static (float x, float y) ToExternalXy(Vector3 pos)
    {
        return (pos.x, pos.z);
    }

    public static Vector2 ToExternalVec2D(Vector3 pos)
    {
        return new Vector2(pos.x, pos.z);
    }

    public static Vector3 ToUnity3D(float x, float y, float z=0f)
    {
        return new Vector3(x, z, y);
    }
}
