﻿using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace Pastasfuture.SplineGraph.Runtime
{
    [ExecuteInEditMode]
    public class SplineGraphComponent : MonoBehaviour
    #if UNITY_EDITOR
    , ISerializationCallbackReceiver
    #endif
    {
    #if UNITY_EDITOR
        // Warning: This is a component only intended for editor use.
        // All data and methods will be stripped from this component during build.
        //
        [System.NonSerialized] public DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph;
        
        [System.NonSerialized] public bool isEditingEnabled = false;
        public int type = 0;
        public DirectedGraphSerializable splineGraphSerializable = new DirectedGraphSerializable();
        public SplineGraphPayloadSerializable splineGraphPayloadSerializable = new SplineGraphPayloadSerializable();

        private bool isDeserializationNeeded = true;
        [System.NonSerialized] public bool isDirty = true;

        public static List<SplineGraphComponent> instances = new List<SplineGraphComponent>();

        void OnEnable()
        {
            // DirectedGraph<SplineGraphPayload>.DebugRunTests();
            instances.Add(this);
        }

        void OnDisable()
        {
            instances.Remove(this);
        }

        void OnDestroy()
        {
            Dispose();
        }

        void Dispose()
        {
            // TODO:
            splineGraph.Dispose();
        }

        public void Verify()
        {
            // TODO:
            if (isDeserializationNeeded)
            {
                isDeserializationNeeded = false;

                splineGraph.Deserialize(ref splineGraphSerializable, ref splineGraphPayloadSerializable, Allocator.Persistent);
            }

            if (isDirty)
            {
                isDirty = false;

                splineGraph.Serialize(ref splineGraphSerializable, ref splineGraphPayloadSerializable);
            }
        }

        public void OnBeforeSerialize()
        {
            Verify();
        }

        public void OnAfterDeserialize()
        {
            isDeserializationNeeded = true;
        }

        public Int16 VertexAdd(float3 position, quaternion rotation, float2 scale)
        {
            Verify();

            Int16 vertexIndex = splineGraph.VertexAdd(Allocator.Persistent);

            splineGraph.payload.positions.data[vertexIndex] = position;
            splineGraph.payload.rotations.data[vertexIndex] = rotation;
            splineGraph.payload.scales.data[vertexIndex] = scale;

            return vertexIndex;
        }

        public void VertexRemove(Int16 vertexIndex)
        {
            Verify();

            splineGraph.VertexRemove(vertexIndex);
        }

        public void EdgeAdd(Int16 vertexParent, Int16 vertexChild)
        {
            Verify();

            splineGraph.EdgeAdd(vertexParent, vertexChild, Allocator.Persistent);
        }

        public void EdgeRemove(Int16 vertexParent, Int16 vertexChild)
        {
            Verify();

            splineGraph.EdgeRemove(vertexParent, vertexChild);
        }

        public void VertexMerge(Int16 vertexParent, Int16 vertexChild)
        {
            Verify();

            splineGraph.VertexMerge(vertexParent, vertexChild, Allocator.Persistent);
        }

        public void UndoRecord(string message)
        {
            Undo.RecordObject(this, message);
            isDirty = true;
        }

        public void BuildCompactGraph()
        {
            Verify();

            var splineGraphCompact = new DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable>(Allocator.Persistent); 
            splineGraph.BuildCompactDirectedGraph(ref splineGraphCompact, Allocator.Persistent);
            splineGraph.Dispose();
            splineGraph = splineGraphCompact;
        }

        public void BuildCompactReverseGraph()
        {
            Verify();

            var splineGraphCompact = new DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable>(Allocator.Persistent); 
            splineGraph.BuildCompactReverseDirectedGraph(ref splineGraphCompact, Allocator.Persistent);
            splineGraph.Dispose();
            splineGraph = splineGraphCompact;
        }

        public static void DrawSplineGraph(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vCount = (Int16)splineGraph.vertices.count; v < vCount; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                {
                    float3 vertexPosition = splineGraph.payload.positions.data[v];
                    quaternion vertexRotation = splineGraph.payload.rotations.data[v];
                    float handleSize = HandleUtility.GetHandleSize(vertexPosition);
                    const float HANDLE_DISPLAY_SIZE = 0.05f;
                    handleSize *= HANDLE_DISPLAY_SIZE;

                    Int16 parentCount = splineGraph.VertexComputeParentCount(v);
                    Int16 childCount = splineGraph.VertexComputeChildCount(v);

                    if (parentCount > 0 && childCount > 0)
                    {
                        Handles.color = Color.blue;
                    }
                    else if (childCount > 0)
                    {
                        Handles.color = Color.green;
                    }
                    else if (parentCount > 0)
                    {
                        Handles.color = Color.red;
                    }
                    else
                    {
                        Handles.color = Color.white;
                    }

                    Handles.DotHandleCap(0, vertexPosition, vertexRotation, handleSize, EventType.Repaint);
                }

                // Place vertex index above vertex position.
                {
                    float3 vertexPosition = splineGraph.payload.positions.data[v];
                    quaternion vertexRotation = splineGraph.payload.rotations.data[v];
                    float handleSize = HandleUtility.GetHandleSize(vertexPosition);
                    const float HANDLE_DISPLAY_SIZE = 0.125f;
                    float vertexLabelSize = handleSize * HANDLE_DISPLAY_SIZE;
                    float3 labelPosition = vertexPosition + math.mul(vertexRotation, new float3(0.0f, vertexLabelSize * 2.0f, 0.0f));
                    // Handles.Label(labelPosition, "" + v);
                }

                Handles.color = Color.white;
                for (Int16 e = vertex.childHead; e != -1; e = splineGraph.edgePoolChildren.data[e].next)
                {
                    DirectedEdge edge = splineGraph.edgePoolChildren.data[e];

                    // TODO: Perform adaptive tesselation of line.
                    SplineMath.Spline spline = splineGraph.payload.edgeParentToChildSplines.data[e];
                    float splineLength = splineGraph.payload.edgeLengths.data[e];
                    const int SAMPLE_COUNT = 8;
                    const float SAMPLE_COUNT_INVERSE = 1.0f / (float)SAMPLE_COUNT;
                    for (int s = 0; s < SAMPLE_COUNT; ++s)
                    {
                        float t0 = (float)(s + 0) * SAMPLE_COUNT_INVERSE;
                        float t1 = (float)(s + 1) * SAMPLE_COUNT_INVERSE;

                        float3 samplePosition0 = SplineMath.EvaluatePositionFromT(spline, t0);
                        float3 samplePosition1 = SplineMath.EvaluatePositionFromT(spline, t1);

                        Handles.DrawLine(samplePosition0, samplePosition1);
                    }

                    // Place arrow in center of spline segment.
                    {
                        float3 arrowPosition = SplineMath.EvaluatePositionFromT(spline, 0.5f);
                        quaternion arrowRotation = SplineMath.EvaluateRotationFromT(spline, 0.5f);
                        
                        float handleSize = HandleUtility.GetHandleSize(arrowPosition);
                        const float HANDLE_DISPLAY_SIZE = 0.125f;
                        float arrowSize = handleSize * HANDLE_DISPLAY_SIZE;
                        Handles.ConeHandleCap(0, arrowPosition, arrowRotation, arrowSize, EventType.Repaint);

                        float3 labelPosition = arrowPosition + math.mul(arrowRotation, new float3(0.0f, arrowSize * 2.0f, 0.0f));
                        // Handles.Label(labelPosition, "Length = " + splineLength);
                    }
                }
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        #if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Verify();

            if (!isEditingEnabled) { return; }

            // return; // TODO: Remove?
            SplineGraphComponent.DrawSplineGraph(ref splineGraph, transform);
        }
        #endif
        #endif // #if UNITY_EDITOR
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(SplineGraphComponent))]
    public class SplineGraphComponentEditor : Editor
    {
        private enum SelectionType
        {
            Vertex = 0,
            Edge = 1
        };
        private SelectionType selectionType = SelectionType.Vertex;
        private List<Int16> selectedIndices = new List<Int16>();

        private enum DragRectState
        {
            None = 0,
            Dragging
        };
        private DragRectState dragRectState = DragRectState.None;
        private float2 dragRectPositionSS0 = new float2(0.0f, 0.0f);
        private float2 dragRectPositionSS1 = new float2(0.0f, 0.0f);

        public override void OnInspectorGUI()
        {
            var sgc = target as SplineGraphComponent;
            sgc.Verify();

            EditorGUILayout.BeginVertical();

            bool isEditingEnabledNew = EditorGUILayout.Toggle("Is Editing Enabled", sgc.isEditingEnabled);
            if (isEditingEnabledNew != sgc.isEditingEnabled)
            {
                sgc.UndoRecord("Toggled SplineGraph.isEditingEnabled");
                sgc.isEditingEnabled = isEditingEnabledNew;
            }
            if (!sgc.isEditingEnabled)
            {
                EditorGUILayout.EndVertical(); 
                return;
            }

            int typeNew = EditorGUILayout.DelayedIntField("Type", sgc.type);
            typeNew = math.clamp(typeNew, 0, int.MaxValue);
            if (typeNew != sgc.type)
            {
                sgc.UndoRecord("Edited Spline Graph Type");
                sgc.type = typeNew;
            }

            EditorGUILayout.BeginVertical();
            if (GUILayout.Button("Add Vertex"))
            {
                sgc.UndoRecord("Spline Graph Vertex Add");

                float3 position = new float3(0.0f, 0.0f, 0.0f);
                quaternion rotation = quaternion.identity;
                float2 scale = new float2(1.0f, 1.0f);

                Int16 vertexIndex = sgc.VertexAdd(position, rotation, scale);

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

            if (GUILayout.Button("Connect Selected"))
            {
                if ((selectionType == SelectionType.Vertex) && (selectedIndices.Count > 1))
                {
                    sgc.UndoRecord("Spline Graph Edge(s) Add");

                    Int16 vertexParent = selectedIndices[0];
                    for (Int16 i = 1, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                    {
                        Int16 vertexChild = selectedIndices[i];

                        sgc.EdgeAdd(vertexParent, vertexChild);
                    }
                }
            }

            if (GUILayout.Button("Merge Selected Vertices"))
            {
                if ((selectionType == SelectionType.Vertex) && (selectedIndices.Count > 1))
                {
                    sgc.UndoRecord("Spline Graph Vertices Merge");

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

            if (GUILayout.Button("Build Compact Graph"))
            {
                sgc.UndoRecord("Spline Graph Build Compact Graph");

                sgc.BuildCompactGraph();

                // Vertex and Edge indices change when graph is compacted.
                // Selection will no longer be valid.
                selectedIndices.Clear();
                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }

            if (GUILayout.Button("Reverse"))
            {
                sgc.UndoRecord("Spline Graph Reverse");

                sgc.BuildCompactReverseGraph();

                // Vertex and Edge indices change when graph is compacted.
                // Selection will no longer be valid.
                selectedIndices.Clear();
                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }

            if (GUILayout.Button("Deselect All"))
            {
                selectedIndices.Clear();
                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }
            EditorGUILayout.EndVertical();

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

                EditorStyles.label.normal.textColor = styleTextColorPrevious;
                
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }

            if (indexAdd != -1)
            {
                sgc.UndoRecord("Spline Graph Vertex Insert");
                {
                    float3 positionParent = sgc.splineGraph.payload.positions.data[indexAdd];
                    quaternion rotationParent = sgc.splineGraph.payload.rotations.data[indexAdd];
                    float2 scaleParent = sgc.splineGraph.payload.scales.data[indexAdd];

                    // TODO: Compute average parent position and use negation of that for offset.
                    float3 positionOffset = new float3(0.0f, 0.0f, 1.0f);

                    Int16 vertexChild = sgc.VertexAdd(positionParent + positionOffset, rotationParent, scaleParent);
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
                sgc.UndoRecord("Spline Graph Vertex Remove");
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

            EditorGUILayout.EndVertical();
        }

        private void OnSceneGUI()
        {
            // If we are currently tumbling the camera, do not attempt to do anything else.
            if (Event.current.alt) { return; }

            var sgc = target as SplineGraphComponent;
            sgc.Verify();

            if (!sgc.isEditingEnabled) { return; }

            // return; // TODO: Remove?

            Transform transform = sgc.transform;
            Quaternion rotation = (Tools.pivotRotation == PivotRotation.Local)
                ? transform.rotation
                : Quaternion.identity;

            // 1) Select vertex(s).
            if (dragRectState == DragRectState.None)
            {
                for (Int16 v = 0, vCount = (Int16)sgc.splineGraph.vertices.count; v < vCount; ++v)
                {
                    DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                    if (vertex.IsValid() == 0) { continue; }

                    float3 vertexPosition = sgc.splineGraph.payload.positions.data[v];
                    vertexPosition = transform.TransformPoint(vertexPosition);

                    quaternion vertexRotation = sgc.splineGraph.payload.rotations.data[v];
                    vertexRotation = math.mul(rotation, vertexRotation);

                    float handleSize = HandleUtility.GetHandleSize(vertexPosition);
                    const float HANDLE_DISPLAY_SIZE = 0.05f;
                    const float HANDLE_PICK_SIZE = HANDLE_DISPLAY_SIZE + HANDLE_DISPLAY_SIZE * 0.5f;
                    if (Handles.Button(vertexPosition, vertexRotation, handleSize * HANDLE_DISPLAY_SIZE, handleSize * HANDLE_PICK_SIZE, Handles.DotHandleCap))
                    {
                        if (Event.current.shift && (selectionType == SelectionType.Vertex))
                        {
                            // Additive / Subtractive selection.
                            if (selectedIndices.IndexOf(v) >= 0)
                            {
                                selectedIndices.Remove(v);
                            }
                            else
                            {
                                selectedIndices.Add(v);
                            }

                        }
                        else
                        {
                            selectionType = SelectionType.Vertex;
                            selectedIndices.Clear();
                            selectedIndices.Add(v);
                        }
                        Repaint(); // Repaint editor to display selection changes in InspectorGUI.
                    }
                }
            }

            if ((selectionType == SelectionType.Vertex) && (selectedIndices.Count > 0))
            {
                EditorGUI.BeginChangeCheck();

                // 2) Compute transform delta(s) for vertex(s).
                float3 positionOffsetOS = new float3(0.0f, 0.0f, 0.0f);
                quaternion rotationOffsetOS = quaternion.identity;
                float3 rotationOffsetOriginOS = new float3(0.0f, 0.0f, 0.0f);
                float2 scaleOffsetOS = new float2(1.0f, 1.0f);
                for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                {
                    Int16 selectedVertexIndex = selectedIndices[i];

                    float3 vertexPositionOS = sgc.splineGraph.payload.positions.data[selectedVertexIndex];
                    quaternion vertexRotationOS = sgc.splineGraph.payload.rotations.data[selectedVertexIndex];
                    float2 vertexScaleOS = sgc.splineGraph.payload.scales.data[selectedVertexIndex];
                    vertexRotationOS = math.mul(rotation, vertexRotationOS);

                    vertexPositionOS = transform.TransformPoint(vertexPositionOS);

                    // TODO: Make scale respect transform scale.

                    if (Tools.current == Tool.Move)
                    {
                        float3 vertexPositionNewOS = Handles.PositionHandle(vertexPositionOS, vertexRotationOS);
                        if (math.any(vertexPositionOS != vertexPositionNewOS))
                        {
                            positionOffsetOS = vertexPositionNewOS - vertexPositionOS;

                            // Under multi-selection, positionOffsetOS will be the same for all points being transformed.
                            break;
                        }
                    }

                    else if (Tools.current == Tool.Rotate)
                    {
                        quaternion vertexRotationNewOS = Handles.RotationHandle(vertexRotationOS, vertexPositionOS);
                        if (math.any(vertexRotationNewOS.value != vertexRotationOS.value))
                        {
                            // Compute the quaternion difference.
                            rotationOffsetOS = math.mul(vertexRotationNewOS, math.inverse(vertexRotationOS));

                            // Under multi-selection, rotationOffsetOS will be the same for all points being transformed.
                            break;
                        }
                    }

                    else if (Tools.current == Tool.Scale)
                    {
                        float handleSize = HandleUtility.GetHandleSize(vertexPositionOS) * 1.0f;
                        float scaleNewOSX = Handles.ScaleSlider(vertexScaleOS.x, vertexPositionOS, math.mul(vertexRotationOS, new float3(0.0f, 0.0f, -1.0f)), vertexRotationOS, handleSize, 1e-5f);
                        float scaleNewOSY = Handles.ScaleSlider(vertexScaleOS.y, vertexPositionOS, math.mul(vertexRotationOS, new float3(0.0f, 0.0f, 1.0f)), vertexRotationOS, handleSize, 1e-5f);
                        float2 scaleNewOS = new float2(scaleNewOSX, scaleNewOSY);
                        if (math.any(scaleNewOS != vertexScaleOS))
                        {
                            scaleOffsetOS.x = (math.abs(vertexScaleOS.x) < 1e-5f) ? 1.0f : (scaleNewOSX / vertexScaleOS.x);
                            scaleOffsetOS.y = (math.abs(vertexScaleOS.y) < 1e-5f) ? 1.0f : (scaleNewOSY / vertexScaleOS.y);

                            // Under multi-selection, scaleOffsetOS will be the same for all points being transformed.
                            break;
                        }
                    }
                }

                // 3) Apply transform delta(s) to vertex(s) and register undo.
                if (EditorGUI.EndChangeCheck())
                {
                    if (Tools.current == Tool.Move)
                    {
                        sgc.UndoRecord("Edited Spline Graph Vertex Position");
                        
                        for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                        {
                            Int16 selectedVertexIndex = selectedIndices[i];

                            float3 vertexPositionOS = sgc.splineGraph.payload.positions.data[selectedVertexIndex];
                            vertexPositionOS = transform.TransformPoint(vertexPositionOS);
                            vertexPositionOS += positionOffsetOS;
                            vertexPositionOS = transform.InverseTransformPoint(vertexPositionOS);
                            sgc.splineGraph.payload.positions.data[selectedVertexIndex] = vertexPositionOS;
                        }

                        for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                        {
                            sgc.splineGraph.VertexComputePayloads(selectedIndices[i]);
                        }
                    }

                    else if (Tools.current == Tool.Rotate)
                    {
                        sgc.UndoRecord("Edited Spline Graph Vertex Rotation");

                        for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                        {
                            Int16 selectedVertexIndex = selectedIndices[i];

                            quaternion vertexRotationOS = sgc.splineGraph.payload.rotations.data[selectedVertexIndex];
                            vertexRotationOS = math.mul(rotationOffsetOS, vertexRotationOS);
                            sgc.splineGraph.payload.rotations.data[selectedVertexIndex] = vertexRotationOS;
                        }

                        for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                        {
                            sgc.splineGraph.VertexComputePayloads(selectedIndices[i]);
                        }
                    }

                    else if (Tools.current == Tool.Scale)
                    {
                        sgc.UndoRecord("Edited Spline Graph Vertex Scale");

                        for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                        {
                            Int16 selectedVertexIndex = selectedIndices[i];

                            float2 vertexScaleOS = sgc.splineGraph.payload.scales.data[selectedVertexIndex];
                            vertexScaleOS *= scaleOffsetOS;
                            sgc.splineGraph.payload.scales.data[selectedVertexIndex] = vertexScaleOS;
                        }

                        for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                        {
                            sgc.splineGraph.VertexComputePayloads(selectedIndices[i]);
                        }
                    }
                }
            }
        }
    }
    #endif
}