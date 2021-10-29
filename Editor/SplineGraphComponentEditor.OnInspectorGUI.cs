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
        private int userBlobDebugDisplayMode = 0;
        private int[] userBlobDebugDisplayModeValues = null;

        private bool TryOnInspectorGUIIsEditingEnabledTool(SplineGraphComponent sgc)
        {
            bool isEditingEnabledNew = EditorGUILayout.Toggle("Is Editing Enabled", sgc.isEditingEnabled);
            if (isEditingEnabledNew != sgc.isEditingEnabled)
            {
                sgc.UndoRecord("Toggled SplineGraph.isEditingEnabled");
                sgc.isEditingEnabled = isEditingEnabledNew;
            }
            return sgc.isEditingEnabled;
        }

        private void OnInspectorGUIUserBlobSchemaTool(SplineGraphComponent sgc)
        {
            var splineGraphUserBlobSchemaNext = EditorGUILayout.ObjectField("User Blob Schema", sgc.splineGraphUserBlobSchema, typeof(SplineGraphUserBlobSchemaScriptableObject), false) as SplineGraphUserBlobSchemaScriptableObject;
            if (splineGraphUserBlobSchemaNext != sgc.splineGraphUserBlobSchema)
            {
                sgc.UndoRecord("Edited Spline Graph User Blob Schema");
                sgc.splineGraphUserBlobSchema = splineGraphUserBlobSchemaNext;

                if (splineGraphUserBlobSchemaNext != null)
                {
                    NativeArray<SplineGraphUserBlob.Scheme> schemaVertex = default;
                    splineGraphUserBlobSchemaNext.CopyVertexSchema(ref schemaVertex, Allocator.Temp);
                    NativeArray<SplineGraphUserBlob.Scheme> schemaEdge = default;
                    splineGraphUserBlobSchemaNext.CopyEdgeSchema(ref schemaEdge, Allocator.Temp);
                    sgc.splineGraph.payload.SetSchema(ref schemaVertex, ref schemaEdge, splineGraphUserBlobSchemaNext.GetVersion(), Allocator.Persistent);
                    schemaVertex.Dispose();
                    schemaEdge.Dispose();
                }
            }
        }

        private void OnInspectorGUITypeTool(SplineGraphComponent sgc)
        {
            int typeNew = EditorGUILayout.DelayedIntField("Type", sgc.type);
            typeNew = math.clamp(typeNew, 0, int.MaxValue);
            if (typeNew != sgc.type)
            {
                sgc.UndoRecord("Edited Spline Graph Type");
                sgc.type = typeNew;
            }
        }

        private void OnInspectorGUIGizmoSplineSegmentCountTool(SplineGraphComponent sgc)
        {
            int gizmoSplineSegmentCountNew = EditorGUILayout.DelayedIntField("Gizmo Spline Segment Count", sgc.gizmoSplineSegmentCount);
            gizmoSplineSegmentCountNew = math.clamp(gizmoSplineSegmentCountNew, 1, int.MaxValue);
            if (gizmoSplineSegmentCountNew != sgc.gizmoSplineSegmentCount)
            {
                sgc.UndoRecord("Edited Spline Graph Spline Segment Count");
                sgc.gizmoSplineSegmentCount = gizmoSplineSegmentCountNew;
            }
        }

        private void OnInspectorGUIUserBlobDebugDisplayTool(SplineGraphComponent sgc)
        {
            if (sgc.splineGraph.payload.userBlobSchemaVersion == 0) { return; }
            string[] namesReadOnly = sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly();
            userBlobDebugDisplayMode = math.clamp(userBlobDebugDisplayMode, 0, namesReadOnly.Length - 1);
            if (userBlobDebugDisplayModeValues == null || userBlobDebugDisplayModeValues.Length != namesReadOnly.Length)
            {
                userBlobDebugDisplayModeValues = new int[namesReadOnly.Length];
                for (int i = 0; i < namesReadOnly.Length; ++i)
                {
                    userBlobDebugDisplayModeValues[i] = i;
                }
            }
            userBlobDebugDisplayMode = EditorGUILayout.IntPopup("User Blob Debug Display Mode", userBlobDebugDisplayMode, namesReadOnly, userBlobDebugDisplayModeValues);
        }

        private void OnInspectorGUIAddVertexTool(SplineGraphComponent sgc)
        {
            if (GUILayout.Button("Add Vertex"))
            {
                sgc.UndoRecord("Spline Graph Vertex Add", true);

                float3 position = new float3(0.0f, 0.0f, 0.0f);
                quaternion rotation = quaternion.identity;
                float2 scale = new float2(1.0f, 1.0f);
                float2 leash = new float2(0.0f, 0.0f);

                Int16 vertexIndex = sgc.VertexAdd(position, rotation, scale, leash);

                // If vertices are selected, make them the parents of the new vertex.
                if ((selectionType == SelectionType.Vertex) && (selectedIndices.Count > 0))
                {
                    for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                    {
                        Int16 vertexParent = selectedIndices[i];

                        sgc.EdgeAdd(vertexParent, vertexIndex);
                    }
                }

                // Select point after adding for convenience, as typical use is add point, move, add point, move, etc. 
                selectedIndices.Clear();
                selectionType = SelectionType.Vertex;
                selectedIndices.Add(vertexIndex);
                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }
        }

        private void OnInspectorGUIConnectSelectedTool(SplineGraphComponent sgc)
        {
            if (GUILayout.Button("Connect Selected"))
            {
                if ((selectionType == SelectionType.Vertex) && (selectedIndices.Count > 1))
                {
                    sgc.UndoRecord("Spline Graph Edge(s) Add", true);

                    Int16 vertexParent = selectedIndices[0];
                    for (Int16 i = 1, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                    {
                        Int16 vertexChild = selectedIndices[i];

                        sgc.EdgeAdd(vertexParent, vertexChild);
                    }
                }
            }
        }

        private void OnInspectorGUIMergeSelectedVerticesTool(SplineGraphComponent sgc)
        {
            if (GUILayout.Button("Merge Selected Vertices"))
            {
                if ((selectionType == SelectionType.Vertex) && (selectedIndices.Count > 1))
                {
                    sgc.UndoRecord("Spline Graph Vertices Merge", true);

                    Int16 vertexParent = selectedIndices[0];
                    for (Int16 i = 1, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                    {
                        Int16 vertexChild = selectedIndices[i];

                        sgc.VertexMerge(vertexParent, vertexChild);
                    }

                    // Deselect all (now invalid) points.
                    selectedIndices.Clear();
                    selectionType = SelectionType.Vertex;
                    selectedIndices.Add(vertexParent);
                    Repaint(); // Repaint editor to display selection changes in InspectorGUI.
                }
            }
        }

        private void OnInspectorGUISplitEdgeBetweenSelectedVerticesTool(SplineGraphComponent sgc)
        {
            if (GUILayout.Button("Split Edge Between Selected Vertices"))
            {
                if ((selectionType == SelectionType.Vertex) && (selectedIndices.Count > 1))
                {
                    sgc.UndoRecord("Spline Graph Split Edge", true);

                    scratchIndices.Clear();
                    for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                    {
                        Int16 vertexParentCandidateIndex = selectedIndices[i];
                        DirectedVertex vertexParentCandidate = sgc.splineGraph.vertices.data[vertexParentCandidateIndex];
                        Debug.Assert(vertexParentCandidate.IsValid() == 1);

                        for (Int16 j = 0; j < iCount; ++j)
                        {
                            if (i == j) { continue; }
                            Int16 vertexChildCandidateIndex = selectedIndices[j];
                            DirectedVertex vertexChildCandidate = sgc.splineGraph.vertices.data[vertexChildCandidateIndex];
                            Debug.Assert(vertexChildCandidate.IsValid() == 1);

                            if (sgc.splineGraph.EdgeContains(vertexParentCandidateIndex, vertexChildCandidateIndex) == 1)
                            {
                                // Found a parent, child pair.
                                scratchIndices.Add(vertexParentCandidateIndex);
                                scratchIndices.Add(vertexChildCandidateIndex);
                            }
                        }
                    }
                    selectedIndices.Clear();

                    for (Int16 i = 0, iCount = (Int16)scratchIndices.Count; i < iCount; i += 2)
                    {
                        Int16 vertexParentIndex = scratchIndices[i + 0];
                        Int16 vertexChildIndex = scratchIndices[i + 1];

                        Int16 vertexSplitIndex = sgc.splineGraph.VertexAdd(Allocator.Persistent);

                        {
                            Int16 edgeIndexPrevious = sgc.splineGraph.EdgeFindIndex(vertexParentIndex, vertexChildIndex);
                            SplineMath.Spline spline = sgc.splineGraph.payload.edgeParentToChildSplines.data[edgeIndexPrevious];
                            float edgeLength = sgc.splineGraph.payload.edgeLengths.data[edgeIndexPrevious];
                            float t = SplineMath.ComputeTFromDeltaIntegrate(spline, 0.0f, edgeLength * 0.5f, sampleCount: 32);

                            SplineMath.ComputePositionRotationLeashFromT(
                                out float3 position,
                                out quaternion rotation,
                                out float2 leash,
                                vertexParentIndex,
                                vertexChildIndex,
                                edgeIndexPrevious,
                                t,
                                sgc.splineGraph.payload.positions.data,
                                sgc.splineGraph.payload.rotations.data,
                                sgc.splineGraph.payload.leashes.data,
                                sgc.splineGraph.payload.edgeParentToChildSplines.data,
                                sgc.splineGraph.payload.edgeParentToChildSplinesLeashes.data
                            );

                            sgc.splineGraph.payload.positions.data[vertexSplitIndex] = position;
                            sgc.splineGraph.payload.rotations.data[vertexSplitIndex] = rotation;
                            sgc.splineGraph.payload.scales.data[vertexSplitIndex] = math.lerp(
                                sgc.splineGraph.payload.scales.data[vertexParentIndex],
                                sgc.splineGraph.payload.scales.data[vertexChildIndex],
                                0.5f
                            ) * 0.5f;
                            sgc.splineGraph.payload.leashes.data[vertexSplitIndex] = leash;

                            sgc.splineGraph.payload.scales.data[vertexParentIndex] = sgc.splineGraph.payload.scales.data[vertexParentIndex] * new float2(1.0f, 0.5f);
                            sgc.splineGraph.payload.scales.data[vertexChildIndex] = sgc.splineGraph.payload.scales.data[vertexChildIndex] * new float2(0.5f, 1.0f);
                        }

                        sgc.splineGraph.EdgeRemove(vertexParentIndex, vertexChildIndex);
                        sgc.splineGraph.EdgeAdd(vertexParentIndex, vertexSplitIndex, Allocator.Persistent);
                        sgc.splineGraph.EdgeAdd(vertexSplitIndex, vertexChildIndex, Allocator.Persistent);
                        selectedIndices.Add(vertexSplitIndex);
                    }

                    scratchIndices.Clear();
                    Repaint(); // Repaint editor to display selection changes in InspectorGUI.
                }
            }
        }

        private void OnInspectorGUIBuildCompactGraphTool(SplineGraphComponent sgc)
        {
            if (GUILayout.Button("Build Compact Graph"))
            {
                sgc.UndoRecord("Spline Graph Build Compact Graph", true);

                sgc.BuildCompactGraph();

                // Vertex and Edge indices change when graph is compacted.
                // Selection will no longer be valid.
                selectedIndices.Clear();
                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }
        }

        private void OnInspectorGUICopyGraphTool(SplineGraphComponent sgc)
        {
            if (GUILayout.Button("Copy Graph"))
            {
                sgc.splineGraph.Copy(ref sgc.splineGraph, ref SplineGraphComponentEditor.copyScratchWS, Allocator.Persistent);

                // Convert the graph payload from object space, into world space.
                // TODO: Add (lossy) support for respecting scale transformations on payload.scales and payload.leashes
                for (Int16 v = 0, vCount = (Int16)SplineGraphComponentEditor.copyScratchWS.vertices.count; v < vCount; ++v)
                {
                    DirectedVertex vertex = SplineGraphComponentEditor.copyScratchWS.vertices.data[v];
                    if (vertex.IsValid() == 0) { continue; }

                    float3 vertexPositionOS = SplineGraphComponentEditor.copyScratchWS.payload.positions.data[v];
                    float3 vertexPositionWS = sgc.transform.TransformPoint(vertexPositionOS);
                    SplineGraphComponentEditor.copyScratchWS.payload.positions.data[v] = vertexPositionWS;
                }

                for (Int16 v = 0, vCount = (Int16)SplineGraphComponentEditor.copyScratchWS.vertices.count; v < vCount; ++v)
                {
                    DirectedVertex vertex = SplineGraphComponentEditor.copyScratchWS.vertices.data[v];
                    if (vertex.IsValid() == 0) { continue; }

                    quaternion vertexRotationOS = SplineGraphComponentEditor.copyScratchWS.payload.rotations.data[v];
                    quaternion vertexRotationWS = math.mul((quaternion)sgc.transform.rotation, vertexRotationOS);
                    SplineGraphComponentEditor.copyScratchWS.payload.rotations.data[v] = vertexRotationWS;
                }

                for (Int16 v = 0, vCount = (Int16)SplineGraphComponentEditor.copyScratchWS.vertices.count; v < vCount; ++v)
                {
                    DirectedVertex vertex = SplineGraphComponentEditor.copyScratchWS.vertices.data[v];
                    if (vertex.IsValid() == 0) { continue; }

                    SplineGraphComponentEditor.copyScratchWS.payload.VertexComputePayloads(ref SplineGraphComponentEditor.copyScratchWS, v);
                }

                for (Int16 e = 0, eCount = (Int16)SplineGraphComponentEditor.copyScratchWS.edgePoolChildren.count; e < eCount; ++e)
                {
                    DirectedEdge edge = SplineGraphComponentEditor.copyScratchWS.edgePoolChildren.data[e];
                    if (edge.IsValid() == 0) { continue; }

                    Int16 vertexIndexChild = edge.vertexIndex;
                    Int16 vertexIndexParent = SplineGraphComponentEditor.copyScratchWS.edgePoolParents.data[e].vertexIndex;

                    SplineGraphComponentEditor.copyScratchWS.payload.EdgeComputePayloads(ref SplineGraphComponentEditor.copyScratchWS, vertexIndexParent, vertexIndexChild);
                }
            }
        }

        private void OnInspectorGUIPasteGraphTool(SplineGraphComponent sgc)
        {
            if (GUILayout.Button("Paste Graph"))
            {
                sgc.UndoRecord("Spline Graph Paste Graph", true);

                SplineGraphComponentEditor.copyScratchWS.Copy(ref SplineGraphComponentEditor.copyScratchWS, ref sgc.splineGraph, Allocator.Persistent);
                for (Int16 v = 0, vCount = (Int16)sgc.splineGraph.vertices.count; v < vCount; ++v)
                {
                    DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                    if (vertex.IsValid() == 0) { continue; }

                    float3 vertexPositionWS = sgc.splineGraph.payload.positions.data[v];
                    float3 vertexPositionOS = sgc.transform.InverseTransformPoint(vertexPositionWS);
                    sgc.splineGraph.payload.positions.data[v] = vertexPositionOS;
                }

                for (Int16 v = 0, vCount = (Int16)sgc.splineGraph.vertices.count; v < vCount; ++v)
                {
                    DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                    if (vertex.IsValid() == 0) { continue; }

                    quaternion vertexRotationWS = sgc.splineGraph.payload.rotations.data[v];
                    quaternion vertexRotationOS = math.mul(math.inverse(sgc.transform.rotation), vertexRotationWS);
                    sgc.splineGraph.payload.rotations.data[v] = vertexRotationOS;
                }

                for (Int16 v = 0, vCount = (Int16)sgc.splineGraph.vertices.count; v < vCount; ++v)
                {
                    DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                    if (vertex.IsValid() == 0) { continue; }

                    sgc.splineGraph.payload.VertexComputePayloads(ref sgc.splineGraph, v);
                }

                for (Int16 e = 0, eCount = (Int16)sgc.splineGraph.edgePoolChildren.count; e < eCount; ++e)
                {
                    DirectedEdge edge = sgc.splineGraph.edgePoolChildren.data[e];
                    if (edge.IsValid() == 0) { continue; }

                    Int16 vertexIndexChild = edge.vertexIndex;
                    Int16 vertexIndexParent = sgc.splineGraph.edgePoolParents.data[e].vertexIndex;

                    sgc.splineGraph.payload.EdgeComputePayloads(ref sgc.splineGraph, vertexIndexParent, vertexIndexChild);
                }


                // Convert the graph payload from world space to object space.
                // TODO: Add (lossy) support for respecting scale transformations on payload.scales and payload.leashes

                // Vertex and Edge indices change when graph is pasted.
                // Selection will no longer be valid.
                selectedIndices.Clear();
                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }
        }

        private void OnInspectorGUIReverseTool(SplineGraphComponent sgc)
        {
            if (GUILayout.Button("Reverse"))
            {
                sgc.UndoRecord("Spline Graph Reverse", true);

                sgc.BuildCompactReverseGraph();

                // Vertex and Edge indices change when graph is compacted.
                // Selection will no longer be valid.
                selectedIndices.Clear();
                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }
        }

        private void OnInspectorGUISelectAllTool(SplineGraphComponent sgc)
        {
            if (GUILayout.Button("Select All"))
            {
                selectedIndices.Clear();

                for (Int16 v = 0, vCount = (Int16)sgc.splineGraph.vertices.count; v < vCount; ++v)
                {
                    DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                    if (vertex.IsValid() == 0) { continue; }

                    selectionType = SelectionType.Vertex;
                    selectedIndices.Add(v);
                }


                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }
        }

        private void OnInspectorGUIDeselectAllTool(SplineGraphComponent sgc)
        {
            if (GUILayout.Button("Deselect All"))
            {
                selectedIndices.Clear();
                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }
        }

        private void OnInspectorGUIRecenterTransformTool(SplineGraphComponent sgc)
        {
            if (GUILayout.Button("Recenter Transform"))
            {
                sgc.UndoRecord("Spline Graph Recenter Transform", true);

                float3 positionAverage = float3.zero;
                int positionAverageCount = 0;
                for (Int16 v = 0, vCount = (Int16)sgc.splineGraph.vertices.count; v < vCount; ++v)
                {
                    DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                    if (vertex.IsValid() == 0) { continue; }

                    positionAverage += sgc.splineGraph.payload.positions.data[v];
                    ++positionAverageCount;
                }

                if (positionAverageCount > 0)
                {
                    positionAverage /= (float)positionAverageCount;

                    for (Int16 v = 0, vCount = (Int16)sgc.splineGraph.vertices.count; v < vCount; ++v)
                    {
                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                        if (vertex.IsValid() == 0) { continue; }

                        sgc.splineGraph.payload.positions.data[v] -= positionAverage;
                        sgc.splineGraph.VertexComputePayloads(v);
                    }

                    Undo.RecordObject(sgc.transform, "Spline Graph Recenter Transform");
                    sgc.transform.position += (Vector3)positionAverage;
                }
            }
        }

        private void OnInspectorGUIEditVertexTool(SplineGraphComponent sgc)
        {
            Int16 indexAdd = -1;
            Int16 indexRemove = -1;
            Color styleTextColorPrevious = EditorStyles.label.normal.textColor;
            for (Int16 v = 0, vCount = (Int16)sgc.splineGraph.vertices.count; v < vCount; ++v)
            {
                DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                if (selectionType == SelectionType.Vertex)
                {
                    for (int i = 0, iLen = selectedIndices.Count; i < iLen; ++i)
                    {
                        Int16 selectedIndex = selectedIndices[i];
                        if (selectedIndex == v)
                        {
                            // Current vertex is selected. Change style in GUI.
                            EditorStyles.label.normal.textColor = Color.green;
                            break;
                        }

                    }
                }

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("+", GUILayout.Width(20)))
                {
                    indexAdd = v;
                }

                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    indexRemove = v;
                }

                EditorGUILayout.BeginVertical();

                float3 position = sgc.splineGraph.payload.positions.data[v];
                float3 positionNew = EditorGUILayout.Vector3Field("Position", position);
                if (math.any(position != positionNew))
                {
                    sgc.UndoRecord("Edited Spline Graph Vertex Position");

                    sgc.splineGraph.payload.positions.data[v] = positionNew;
                    sgc.splineGraph.VertexComputePayloads(v);
                }

                quaternion rotation = sgc.splineGraph.payload.rotations.data[v];

                float3 rotationEulerDegrees = ((Quaternion)rotation).eulerAngles;
                float3 rotationEulerDegreesNew = EditorGUILayout.Vector3Field("Rotation", rotationEulerDegrees);
                if (math.any(rotationEulerDegreesNew != rotationEulerDegrees))
                {
                    sgc.UndoRecord("Edited Spline Graph Vertex Rotation");

                    float3 rotationEulerRadiansNew = math.radians(rotationEulerDegreesNew);
                    quaternion rotationNew = quaternion.EulerXYZ(rotationEulerRadiansNew);
                    sgc.splineGraph.payload.rotations.data[v] = rotationNew;
                    sgc.splineGraph.VertexComputePayloads(v);
                }

                float2 scale = sgc.splineGraph.payload.scales.data[v];
                float2 scaleNew = EditorGUILayout.Vector2Field("Scale", scale);
                if (math.any(scaleNew != scale))
                {
                    sgc.UndoRecord("Edited Spline Graph Vertex Scale");

                    sgc.splineGraph.payload.scales.data[v] = scaleNew;
                    sgc.splineGraph.VertexComputePayloads(v);
                }

                float2 leash = sgc.splineGraph.payload.leashes.data[v];
                float2 leashNew = EditorGUILayout.Vector2Field("Leash", leash);
                if (math.any(leashNew != leash))
                {
                    sgc.UndoRecord("Edited Spline Graph Vertex Leash");

                    sgc.splineGraph.payload.leashes.data[v] = leashNew;
                    sgc.splineGraph.VertexComputePayloads(v);
                }

                if (sgc.splineGraph.payload.userBlobSchemaVersion != 0)
                {
                    for (int scheme = 0; scheme < sgc.splineGraph.payload.userBlobVertex.schema.Length; ++scheme)
                    {
                        switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].type)
                        {
                            case SplineGraphUserBlob.Scheme.Type.Int:
                            {
                                switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].stride)
                                {
                                    case 1:
                                    {
                                        int value = sgc.splineGraph.payload.userBlobVertex.GetInt(scheme, v);
                                        int valueNew = EditorGUILayout.IntField(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], value);
                                        if (value != valueNew)
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    case 2:
                                    {
                                        int2 value = sgc.splineGraph.payload.userBlobVertex.GetInt2(scheme, v);
                                        Vector2Int valueNewUI = EditorGUILayout.Vector2IntField(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], new Vector2Int(value.x, value.y));
                                        int2 valueNew = new int2(valueNewUI.x, valueNewUI.y);
                                        if (math.any(value != valueNew))
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    case 3:
                                    {
                                        int3 value = sgc.splineGraph.payload.userBlobVertex.GetInt3(scheme, v);
                                        Vector3Int valueNewUI = EditorGUILayout.Vector3IntField(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], new Vector3Int(value.x, value.y, value.z));
                                        int3 valueNew = new int3(valueNewUI.x, valueNewUI.y, valueNewUI.z);
                                        if (math.any(value != valueNew))
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    case 4:
                                    {
                                        int4 value = sgc.splineGraph.payload.userBlobVertex.GetInt4(scheme, v);
                                        Vector4 valueNewUI = EditorGUILayout.Vector4Field(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], new Vector4(value.x, value.y, value.z, value.w));
                                        int4 valueNew = new int4((int)valueNewUI.x, (int)valueNewUI.y, (int)valueNewUI.z, (int)valueNewUI.w);
                                        if (math.any(value != valueNew))
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    default: Debug.Assert(false); break;
                                }
                                break;
                            }

                            case SplineGraphUserBlob.Scheme.Type.UInt:
                            {
                                switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].stride)
                                {
                                    case 1:
                                    {
                                        uint value = sgc.splineGraph.payload.userBlobVertex.GetUInt(scheme, v);
                                        uint valueNew = (uint)math.max(0, EditorGUILayout.IntField(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], (int)value));
                                        if (value != valueNew)
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    case 2:
                                    {
                                        uint2 value = sgc.splineGraph.payload.userBlobVertex.GetUInt2(scheme, v);
                                        Vector2Int valueNewUI = EditorGUILayout.Vector2IntField(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], new Vector2Int((int)value.x, (int)value.y));
                                        uint2 valueNew = new uint2((uint)math.max(0, valueNewUI.x), (uint)math.max(0, valueNewUI.y));
                                        if (math.any(value != valueNew))
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    case 3:
                                    {
                                        uint3 value = sgc.splineGraph.payload.userBlobVertex.GetUInt3(scheme, v);
                                        Vector3Int valueNewUI = EditorGUILayout.Vector3IntField(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], new Vector3Int((int)value.x, (int)value.y, (int)value.z));
                                        uint3 valueNew = new uint3((uint)math.max(0, valueNewUI.x), (uint)math.max(0, valueNewUI.y), (uint)math.max(0, valueNewUI.z));
                                        if (math.any(value != valueNew))
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    case 4:
                                    {
                                        uint4 value = sgc.splineGraph.payload.userBlobVertex.GetUInt4(scheme, v);
                                        Vector4 valueNewUI = EditorGUILayout.Vector4Field(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], new Vector4(value.x, value.y, value.z, value.w));
                                        uint4 valueNew = new uint4((uint)math.max(0, valueNewUI.x), (uint)math.max(0, valueNewUI.y), (uint)math.max(0, valueNewUI.z), (uint)math.max(0, valueNewUI.w));
                                        if (math.any(value != valueNew))
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    default: Debug.Assert(false); break;
                                }
                                break;
                            }

                            case SplineGraphUserBlob.Scheme.Type.Float:
                            {
                                switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].stride)
                                {
                                    case 1:
                                    {
                                        float value = sgc.splineGraph.payload.userBlobVertex.GetFloat(scheme, v);
                                        float valueNew = math.max(0, EditorGUILayout.FloatField(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], value));
                                        if (value != valueNew)
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    case 2:
                                    {
                                        float2 value = sgc.splineGraph.payload.userBlobVertex.GetFloat2(scheme, v);
                                        float2 valueNew = EditorGUILayout.Vector2Field(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], value);
                                        if (math.any(value != valueNew))
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    case 3:
                                    {
                                        float3 value = sgc.splineGraph.payload.userBlobVertex.GetFloat3(scheme, v);
                                        float3 valueNew = EditorGUILayout.Vector3Field(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], value);
                                        if (math.any(value != valueNew))
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    case 4:
                                    {
                                        float4 value = sgc.splineGraph.payload.userBlobVertex.GetFloat4(scheme, v);
                                        float4 valueNew = EditorGUILayout.Vector4Field(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], value);
                                        if (math.any(value != valueNew))
                                        {
                                            sgc.UndoRecord("Edited Spline Graph Vertex User Blob");
                                            sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, valueNew);
                                        }
                                        break;
                                    }
                                    default: Debug.Assert(false); break;
                                }
                                break;
                            }

                            default: Debug.Assert(false); break;
                        }
                    }
                }

                EditorStyles.label.normal.textColor = styleTextColorPrevious;

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }

            if (indexAdd != -1)
            {
                sgc.UndoRecord("Spline Graph Vertex Insert", true);
                {
                    float3 positionParent = sgc.splineGraph.payload.positions.data[indexAdd];
                    quaternion rotationParent = sgc.splineGraph.payload.rotations.data[indexAdd];
                    float2 scaleParent = sgc.splineGraph.payload.scales.data[indexAdd];
                    float2 leashParent = sgc.splineGraph.payload.leashes.data[indexAdd];

                    // TODO: Compute average parent position and use negation of that for offset.
                    float3 positionOffset = new float3(0.0f, 0.0f, 1.0f);

                    Int16 vertexChild = sgc.VertexAdd(positionParent + positionOffset, rotationParent, scaleParent, leashParent);
                    sgc.EdgeAdd(indexAdd, vertexChild);

                    // Select point after adding for convenience, as typical use is add point, move, add point, move, etc.
                    selectedIndices.Clear();
                    selectionType = SelectionType.Vertex;
                    selectedIndices.Add(vertexChild);
                    Repaint(); // Repaint editor to display selection changes in InspectorGUI.
                }
            }

            if (indexRemove != -1)
            {
                sgc.UndoRecord("Spline Graph Vertex Remove", true);
                {
                    // Deselect vertex we are removing.
                    if ((selectedIndices.Count > 0) && (selectionType == SelectionType.Vertex))
                    {
                        for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                        {
                            if (selectedIndices[i] == indexRemove)
                            {
                                selectedIndices.RemoveAt(i);
                                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
                                break;
                            }
                        }
                    }

                    // And now remove the vertex.
                    sgc.VertexRemove(indexRemove);
                }
            }
        }
    }
}
