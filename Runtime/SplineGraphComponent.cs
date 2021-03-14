using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace Pastasfuture.SplineGraph.Runtime
{
    [ExecuteInEditMode]
    public class SplineGraphComponent : MonoBehaviour, ISerializationCallbackReceiver
    {
        // Warning: This is a component only intended for editor use.
        // All data and methods will be stripped from this component during build.
        //
        [System.NonSerialized] public DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph;
        
        [System.NonSerialized] public bool isEditingEnabled = false;
        public int type = 0;
        public DirectedGraphSerializable splineGraphSerializable = new DirectedGraphSerializable();
        public SplineGraphPayloadSerializable splineGraphPayloadSerializable = new SplineGraphPayloadSerializable();
        public int gizmoSplineSegmentCount = 4;

        private bool isDeserializationNeeded = true;
        [System.NonSerialized] public bool isDirty = true;
        [System.NonSerialized] public int lastDirtyTimestamp = 0; // Do not need actual time, just a counter.

        public static List<SplineGraphComponent> instances = new List<SplineGraphComponent>();

        public DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> GetSplineGraph()
        {
            Verify();
            return splineGraph;
        }

#if UNITY_EDITOR
        void SubscribeUndoRedoPerformed()
        {
            UnsubscribeUndoRedoPerformed();
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        void UnsubscribeUndoRedoPerformed()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        void OnUndoRedoPerformed()
        {
            ++lastDirtyTimestamp;
        }
#endif

        void OnEnable()
        {
            // DirectedGraph<SplineGraphPayload>.DebugRunTests();
            instances.Add(this);

            Verify();

#if UNITY_EDITOR
            SubscribeUndoRedoPerformed();
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnsubscribeUndoRedoPerformed();
#endif

            instances.Remove(this);

            Verify();

            // Need to Dispose() inside of OnDisable(), because OnDestroy() is not called on domain reloads.
            // Only calling Dispose() inside of OnDestroy() would cause memory leaks between domain reloads.
            // This downside of calling Dispose() here is that enabling / disabling this component will trigger
            // (native) memory allocations.
            // Luckily, SplineGraphComponent is designed only really for editing.
            // SplineGraphManager is what is used during runtime, so this allocation is not a concern in practice.
            Dispose();
        }

        void OnDestroy()
        {
#if UNITY_EDITOR
            UnsubscribeUndoRedoPerformed();
#endif
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

#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }

        public void OnBeforeSerialize()
        {
            // Warning: OnBeforeSerialize is the one place where we need this guard.
            // Without it, during a domain reload from scripts recompiling,
            // Dispose() will be triggered from OnDestroy().
            // Later, OnBeforeSerialize() will get hit, with isDeserializationNeeded set to true.
            // This will trigger Verify() to Deserialize the splineGraph, causing Allocator.Persistent allocations.
            // These allocations will never get cleaned up, because Dispose was already run.
            // Additionally, it never makes sense to deserialize, just to reserialize in OnBeforeSerialize().
            if (!isDeserializationNeeded)
            {
                Verify();
            }
        }

        public void OnAfterDeserialize()
        {
            isDeserializationNeeded = true;
        }

#if UNITY_EDITOR
        public Int16 VertexAdd(float3 position, quaternion rotation, float2 scale, float2 leash)
        {
            Verify();

            Int16 vertexIndex = splineGraph.VertexAdd(Allocator.Persistent);

            splineGraph.payload.positions.data[vertexIndex] = position;
            splineGraph.payload.rotations.data[vertexIndex] = rotation;
            splineGraph.payload.scales.data[vertexIndex] = scale;
            splineGraph.payload.leashes.data[vertexIndex] = leash;

            return vertexIndex;
        }

        public void VertexRemove(Int16 vertexIndex)
        {
            Verify();

            splineGraph.VertexRemove(vertexIndex);

            // Need to set the dirty flag here because the call to Verify() above cleared any dirty flags that were possibly set by UndoRecord()
            // and we have just changed our runtime data representation via VertexRemove().
            isDirty = true;
        }

        public void EdgeAdd(Int16 vertexParent, Int16 vertexChild)
        {
            Verify();

            splineGraph.EdgeAdd(vertexParent, vertexChild, Allocator.Persistent);

            // Need to set the dirty flag here because the call to Verify() above cleared any dirty flags that were possibly set by UndoRecord()
            // and we have just changed our runtime data representation via EdgeAdd().
            isDirty = true;
        }

        public void EdgeRemove(Int16 vertexParent, Int16 vertexChild)
        {
            Verify();

            splineGraph.EdgeRemove(vertexParent, vertexChild);

            // Need to set the dirty flag here because the call to Verify() above cleared any dirty flags that were possibly set by UndoRecord()
            // and we have just changed our runtime data representation via EdgeRemove().
            isDirty = true;
        }

        public void VertexMerge(Int16 vertexParent, Int16 vertexChild)
        {
            Verify();

            splineGraph.VertexMerge(vertexParent, vertexChild, Allocator.Persistent);

            // Need to set the dirty flag here because the call to Verify() above cleared any dirty flags that were possibly set by UndoRecord()
            // and we have just changed our runtime data representation via VertexMerge().
            isDirty = true;
        }

        public void UndoRecord(string message, bool isForceNewOperationEnabled = false)
        {
            Verify();

            if (isForceNewOperationEnabled)
            {
                Undo.IncrementCurrentGroup();
            }

            Undo.RecordObject(this, message);
            isDirty = true;
            ++lastDirtyTimestamp;  
        }

        public void BuildCompactGraph()
        {
            Verify();

            var splineGraphCompact = new DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable>(Allocator.Persistent); 
            splineGraph.BuildCompactDirectedGraph(ref splineGraphCompact, Allocator.Persistent);
            splineGraph.Dispose();
            splineGraph = splineGraphCompact;

            // Need to set the dirty flag here because the call to Verify() above cleared any dirty flags that were possibly set by UndoRecord()
            // and we have just changed our runtime data representation via BuildCompactDirectedGraph().
            isDirty = true;
        }

        public void BuildCompactReverseGraph()
        {
            Verify();

            var splineGraphCompact = new DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable>(Allocator.Persistent); 
            splineGraph.BuildCompactReverseDirectedGraph(ref splineGraphCompact, Allocator.Persistent);
            splineGraph.Dispose();
            splineGraph = splineGraphCompact;

            // Need to set the dirty flag here because the call to Verify() above cleared any dirty flags that were possibly set by UndoRecord()
            // and we have just changed our runtime data representation via BuildCompactReverseDirectedGraph().
            isDirty = true;
        }

        public static void DrawSplineGraph(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount)
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
                    float2 vertexLeash = splineGraph.payload.leashes.data[v];
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

                    // Draw leash ellipse by scaling a circle handle.
                    float4x4 leashLocalToWorldMatrix = float4x4.TRS(vertexPosition, vertexRotation, new float3(vertexLeash.x, vertexLeash.y, 1.0f));
                    Handles.matrix = math.mul(splineGraphTransform.localToWorldMatrix, leashLocalToWorldMatrix);
                    Handles.CircleHandleCap(0, float3.zero, quaternion.identity, 1.0f, EventType.Repaint);
                    Handles.matrix = splineGraphTransform.localToWorldMatrix;
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
                    float gizmoSplineSegmentCountInverse = 1.0f / (float)gizmoSplineSegmentCount;
                    for (int s = 0; s < gizmoSplineSegmentCount; ++s)
                    {
                        float t0 = (float)(s + 0) * gizmoSplineSegmentCountInverse;
                        float t1 = (float)(s + 1) * gizmoSplineSegmentCountInverse;

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

                        // Display the spline's curvature at its halfway point along the spline.
                        // {
                        //     // float curvature = SplineMath.EvaluateCurvatureFromT(spline, 0.5f);
                        //     // Handles.Label(labelPosition, "Curvature = " + curvature);
                        //     for (int s = 0; s < gizmoSplineSegmentCount; ++s)
                        //     {
                        //         float t0 = (float)(s + 0) * gizmoSplineSegmentCountInverse;

                        //         float3 samplePosition0 = SplineMath.EvaluatePositionFromT(spline, t0);

                        //         float curvature = SplineMath.EvaluateCurvatureFromT(spline, t0);
                        //         Handles.Label(samplePosition0, "Curvature = " + curvature);
                        //     }
                        // }
                    }
                }
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }
#endif

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Verify();

            if (!isEditingEnabled) { return; }

            // return; // TODO: Remove?
            SplineGraphComponent.DrawSplineGraph(ref splineGraph, transform, gizmoSplineSegmentCount);
        }
#endif
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

        private bool isExtruding = false;

        private static DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> copyScratchWS;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
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

            int gizmoSplineSegmentCountNew = EditorGUILayout.DelayedIntField("Gizmo Spline Segment Count", sgc.gizmoSplineSegmentCount);
            gizmoSplineSegmentCountNew = math.clamp(gizmoSplineSegmentCountNew, 1, int.MaxValue);
            if (gizmoSplineSegmentCountNew != sgc.gizmoSplineSegmentCount)
            {
                sgc.UndoRecord("Edited Spline Graph Spline Segment Count");
                sgc.gizmoSplineSegmentCount = gizmoSplineSegmentCountNew;
            }

            EditorGUILayout.BeginVertical();
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

            if (GUILayout.Button("Build Compact Graph"))
            {
                sgc.UndoRecord("Spline Graph Build Compact Graph", true);

                sgc.BuildCompactGraph();

                // Vertex and Edge indices change when graph is compacted.
                // Selection will no longer be valid.
                selectedIndices.Clear();
                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }

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

            if (GUILayout.Button("Reverse"))
            {
                sgc.UndoRecord("Spline Graph Reverse", true);

                sgc.BuildCompactReverseGraph();

                // Vertex and Edge indices change when graph is compacted.
                // Selection will no longer be valid.
                selectedIndices.Clear();
                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }

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

            if (GUILayout.Button("Deselect All"))
            {
                selectedIndices.Clear();
                Repaint(); // Repaint editor to display selection changes in InspectorGUI.
            }

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

                float2 leash = sgc.splineGraph.payload.leashes.data[v];
                float2 leashNew = EditorGUILayout.Vector2Field("Leash", leash);
                if (math.any(leashNew != leash))
                {
                    sgc.UndoRecord("Edited Spline Graph Vertex Leash");

                    sgc.splineGraph.payload.leashes.data[v] = leashNew;
                    sgc.splineGraph.VertexComputePayloads(v);
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

            EditorGUILayout.EndVertical();

            sgc.Verify();

            serializedObject.ApplyModifiedProperties();

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void OnSceneGUI()
        {
            // If we are currently tumbling the camera, do not attempt to do anything else.
            if (Event.current.alt) { return; }

            serializedObject.Update();

            var sgc = target as SplineGraphComponent;
            sgc.Verify();

            if (!sgc.isEditingEnabled) { return; }

            // return; // TODO: Remove?

            Transform transform = sgc.transform;
            Quaternion rotation = (Tools.pivotRotation == PivotRotation.Local)
                ? transform.rotation
                : Quaternion.identity;

            if (!Event.current.shift || (GUIUtility.hotControl == 0))
            {
                // Detect when user has stopped holding shift and reset extruding state.
                // Also reset extrude state if user is no longer interacting with a control.
                // This allows us to support the case where user holds shift, drags out, releases mouse but keeps holding shift,
                // clicks mouse, drags out, etc.
                // Feels a little jank that we would do this so high up, but it ensures we have all states covered.
                isExtruding = false;
            }

            // Delete selected vertices if delete key is pressed.
            if (Event.current != null 
                && Event.current.isKey 
                && Event.current.type == EventType.KeyUp 
                && (Event.current.keyCode == KeyCode.Delete || Event.current.keyCode == KeyCode.Backspace))
            {
                if ((selectionType == SelectionType.Vertex) && (selectedIndices.Count > 0))
                {
                    sgc.UndoRecord("Spline Graph Vertex Remove", true);
                    Event.current.Use();

                    for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                    {
                        Int16 selectedVertexIndex = selectedIndices[i];

                        sgc.VertexRemove(selectedVertexIndex);
                    }

                    selectedIndices.Clear();
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
                float2 leashOffsetOS = new float2(0.0f, 0.0f);
                for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                {
                    Int16 selectedVertexIndex = selectedIndices[i];

                    float3 vertexPositionOS = sgc.splineGraph.payload.positions.data[selectedVertexIndex];
                    quaternion vertexRotationOS = sgc.splineGraph.payload.rotations.data[selectedVertexIndex];
                    float2 vertexScaleOS = sgc.splineGraph.payload.scales.data[selectedVertexIndex];
                    float2 vertexLeashOS = sgc.splineGraph.payload.leashes.data[selectedVertexIndex];
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

                            // Cache off the position of the current vertex we are rotating.
                            // This will be used to rotate multi-selected vertices about this origin.
                            rotationOffsetOriginOS = vertexPositionOS;


                            // Under multi-selection, rotationOffsetOS will be the same for all points being transformed.
                            break;
                        }
                    }

                    else if (Tools.current == Tool.Scale)
                    {
                        bool isDone = false;
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
                                isDone = true;
                            }
                        }

                        {
                            float handleSize = HandleUtility.GetHandleSize(vertexPositionOS) * 1.0f;
                            float leashNewOSX = Handles.ScaleSlider(vertexLeashOS.x, vertexPositionOS, math.mul(vertexRotationOS, new float3(1.0f, 0.0f, 0.0f)), vertexRotationOS, handleSize, 1e-5f);
                            float leashNewOSY = Handles.ScaleSlider(vertexLeashOS.y, vertexPositionOS, math.mul(vertexRotationOS, new float3(0.0f, 1.0f, 0.0f)), vertexRotationOS, handleSize, 1e-5f);
                            float2 leashNewOS = new float2(leashNewOSX, leashNewOSY);

                            if (math.any(leashNewOS != vertexLeashOS))
                            {
                                leashOffsetOS = new float2(leashNewOSX, leashNewOSY) - vertexLeashOS;

                                // Under multi-selection, leashOffsetOS will be the same for all points being transformed.
                                isDone = true;
                            }
                        }

                        if (isDone) { break; }
                    }
                }

                // 3) Apply transform delta(s) to vertex(s) and register undo.
                if (EditorGUI.EndChangeCheck())
                {
                    if (Tools.current == Tool.Move)
                    {
                        if (Event.current.shift && !isExtruding)
                        {
                            // Extrude selected vertex / vertices along offset.
                            sgc.UndoRecord("Edited Spline Graph Extude", true);

                            isExtruding = true;

                            for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                            {
                                Int16 selectedVertexIndex = selectedIndices[i];

                                float3 vertexPositionOS = sgc.splineGraph.payload.positions.data[selectedVertexIndex];
                                vertexPositionOS = transform.TransformPoint(vertexPositionOS);
                                vertexPositionOS += positionOffsetOS;
                                vertexPositionOS = transform.InverseTransformPoint(vertexPositionOS);

                                quaternion rotationParent = sgc.splineGraph.payload.rotations.data[selectedVertexIndex];
                                float2 scaleParent = sgc.splineGraph.payload.scales.data[selectedVertexIndex];
                                float2 leashParent = sgc.splineGraph.payload.leashes.data[selectedVertexIndex];

                                Int16 selectedVertexChildIndex = sgc.VertexAdd(vertexPositionOS, rotationParent, scaleParent, leashParent);
                                sgc.EdgeAdd(selectedVertexIndex, selectedVertexChildIndex);

                                // Update the selection to the new child vertex instead of the parent.
                                // This makes it convenient to perform multiple extrusions.
                                selectedIndices[i] = selectedVertexChildIndex;
                            }
                        }
                        else
                        {
                            // Standard move of currently selected vertex / vertices.
                            if (isExtruding)
                            {
                                sgc.UndoRecord("Edited Spline Graph Extude");
                            }
                            else
                            {
                                sgc.UndoRecord("Edited Spline Graph Vertex Position");
                            }
                            
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

                            // In order to support the case where we have multi-selected vertices
                            // we need to rotate the positions of all our selected vertices about the vertex
                            // whos handle is currently being interacted with.
                            float3 vertexPositionOS = sgc.splineGraph.payload.positions.data[selectedVertexIndex];
                            vertexPositionOS = transform.TransformPoint(vertexPositionOS);
                            vertexPositionOS -= rotationOffsetOriginOS;

                            vertexPositionOS = math.mul(rotationOffsetOS, vertexPositionOS);

                            vertexPositionOS += rotationOffsetOriginOS;
                            vertexPositionOS = transform.InverseTransformPoint(vertexPositionOS);
                            sgc.splineGraph.payload.positions.data[selectedVertexIndex] = vertexPositionOS;
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

                            float2 vertexLeashOS = sgc.splineGraph.payload.leashes.data[selectedVertexIndex];
                            vertexLeashOS = math.max(0.0f, vertexLeashOS + leashOffsetOS);
                            sgc.splineGraph.payload.leashes.data[selectedVertexIndex] = vertexLeashOS;
                        }

                        for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                        {
                            sgc.splineGraph.VertexComputePayloads(selectedIndices[i]);
                        }
                    }
                }
            }

            // WARNING: Make sure to draw selection handles after all modeling logic.
            // This is necessary to make the extrusion hotkey work.
            // Previously, when a vertex was extruded, that would cause a new Handles.Button() to be drawn for that vertex.
            // That would in turn cause the handle IDs to change, forcing a deselection of the current Handles.PositionHandle().
            // This is very brittle, keep an eye on this area of code.

            // Select vertex(s).
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

            // Now that we have potentially performed some transforms to the spline graph, tell it to serialize (just to be safe).
            // Otherwise, this serialization would happen next frame (which is probably fine?)
            sgc.Verify();

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
