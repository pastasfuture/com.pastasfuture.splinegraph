using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace Pastasfuture.SplineGraph.Runtime
{
    [CreateAssetMenu(fileName = "SplineGraphBinaryData.asset", menuName = "Spline Graph/SplineGraphBinaryData")]
    [PreferBinarySerialization]
    public class SplineGraphBinaryDataScriptableObject : ScriptableObject
    {
        public DirectedGraphSerializable splineGraphSerializable;
        public SplineGraphPayloadSerializable splineGraphPayloadSerializable;

    }
}

