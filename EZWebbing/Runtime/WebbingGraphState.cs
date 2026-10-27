// WebbingGraphState.cs
// Plain data structures used for serialization.
// All fields are public so that JsonUtility can serialize/deserialize them directly.

using System;
using System.Collections.Generic;

namespace EZWebbing
{
    [Serializable]
    public class WebbingNodeState
    {
        public string nodeId;
        public float   x;
        public float   y;
    }

    [Serializable]
    public class WebbingConnectionState
    {
        public string fromNodeId;
        public int    fromPinIndex;
        public string toNodeId;
        public int    toPinIndex;
    }

    [Serializable]
    public class WebbingGraphState
    {
        public List<WebbingNodeState>       nodes       = new();
        public List<WebbingConnectionState> connections = new();
    }
}
