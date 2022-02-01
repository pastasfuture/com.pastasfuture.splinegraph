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
        public SplineGraphUserBlobSchemaScriptableObject splineGraphUserBlobSchema = null;
        public DirectedGraphSerializable splineGraphSerializable = new DirectedGraphSerializable();
        public SplineGraphPayloadSerializable splineGraphPayloadSerializable = new SplineGraphPayloadSerializable();
        public SplineGraphBinaryDataScriptableObject splineGraphBinaryData = null;
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
            if (!isDirty)
            {
                ++lastDirtyTimestamp;

                // When an undo or redo is performed, the serialized data becomes the source of truth, and needs to be deserialized.
                // If we do not perform this, runtime data will get out of sync. Its especially noticable when SplineGraphBinaryData is used, as the changes in the binary data will not get reflected in the runtime.
                // Note: We do not know if the undo or redo event was actually performed on ourselves. Keep an eye on this. It seems ok since we are just flagging the system to say the serialized data is the truth,
                // which seems fine if the object isnt dirty.
                isDeserializationNeeded = true;
            }
            
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

                if (splineGraphBinaryData != null)
                {
                    splineGraphSerializable = null;
                    splineGraphPayloadSerializable = null;
                    splineGraph.Deserialize(ref splineGraphBinaryData.splineGraphSerializable, ref splineGraphBinaryData.splineGraphPayloadSerializable, Allocator.Persistent);
                }
                else
                {
                    splineGraph.Deserialize(ref splineGraphSerializable, ref splineGraphPayloadSerializable, Allocator.Persistent);
                }
            }

            if (splineGraphUserBlobSchema != null)
            {
                if (splineGraphUserBlobSchema.GetVersion() > splineGraph.payload.userBlobSchemaVersion)
                {
                    splineGraphUserBlobSchema.Migrate(ref splineGraph, Allocator.Persistent);
                    isDirty = true;
                }
            }
            else
            {
                if (splineGraph.payload.userBlobSchemaVersion != 0)
                {
                    splineGraph.payload.ClearSchema();
                    isDirty = true;
                }
            }

            if (isDirty)
            {
                isDirty = false;

                if (splineGraphBinaryData != null)
                {
                    splineGraphSerializable = null;
                    splineGraphPayloadSerializable = null;

                    splineGraph.Serialize(ref splineGraphBinaryData.splineGraphSerializable, ref splineGraphBinaryData.splineGraphPayloadSerializable);
                }
                else
                {
                    splineGraph.Serialize(ref splineGraphSerializable, ref splineGraphPayloadSerializable);
                }
                

#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
                if (splineGraphBinaryData != null)
                {
                    EditorUtility.SetDirty(splineGraphBinaryData);
                }
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

        private UnityEngine.Object[] undoObjectsScratch = new UnityEngine.Object[2];

        public void UndoRecord(string message, bool isForceNewOperationEnabled = false)
        {
            Verify();

            if (isForceNewOperationEnabled)
            {
                Undo.IncrementCurrentGroup();
            }

            if (splineGraphBinaryData != null)
            {
                undoObjectsScratch[0] = this;
                undoObjectsScratch[1] = splineGraphBinaryData;
                Undo.RecordObjects(undoObjectsScratch, message);
            }
            else
            {
                Undo.RecordObject(this, message);
            }
            
            isDirty = true;
            ++lastDirtyTimestamp;  
        }

        public void BuildCompactGraph()
        {
            Verify();

            var splineGraphCompact = new DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable>(Allocator.Persistent);
            if (splineGraph.payload.userBlobSchemaVersion != 0)
            {
                splineGraphCompact.payload.SetSchema(ref splineGraph.payload.userBlobVertex.schema, ref splineGraph.payload.userBlobEdge.schema, splineGraph.payload.userBlobSchemaVersion, Allocator.Persistent);
            }
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
            if (splineGraph.payload.userBlobSchemaVersion != 0)
            {
                splineGraphCompact.payload.SetSchema(ref splineGraph.payload.userBlobVertex.schema, ref splineGraph.payload.userBlobEdge.schema, splineGraph.payload.userBlobSchemaVersion, Allocator.Persistent);
            }
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

                //Place vertex index above vertex position.
                //{
                //    float3 vertexPosition = splineGraph.payload.positions.data[v];
                //    quaternion vertexRotation = splineGraph.payload.rotations.data[v];
                //    float handleSize = HandleUtility.GetHandleSize(vertexPosition);
                //    const float HANDLE_DISPLAY_SIZE = 0.125f;
                //    float vertexLabelSize = handleSize * HANDLE_DISPLAY_SIZE;
                //    float3 labelPosition = vertexPosition + math.mul(vertexRotation, new float3(0.0f, vertexLabelSize * 2.0f, 0.0f));
                //    // Handles.Label(labelPosition, "" + v);
                //}

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

                    //Place arrow in center of spline segment.
                    {
                        float3 arrowPosition = SplineMath.EvaluatePositionFromT(spline, 0.5f);
                        quaternion arrowRotation = SplineMath.EvaluateRotationFromT(spline, 0.5f);

                        float handleSize = HandleUtility.GetHandleSize(arrowPosition);
                        const float HANDLE_DISPLAY_SIZE = 0.125f;
                        float arrowSize = handleSize * HANDLE_DISPLAY_SIZE;
                        Handles.ConeHandleCap(0, arrowPosition, arrowRotation, arrowSize, EventType.Repaint);

                        //float3 labelPosition = arrowPosition + math.mul(arrowRotation, new float3(0.0f, arrowSize * 2.0f, 0.0f));
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
}
