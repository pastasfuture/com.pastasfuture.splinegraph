using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using Pastasfuture.SplineGraph.Runtime;

namespace Pastasfuture.SplineGraph.Editor
{
    public partial class SplineGraphComponentEditor : UnityEditor.Editor
    {
        private class ScaleTool
        {
            public bool isFoldoutEnabled = false;

            public float3 origin = float3.zero;
            public float3 scale = new float3(1.0f, 1.0f, 1.0f);
        }

        private ScaleTool scaleTool = new ScaleTool();

        public void OnInspectorGUIScaleTool(SplineGraphComponent sgc)
        {
            EditorGUILayout.BeginVertical();

            if (!scaleTool.isFoldoutEnabled && GUILayout.Button("Scale Tool"))
            {
                scaleTool.isFoldoutEnabled = true;
            }
            if (scaleTool.isFoldoutEnabled)
            {
                scaleTool.isFoldoutEnabled = EditorGUILayout.BeginFoldoutHeaderGroup(scaleTool.isFoldoutEnabled, "Scale Tool");
                if (scaleTool.isFoldoutEnabled)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Origin", GUILayout.Width(100));
                    scaleTool.origin = EditorGUILayout.Vector3Field("", scaleTool.origin);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Scale", GUILayout.Width(100));
                    scaleTool.scale = EditorGUILayout.Vector3Field("", scaleTool.scale);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    if (selectedIndices.Count > 0 && GUILayout.Button("Apply") && math.lengthsq(scaleTool.scale) > 1e-5f)
                    {
                        scaleTool.isFoldoutEnabled = false;
                        sgc.UndoRecord("Spline Graph Scale Tool", true);

                        for (int i = 0; i < selectedIndices.Count; ++i)
                        {
                            Int16 v = selectedIndices[i];

                            float3 positionOS = sgc.splineGraph.payload.positions.data[v];

                            sgc.splineGraph.payload.positions.data[v] = (positionOS - scaleTool.origin) * scaleTool.scale + scaleTool.origin;

                            quaternion rotation = sgc.splineGraph.payload.rotations.data[v];
                            float3 forward = math.mul(rotation, new float3(0.0f, 0.0f, 1.0f));
                            float velocityScale = math.abs(math.dot(forward, scaleTool.scale));
                            sgc.splineGraph.payload.scales.data[v] = sgc.splineGraph.payload.scales.data[v] * velocityScale;

                        }

                        for (int i = 0, iLen = selectedIndices.Count; i < iLen; ++i)
                        {
                            sgc.splineGraph.VertexComputePayloads(selectedIndices[i]);
                        }

                    }
                    else if (GUILayout.Button("Cancel"))
                    {
                        scaleTool.isFoldoutEnabled = false;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
