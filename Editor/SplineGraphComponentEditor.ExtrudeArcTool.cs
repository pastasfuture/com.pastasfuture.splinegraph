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
        private class ExtrudeArcTool
        {
            public bool isFoldoutEnabled = false;

            public enum ArcDirection
            {
                Left = 0,
                Right,
                Up,
                Down
            }
            public ArcDirection arcDirection = ArcDirection.Left;

            public float radius;
            public float angleDegrees;
            public float height;

            public List<Int16> vertexIndicesScratch = new List<Int16>();
            public List<Int16> selectedVerticesNext = new List<Int16>();
        }

        private ExtrudeArcTool extrudeArcTool = new ExtrudeArcTool();

        public void OnInspectorGUIExtrudeArcTool(SplineGraphComponent sgc)
        {
            EditorGUILayout.BeginVertical();

            if (!extrudeArcTool.isFoldoutEnabled && GUILayout.Button("Extrude Arc Tool"))
            {
                extrudeArcTool.isFoldoutEnabled = true;
            }
            if (extrudeArcTool.isFoldoutEnabled)
            {
                extrudeArcTool.isFoldoutEnabled = EditorGUILayout.BeginFoldoutHeaderGroup(extrudeArcTool.isFoldoutEnabled, "Extrude Arc Tool");
                if (extrudeArcTool.isFoldoutEnabled)
                {
                    EditorGUILayout.BeginHorizontal();
                    extrudeArcTool.arcDirection = (ExtrudeArcTool.ArcDirection)EditorGUILayout.EnumPopup(extrudeArcTool.arcDirection);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Radius", GUILayout.Width(100));
                    extrudeArcTool.radius = EditorGUILayout.FloatField("", extrudeArcTool.radius);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Angle", GUILayout.Width(100));
                    extrudeArcTool.angleDegrees = EditorGUILayout.FloatField("", extrudeArcTool.angleDegrees);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Height", GUILayout.Width(100));
                    extrudeArcTool.height = EditorGUILayout.FloatField("", extrudeArcTool.height);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    if (selectedIndices.Count > 0 && GUILayout.Button("Apply") && math.abs(extrudeArcTool.angleDegrees) > 1e-5f)
                    {
                        extrudeArcTool.isFoldoutEnabled = false;
                        sgc.UndoRecord("Spline Graph Extrude Arc Tool", true);

                        extrudeArcTool.vertexIndicesScratch.Clear();
                        extrudeArcTool.selectedVerticesNext.Clear();
                        {

                            for (int i = 0; i < selectedIndices.Count; ++i)
                            {
                                Int16 v = selectedIndices[i];

                                quaternion arcRotation = sgc.splineGraph.payload.rotations.data[v];
                                float2 arcLeash = sgc.splineGraph.payload.leashes.data[v];
                                float3 arcOrigin = sgc.splineGraph.payload.positions.data[v];

                                float2 scaleCurrent = sgc.splineGraph.payload.scales.data[v];

                                sgc.splineGraph.payload.scales.data[v] = new float2(scaleCurrent.x, 0.875f * extrudeArcTool.radius * 2.0f);
                                extrudeArcTool.vertexIndicesScratch.Add(v);

                                Int16 vertexIndexPrevious = v;
                                for (int j = 0, jLen = (int)math.ceil(math.abs(extrudeArcTool.angleDegrees) / 90.0f); j < jLen; ++j)
                                {
                                    float progressFractional = (math.abs(extrudeArcTool.angleDegrees) / 90.0f) - (float)j;
                                    progressFractional = ((j + 1) == jLen) ? progressFractional : 1.0f;
                                    float progressNormalized = ((float)j + progressFractional) / (math.abs(extrudeArcTool.angleDegrees) / 90.0f);
                                    float heightOffsetOS = progressNormalized * extrudeArcTool.height;
                                    float angle = progressNormalized * math.radians(extrudeArcTool.angleDegrees);

                                    float3 arcOriginOffsetOS = new float3(extrudeArcTool.radius * math.cos(angle), heightOffsetOS, extrudeArcTool.radius * math.sin(angle));

                                    // Pitch angle derived by unrolling the circumference of the helix:
                                    // https://forums.autodesk.com/t5/autocad-forum/using-helix-command-at-a-certain-angle/td-p/5437524
                                    float pitchAngleOS = math.tan(extrudeArcTool.height / (2.0f * math.PI * extrudeArcTool.radius * extrudeArcTool.angleDegrees / 360.0f));

                                    arcOriginOffsetOS.x -= extrudeArcTool.radius;

                                    float3 yawAxis = new float3(0.0f, 1.0f, 0.0f);
                                    float3 pitchAxis = new float3(1.0f, 0.0f, 0.0f);

                                    if (extrudeArcTool.arcDirection == ExtrudeArcTool.ArcDirection.Right)
                                    {
                                        yawAxis = -yawAxis;
                                        arcOriginOffsetOS.x *= -1.0f;
                                    }
                                    else if (extrudeArcTool.arcDirection == ExtrudeArcTool.ArcDirection.Down)
                                    {
                                        pitchAxis = new float3(0.0f, -1.0f, 0.0f);
                                        yawAxis = new float3(-1.0f, 0.0f, 0.0f);
                                        arcOriginOffsetOS = new float3(arcOriginOffsetOS.y, arcOriginOffsetOS.x, arcOriginOffsetOS.z);
                                    }
                                    else if (extrudeArcTool.arcDirection == ExtrudeArcTool.ArcDirection.Up)
                                    {
                                        pitchAxis = new float3(0.0f, -1.0f, 0.0f);
                                        yawAxis = new float3(1.0f, 0.0f, 0.0f);
                                        arcOriginOffsetOS = new float3(arcOriginOffsetOS.y, -arcOriginOffsetOS.x, arcOriginOffsetOS.z);
                                    }

                                    float3 positionOS = math.mul(arcRotation, arcOriginOffsetOS) + arcOrigin;

                                    quaternion rotationOS = arcRotation * Quaternion.AngleAxis(Mathf.Rad2Deg * -angle, yawAxis) * Quaternion.AngleAxis(Mathf.Rad2Deg * -pitchAngleOS, pitchAxis);

                                    // Scale to get close to a circle determined experimentally.
                                    float2 scaleOS = new float2(0.875f, 0.875f) * extrudeArcTool.radius * 2.0f * progressFractional;

                                    if ((j + 1) == jLen)
                                    {
                                        // Fix up the scale of our previous one.
                                        sgc.splineGraph.payload.scales.data[vertexIndexPrevious] = new float2(sgc.splineGraph.payload.scales.data[vertexIndexPrevious].x, scaleOS.x);
                                    }

                                    Int16 vertexIndexNext = sgc.VertexAdd(positionOS, rotationOS, scaleOS, arcLeash);
                                    sgc.EdgeAdd(vertexIndexPrevious, vertexIndexNext);
                                    extrudeArcTool.vertexIndicesScratch.Add(vertexIndexNext);
                                    vertexIndexPrevious = vertexIndexNext;

                                    if ((j + 1) == jLen)
                                    {
                                        extrudeArcTool.selectedVerticesNext.Add(vertexIndexNext);
                                    }


                                }
                            }

                            for (int i = 0, iLen = extrudeArcTool.vertexIndicesScratch.Count; i < iLen; ++i)
                            {
                                sgc.splineGraph.VertexComputePayloads(extrudeArcTool.vertexIndicesScratch[i]);
                            }
                            selectedIndices.Clear();
                            for (int i = 0, iLen = extrudeArcTool.selectedVerticesNext.Count; i < iLen; ++i)
                            {
                                selectedIndices.Add(extrudeArcTool.selectedVerticesNext[i]);
                            }
                            extrudeArcTool.vertexIndicesScratch.Clear();
                            extrudeArcTool.selectedVerticesNext.Clear();
                        }
                    }
                    else if (GUILayout.Button("Cancel"))
                    {
                        extrudeArcTool.isFoldoutEnabled = false;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
