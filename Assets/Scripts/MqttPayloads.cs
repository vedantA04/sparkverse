using System;

[Serializable]
public class TwistPayload {
    public float linear;
    public float angular;
}

[Serializable]
public class PosePayload {
    public float x;
    public float y;
    public float theta;
}