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
    public class SplineGraphManager : MonoBehaviour, ISerializationCallbackReceiver
    {
        [System.NonSerialized] public DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph;
        
        [System.NonSerialized] public bool isEditingEnabled = false;
        [System.NonSerialized] public bool isRenderingEnabled = false;
        [System.NonSerialized] public bool isAutoUpdateEnabled = false;
        public bool isReadOnly = false;
        public int type = 0;
        public SplineGraphUserBlobSchemaScriptableObject splineGraphUserBlobSchema = null;
        public DirectedGraphSerializable splineGraphSerializable = new DirectedGraphSerializable();
        public SplineGraphPayloadSerializable splineGraphPayloadSerializable = new SplineGraphPayloadSerializable();
        public SplineGraphBinaryDataScriptableObject splineGraphBinaryData = null;
        public int gizmoSplineSegmentCount = 4;

        private bool isDeserializationNeeded = true;
        [System.NonSerialized] public bool isDirty = true;
        [System.NonSerialized] public int lastDirtyTimestamp = 0; // Do not need actual time, just a counter.

        [System.NonSerialized] public bool debugIsSpawnEnabled = false;
        [System.NonSerialized] public float3 debugPosition;
        [System.NonSerialized] public float debugVelocity;
        [System.NonSerialized] public bool debugIsReverse;
        [System.NonSerialized] public float debugSpawnDeltaTime = float.MaxValue;
        [System.NonSerialized] public List<SplineMath.SplineGraphFollowState> debugFollowStates = new List<SplineMath.SplineGraphFollowState>();
        [System.NonSerialized] public List<Unity.Mathematics.Random> debugFollowRandoms = new List<Unity.Mathematics.Random>();
        [System.NonSerialized] public Unity.Mathematics.Random randomSeedGenerator = new Unity.Mathematics.Random(12371923);
        
        public DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> GetSplineGraph()
        {
            Verify();
            return splineGraph;
        }

        public void SetAndCopyDataFromSplineGraphBinaryDataScriptableObject(SplineGraphBinaryDataScriptableObject splineGraphBinaryDataNext)
        {
            if (splineGraphBinaryData != null && splineGraphBinaryDataNext == null)
            {
                // Asset was unassigned, clear out the serialized data.
                splineGraphSerializable = new DirectedGraphSerializable();
                splineGraphPayloadSerializable = new SplineGraphPayloadSerializable();
                splineGraphBinaryData = null;
                isDeserializationNeeded = true;
            }
            else
            {
                // Asset was assigned. Overwrite any existing data with the binary data.
                splineGraphSerializable = null;
                splineGraphPayloadSerializable = null;
                splineGraphBinaryData = splineGraphBinaryDataNext;
                isDeserializationNeeded = true;
            }

            Verify();

#if UNITY_EDITOR
            // Need to force SetDirty here. Normally, Verify() would handle this, however, it skips SetDirty if the asset is flagged as readonly.
            // We want the contents of splineGraphBinaryData to be treated as readonly, but we want the assignment of the scriptable object reference itself to be stored to disk.
            EditorUtility.SetDirty(this);
#endif
        }

        public void SetSplineGraphBinaryDataScriptableObjectAsSource(SplineGraphUserBlobSchemaScriptableObject userBlobSchema, SplineGraphBinaryDataScriptableObject binaryData)
        {
            splineGraphUserBlobSchema = userBlobSchema;
            splineGraphBinaryData = binaryData;
            splineGraphSerializable = null;

            isReadOnly = (binaryData != null) ? true : isReadOnly;
            isDirty = false;
            isDeserializationNeeded = (binaryData != null);

            if (binaryData == null)
            {
                splineGraph.Clear();
            }

            Verify();
            
#if UNITY_EDITOR
            // Need to force SetDirty here. Normally, Verify() would handle this, however, it skips SetDirty if the asset is flagged as readonly.
            // We want the contents of splineGraphBinaryData to be treated as readonly, but we want the assignment of the scriptable object reference itself to be stored to disk.
            EditorUtility.SetDirty(this);
#endif
        }

        void OnEnable()
        {
            Verify();
        }

        void OnDisable()
        {
            Verify();

            // Need to Dispose() inside of OnDisable(), because OnDestroy() is not called on domain reloads.
            // Only calling Dispose() inside of OnDestroy() would cause memory leaks between domain reloads.
            // This downside of calling Dispose() here is that enabling / disabling this component will trigger
            // (native) memory allocations.
            // We need to be mindful of this, and not enable / disable SplineGraphManager often.
            // In practice, we should only trigger this on level loads.
            Dispose();
        }

        void OnDestroy()
        {
            Dispose();
        }

        void Dispose()
        {
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

            if (isDirty && !isReadOnly)
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
        private static UnityEngine.Object[] undoObjectsScratch = new UnityEngine.Object[2];
        public void UndoRecord(string message)
        {
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
            
            this.isDirty = true;
            ++this.lastDirtyTimestamp;
        }
#endif

#if UNITY_EDITOR
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

        public void VertexWeldAllWithinThreshold()
        {
            Verify();

            for (Int16 v0 = 0, v0Count = (Int16)splineGraph.vertices.count; v0 < v0Count; ++v0)
            {
                DirectedVertex vertex0 = splineGraph.vertices.data[v0];
                if (vertex0.IsValid() == 0) { continue; }

                float3 positionParent = splineGraph.payload.positions.data[v0];
                quaternion rotationParent = splineGraph.payload.rotations.data[v0];
                float2 scaleParent = splineGraph.payload.scales.data[v0];
                float2 leashParent = splineGraph.payload.leashes.data[v0];

                for (Int16 v1 = (Int16)(v0 + 1); v1 < v0Count; ++v1)
                {
                    DirectedVertex vertex1 = splineGraph.vertices.data[v1];
                    if (vertex1.IsValid() == 0) { continue; }

                    float3 positionChild = splineGraph.payload.positions.data[v1];
                    quaternion rotationChild = splineGraph.payload.rotations.data[v1];
                    float2 scaleChild = splineGraph.payload.scales.data[v1];
                    float2 leashChild = splineGraph.payload.leashes.data[v1];

                    float positionDelta2 = math.lengthsq(positionParent - positionChild);

                    float positionMagnitude = math.max(math.cmax(math.abs(positionParent)), math.cmax(math.abs(positionChild)));

                    float leashDelta2 = math.lengthsq(leashParent - leashChild);

                    float epsilon = 1e-2f;//(positionMagnitude < 2.0f) ? 1e-5f : (math.log2(positionMagnitude) * 1e-5f);

                    if (positionDelta2 > (epsilon * epsilon)) { continue; }
                    if (leashDelta2 > (epsilon * epsilon)) { continue; }
                    if (!(math.any(math.abs(rotationParent.value - rotationChild.value) < 1e-2f)
                        || math.any(math.abs(rotationParent.value + rotationChild.value) < 1e-2f)))
                    {
                        // Quaternions have double coverage so need to compare component wise equivalence for positive and negative operand b. 
                        Debug.Log("Failed to weld because of rotation: {" + rotationParent.value.x + ", " + rotationParent.value.y + ", " + rotationParent.value.z + ", " + rotationParent.value.w + "} and {" + rotationChild.value.x + ", " + rotationChild.value.y + ", " + rotationChild.value.z + ", " + rotationChild.value.w + "}");
                        
                        float3 forwardParent = math.mul(rotationParent, new float3(0.0f, 0.0f, 1.0f));
                        float3 forwardChild = math.mul(rotationChild, new float3(0.0f, 0.0f, 1.0f));
                        Debug.Log("Failed to weld forward: {" + forwardParent.x + ", " + forwardParent.y + ", " + forwardParent.z + "} and {" + forwardChild.x + ", " + forwardChild.y + ", " + forwardChild.z + "}");
                        
                        float3 tangentParent = math.mul(rotationParent, new float3(1.0f, 0.0f, 0.0f));
                        float3 tangentChild = math.mul(rotationChild, new float3(1.0f, 0.0f, 0.0f));
                        Debug.Log("Failed to weld tangent: {" + tangentParent.x + ", " + tangentParent.y + ", " + tangentParent.z + "} and {" + tangentChild.x + ", " + tangentChild.y + ", " + tangentChild.z + "}");
                        
                        float3 bitangentParent = math.mul(rotationParent, new float3(0.0f, 1.0f, 0.0f));
                        float3 bitangentChild = math.mul(rotationChild, new float3(0.0f, 1.0f, 0.0f));
                        Debug.Log("Failed to weld bitangent: {" + bitangentParent.x + ", " + bitangentParent.y + ", " + bitangentParent.z + "} and {" + bitangentChild.x + ", " + bitangentChild.y + ", " + bitangentChild.z + "}");
                        
                        continue; 
                    }

                    // Average vertex payload data:
                    splineGraph.payload.positions.data[v0] = (positionParent * 0.5f + positionChild * 0.5f);
                    splineGraph.payload.rotations.data[v0] = math.slerp(rotationParent, rotationChild, 0.5f);
                    
                    // Weighted average vertex payload scale and leash data.
                    {
                        float v0ParentCount = (float)splineGraph.VertexComputeParentCount(v0);
                        float v0ChildCount = (float)splineGraph.VertexComputeChildCount(v0);
                        float v1ParentCount = (float)splineGraph.VertexComputeParentCount(v1);
                        float v1ChildCount = (float)splineGraph.VertexComputeChildCount(v1);

                        float scaleX = ((v0ParentCount + v1ParentCount) > 0.0f)
                            ? ((splineGraph.payload.scales.data[v0].x * v0ParentCount + splineGraph.payload.scales.data[v1].x * v1ParentCount) / (v0ParentCount + v1ParentCount))
                            : (splineGraph.payload.scales.data[v0].x);
                        float scaleY = ((v0ChildCount + v1ChildCount) > 0.0f)
                            ? ((splineGraph.payload.scales.data[v0].y * v0ChildCount + splineGraph.payload.scales.data[v1].y * v1ChildCount) / (v0ChildCount + v1ChildCount))
                            : (splineGraph.payload.scales.data[v0].y);
                        splineGraph.payload.scales.data[v0] = new float2(scaleX, scaleY);

                        float leashX = ((v0ParentCount + v1ParentCount) > 0.0f)
                            ? ((splineGraph.payload.leashes.data[v0].x * v0ParentCount + splineGraph.payload.leashes.data[v1].x * v1ParentCount) / (v0ParentCount + v1ParentCount))
                            : (splineGraph.payload.leashes.data[v0].x);
                        float leashY = ((v0ChildCount + v1ChildCount) > 0.0f)
                            ? ((splineGraph.payload.leashes.data[v0].y * v0ChildCount + splineGraph.payload.leashes.data[v1].y * v1ChildCount) / (v0ChildCount + v1ChildCount))
                            : (splineGraph.payload.leashes.data[v0].y);
                        splineGraph.payload.leashes.data[v0] = new float2(leashX, leashY);
                    }
                    splineGraph.VertexMerge(v0, v1, Allocator.Persistent);

                    // Need to set the dirty flag here because the call to Verify() above cleared any dirty flags that were possibly set by UndoRecord()
                    // and we have just changed our runtime data representation via welding.
                    isDirty = true;
                }

            }
        }

        public void VertexWeldAllRedundant()
        {
            Verify();

            for (Int16 v0 = 0, v0Count = (Int16)splineGraph.vertices.count; v0 < v0Count; ++v0)
            {
                DirectedVertex vertex0 = splineGraph.vertices.data[v0];
                if (vertex0.IsValid() == 0) { continue; }

                // For now, just going to handle the easy, and probably majority case of a spline that looks like:
                // vertex0 -------------------> vertex0Child -------------------> vertex0ChildChild
                // where vertex0, and vertex0Child only have 1 child and vertex0 -> vertex0Child -> vertex0ChildChild describes a straight line.

                Int16 vertex0ChildCount = splineGraph.VertexComputeChildCount(v0);
                if (vertex0ChildCount != 1) { continue; }

                Int16 vertex0ChildIndex = splineGraph.edgePoolChildren.data[vertex0.childHead].vertexIndex;
                Debug.Assert(vertex0ChildIndex >= 0 && vertex0ChildIndex < v0Count);
                DirectedVertex vertex0Child = splineGraph.vertices.data[vertex0ChildIndex];
                Debug.Assert(vertex0Child.IsValid() == 1);

                Int16 vertex0ChildChildCount = splineGraph.VertexComputeChildCount(vertex0ChildIndex);
                if (vertex0ChildChildCount != 1) { continue; }
                Int16 vertex0ChildParentCount = splineGraph.VertexComputeParentCount(vertex0ChildIndex);
                if (vertex0ChildParentCount != 1) { continue; }

                // We have now verified that vertex0 -> vertex0Child -> vertex0ChildChild describe a single path.
                // Now need to verify that this path is a straight line.
                Int16 vertex0ChildChildIndex = splineGraph.edgePoolChildren.data[vertex0Child.childHead].vertexIndex;
                Debug.Assert(vertex0ChildChildIndex >= 0 && vertex0ChildChildIndex < v0Count);
                DirectedVertex vertex0ChildChild = splineGraph.vertices.data[vertex0ChildChildIndex];
                Debug.Assert(vertex0ChildChild.IsValid() == 1);

                float3 vertex0Position = splineGraph.payload.positions.data[v0];
                float3 vertex0ChildPosition = splineGraph.payload.positions.data[vertex0ChildIndex];
                float3 vertex0ChildChildPosition = splineGraph.payload.positions.data[vertex0ChildChildIndex];

                float3 vertex0ChildFromVertex0Direction = math.normalize(vertex0ChildPosition - vertex0Position);
                float3 vertex0ChildChildFromVertex0Direction = math.normalize(vertex0ChildChildPosition - vertex0Position);
                float3 vertex0ChildChildFromVertex0ChildDirection = math.normalize(vertex0ChildChildPosition - vertex0ChildPosition);

                // Note: Mathematically, we only need to actually check the first of these two dot products.
                // For precision reasons with large distances between vertices, we check both to be extra conservative.
                if (math.dot(vertex0ChildFromVertex0Direction, vertex0ChildChildFromVertex0Direction) < 0.9999f
                    || math.dot(vertex0ChildFromVertex0Direction, vertex0ChildChildFromVertex0ChildDirection) < 0.9999f)
                {
                    // Positions alone do not line up. Cannot be a linear path:
                    //
                    // vertex0 ------------> vertexChild0 -
                    //                                      \
                    //                                       \
                    //                                        \
                    //                                 vertex0ChildChild
                    continue;
                }

                quaternion vertex0Rotation = splineGraph.payload.rotations.data[v0];
                quaternion vertex0ChildRotation = splineGraph.payload.rotations.data[vertex0ChildIndex];
                quaternion vertex0ChildChildRotation = splineGraph.payload.rotations.data[vertex0ChildChildIndex];

                float3 vertex0Forward = math.mul(vertex0Rotation, new float3(0.0f, 0.0f, 1.0f));
                float3 vertex0ChildForward = math.mul(vertex0ChildRotation, new float3(0.0f, 0.0f, 1.0f));
                float3 vertex0ChildChildForward = math.mul(vertex0ChildChildRotation, new float3(0.0f, 0.0f, 1.0f));

                if (math.dot(vertex0Forward, vertex0ChildForward) < 0.999f)
                {
                    // Not pointing in the same direction. Cannot be a linear path.
                    continue;
                }

                if (math.dot(vertex0Forward, vertex0ChildChildForward) < 0.999f)
                {
                    // Not pointing in the same direction. Cannot be a linear path.
                    continue;
                }

                if (math.abs(math.dot(vertex0Forward, vertex0ChildFromVertex0Direction)) < 0.999f)
                {
                    // Vertices not pointing directly toward eachother. Cannot be a linear path.
                    continue;
                }

                float2 vertex0LeashOS = splineGraph.payload.leashes.data[v0];
                float2 vertex0ChildLeashOS = splineGraph.payload.leashes.data[vertex0ChildIndex];
                float3 vertex0LeashWS = math.mul(vertex0Rotation, new float3(vertex0LeashOS, 0.0f));
                float3 vertex0ChildLeashWS = math.mul(vertex0Rotation, new float3(vertex0ChildLeashOS, 0.0f));
                if (math.any(math.abs(vertex0LeashWS - vertex0ChildLeashWS) > 1e-3f))
                {
                    // Leash is not identical, cannot be a linear path.
                    continue;
                }

                float2 vertex0ChildChildLeashOS = splineGraph.payload.leashes.data[vertex0ChildChildIndex];
                float3 vertex0ChildChildLeashWS = math.mul(vertex0ChildChildRotation, new float3(vertex0ChildChildLeashOS, 0.0f));
                if (math.any(math.abs(vertex0ChildLeashWS - vertex0ChildChildLeashWS) > 1e-3f))
                {
                    // Leash is not identical, cannot be a linear path.
                    continue;
                }

                // Found a linear path!
                // Note, we do not actually care about a scale differences.
                // All we care about is making sure scaleIn at vertex0 is maintained, and scaleOut at vertex0ChildChildRotation is maintained.
                // This will automatically happen just by merging vertex0Child into vertex0.
                splineGraph.VertexMerge(v0, vertex0ChildIndex, Allocator.Persistent);

                // Need to set the dirty flag here because the call to Verify() above cleared any dirty flags that were possibly set by UndoRecord()
                // and we have just changed our runtime data representation via welding.
                isDirty = true;

                // Since we merged, we can stay at v0 and test again to see if there is a new merge case.
                --v0;
            }
        }

        public void BuildGraphFromInstances()
        {
            Verify();

            splineGraph.Clear();
            if (splineGraphUserBlobSchema != null)
            {
                NativeArray<SplineGraphUserBlob.Scheme> schemaVertex = default;
                splineGraphUserBlobSchema.CopyVertexSchema(ref schemaVertex, Allocator.Temp);
                NativeArray<SplineGraphUserBlob.Scheme> schemaEdge = default;
                splineGraphUserBlobSchema.CopyEdgeSchema(ref schemaEdge, Allocator.Temp);
                splineGraph.payload.SetSchema(ref schemaVertex, ref schemaEdge, splineGraphUserBlobSchema.GetVersion(), Allocator.Persistent);
                schemaVertex.Dispose();
                schemaEdge.Dispose();
            }

            // 1) Append all graphs into single graph.
            for (int i = 0, iCount = SplineGraphComponent.instances.Count; i < iCount; ++i)
            {
                SplineGraphComponent instance = SplineGraphComponent.instances[i];
                instance.Verify();

                if (instance.type != type) { continue; }
                if (instance.splineGraphUserBlobSchema != splineGraphUserBlobSchema) { continue; }

                Int16 vertexStart = (Int16)splineGraph.vertices.count;
                splineGraph.PushDirectedGraph(ref instance.splineGraph, Allocator.Persistent);
                Int16 vertexEnd = (Int16)splineGraph.vertices.count;

                // Transform vertices into splineGraphManager's coordinate system:
                Transform instanceTransform = instance.GetComponent<Transform>();
                for (Int16 v = vertexStart; v < vertexEnd; ++v)
                {
                    float3 position = splineGraph.payload.positions.data[v];
                    quaternion rotation = splineGraph.payload.rotations.data[v];
                    float2 scale = splineGraph.payload.scales.data[v];
                    float2 leash = splineGraph.payload.leashes.data[v];

                    float3 forwardOS = math.mul(rotation, new float3(0.0f, 0.0f, 1.0f));
                    float3 tangentOS = math.mul(rotation, new float3(1.0f, 0.0f, 0.0f));
                    float3 bitangentOS = math.mul(rotation, new float3(0.0f, 1.0f, 0.0f));
                    float3 forwardWS = instanceTransform.TransformVector(forwardOS);
                    float3 tangentWS = instanceTransform.TransformVector(tangentOS);
                    float3 bitangentWS = instanceTransform.TransformVector(bitangentOS);
                    float3 forwardMOS = transform.InverseTransformVector(forwardWS);
                    float3 tangentMOS = transform.InverseTransformVector(tangentWS);
                    float3 bitangentMOS = transform.InverseTransformVector(bitangentWS);
                    float mosFromInstanceOSScale = math.length(forwardMOS);
                    float2 mosFromInstanceOSLeashScale = new float2(
                        math.length(tangentMOS),
                        math.length(bitangentMOS)
                    );

                    // Construct manager-object-space rotation from transformed frame, so that scale can be accounted for in final rotation.
                    // In particular, we care about this for using -1 scale to perform mirroring of graph in X, Y, or Z.
                    forwardMOS = math.normalize(forwardMOS);
                    bitangentMOS = math.normalize(bitangentMOS);
                    quaternion rotationMOS = Quaternion.LookRotation(forwardMOS, bitangentMOS);

                    // Manager-Object-space from world-space * World-space from Instance-Object-space
                    position = instanceTransform.TransformPoint(position);
                    position = transform.InverseTransformPoint(position);
                    rotation = rotationMOS;
                    scale = scale * mosFromInstanceOSScale;
                    leash = leash * mosFromInstanceOSLeashScale;

                    splineGraph.payload.positions.data[v] = position;
                    splineGraph.payload.rotations.data[v] = rotation;
                    splineGraph.payload.scales.data[v] = scale;
                    splineGraph.payload.leashes.data[v] = leash;
                }
                for (Int16 v = vertexStart; v < vertexEnd; ++v)
                {
                    splineGraph.payload.VertexComputePayloads(ref splineGraph, v);
                }

            }

            // TODO: MAKE WELDING RESPECT USER BLOB VALUES.
            // 2) Weld vertices.
            //VertexWeldAllWithinThreshold();

            // 3)
            //VertexWeldAllRedundant();

            // 4) Compact.
            //BuildCompactGraph();

            // Need to set the dirty flag here because the call to Verify() above cleared any dirty flags that were possibly set by UndoRecord()
            // and we have just changed our runtime data representation.
            isDirty = true;

        }
#endif // #if UNITY_EDITOR

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Verify();

            if (!isRenderingEnabled) { return; }

            // return; // TODO: Remove?

            SplineGraphComponent.DrawSplineGraph(ref splineGraph, transform, gizmoSplineSegmentCount);

            // Debug only:
            if (!isEditingEnabled) { return; }
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
#endif

#if UNITY_EDITOR
        // Debug only:
        void Update()
        {
            Verify();

            if (!isEditingEnabled) { return; }

            // Debug.Log("debugSpawnDeltaTime = " + debugSpawnDeltaTime);
            debugSpawnDeltaTime += Time.deltaTime;
            if (debugIsSpawnEnabled && (debugSpawnDeltaTime > 1.0f))
            {
                debugSpawnDeltaTime = 0;

                int isReverse = debugIsReverse ? 1 : 0;
                
                SplineMath.FindTFromClosestPointOnSplineGraph(
                    out float t,
                    out float d,
                    out Int16 edgeIndex,
                    isReverse,
                    debugPosition,
                    splineGraph.vertices.data,
                    splineGraph.vertices.count,
                    splineGraph.edgePoolChildren.data,
                    splineGraph.edgePoolParents.data,
                    splineGraph.payload.edgeParentToChildSplines.data,
                    splineGraph.payload.edgeChildToParentSplines.data
                );

                SplineMath.SplineGraphFollowState state = new SplineMath.SplineGraphFollowState(t, edgeIndex, 0, isReverse);
                // Debug.Log("Spawning at:" + state.DebugString());

                debugFollowStates.Add(state);

                UInt32 seed = randomSeedGenerator.NextUInt();
                while (seed == 0) { seed = randomSeedGenerator.NextUInt(); }
                debugFollowRandoms.Add(new Unity.Mathematics.Random(seed));
            }

            for (int i = 0; i < debugFollowStates.Count; )
            {
                SplineMath.SplineGraphFollowState state = debugFollowStates[i];
                Unity.Mathematics.Random random = debugFollowRandoms[i];

                float delta = Time.deltaTime * debugVelocity;

                SplineMath.AdvanceTFromDelta(
                    ref state,
                    ref random,
                    delta,
                    splineGraph.vertices.data,
                    splineGraph.vertices.count,
                    splineGraph.edgePoolChildren.data,
                    splineGraph.edgePoolParents.data,
                    splineGraph.payload.edgeParentToChildSplines.data,
                    splineGraph.payload.edgeChildToParentSplines.data
                );

                // Debug.Log("Updating at: " + state.DebugString());

                if (state.DecodeIsComplete() == 1)
                {
                    debugFollowStates.RemoveAt(i);
                    debugFollowRandoms.RemoveAt(i);

                }
                else
                {
                    debugFollowStates[i] = state;
                    debugFollowRandoms[i] = random;
                    ++i;
                }
            }
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SplineGraphManager))]
    public class SplineGraphManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var sgm = target as SplineGraphManager;
            sgm.Verify();

            EditorGUILayout.BeginVertical();

            bool isEditingEnabledNew = EditorGUILayout.Toggle("Is Editing Enabled", sgm.isEditingEnabled);
            if (isEditingEnabledNew != sgm.isEditingEnabled)
            {
                sgm.UndoRecord("Toggled Spline Graph Manager isEditingEnabled");
                sgm.isEditingEnabled = isEditingEnabledNew;
            }
            if (!sgm.isEditingEnabled)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            bool isReadOnlyNew = EditorGUILayout.Toggle("Read-Only", sgm.isReadOnly);
            if (isReadOnlyNew != sgm.isReadOnly)
            {
                sgm.UndoRecord("Toggled Spline Graph Manager isReadOnly");
                sgm.isReadOnly = isReadOnlyNew;
            }

            var splineGraphUserBlobSchemaNext = EditorGUILayout.ObjectField("User Blob Schema", sgm.splineGraphUserBlobSchema, typeof(SplineGraphUserBlobSchemaScriptableObject), false) as SplineGraphUserBlobSchemaScriptableObject;
            if (splineGraphUserBlobSchemaNext != sgm.splineGraphUserBlobSchema)
            {
                sgm.UndoRecord("Edited Spline Graph Manager User Blob Schema");
                sgm.splineGraphUserBlobSchema = splineGraphUserBlobSchemaNext;

                NativeArray<SplineGraphUserBlob.Scheme> schemaVertex = default;
                splineGraphUserBlobSchemaNext.CopyVertexSchema(ref schemaVertex, Allocator.Temp);
                NativeArray<SplineGraphUserBlob.Scheme> schemaEdge = default;
                splineGraphUserBlobSchemaNext.CopyEdgeSchema(ref schemaEdge, Allocator.Temp);
                sgm.splineGraph.payload.SetSchema(ref schemaVertex, ref schemaEdge, splineGraphUserBlobSchemaNext.GetVersion(), Allocator.Persistent);
                schemaVertex.Dispose();
                schemaEdge.Dispose();
            }

            OnInspectorGUISplineGraphBinaryDataTool(sgm);

            bool isRenderingEnabledNew = EditorGUILayout.Toggle("Is Rendering Enabled", sgm.isRenderingEnabled);
            if (isRenderingEnabledNew != sgm.isRenderingEnabled)
            {
                sgm.UndoRecord("Toggled Spline Graph Manager isRenderingEnabled");
                sgm.isRenderingEnabled = isRenderingEnabledNew;
            }

            if (!sgm.isReadOnly)
            {
                bool isAutoUpdateEnabledNew = EditorGUILayout.Toggle("Is Auto Update Enabled", sgm.isAutoUpdateEnabled);
                if (isAutoUpdateEnabledNew != sgm.isAutoUpdateEnabled)
                {
                    sgm.UndoRecord("Toggled Spline Graph Manager isAutoUpdateEnabled");
                    sgm.isAutoUpdateEnabled = isAutoUpdateEnabledNew;
                }
            }

            sgm.debugIsSpawnEnabled = EditorGUILayout.Toggle("Debug Is Spawn Enabled", sgm.debugIsSpawnEnabled);
            sgm.debugPosition = EditorGUILayout.Vector3Field("Debug Position", sgm.debugPosition);
            sgm.debugVelocity = EditorGUILayout.FloatField("Debug Velocity", sgm.debugVelocity);
            sgm.debugIsReverse = EditorGUILayout.Toggle("Debug Is Reverse", sgm.debugIsReverse);

            if (!sgm.isReadOnly)
            {
                int typeNew = EditorGUILayout.DelayedIntField("Type", sgm.type);
                typeNew = math.clamp(typeNew, 0, int.MaxValue);
                if (typeNew != sgm.type)
                {
                    sgm.UndoRecord("Edited Spline Graph Manager Type");
                    sgm.type = typeNew;
                }

                if (GUILayout.Button("Build Graph From Instances"))
                {
                    sgm.UndoRecord("Spline Graph Manager Build Graph From Instances");

                    sgm.BuildGraphFromInstances();
                }
            }

            if (sgm.isRenderingEnabled)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    for (Int16 v = 0, vCount = (Int16)sgm.splineGraph.vertices.count; v < vCount; ++v)
                    {
                        DirectedVertex vertex = sgm.splineGraph.vertices.data[v];
                        if (vertex.IsValid() == 0) { continue; }

                        EditorGUILayout.BeginVertical();

                        // TODO: Convert these input fields to read-only fields.
                        float3 position = sgm.splineGraph.payload.positions.data[v];
                        EditorGUILayout.Vector3Field("Position", position);

                        quaternion rotation = sgm.splineGraph.payload.rotations.data[v];
                        float3 rotationEulerDegrees = ((Quaternion)rotation).eulerAngles;
                        EditorGUILayout.Vector3Field("Rotation", rotationEulerDegrees);

                        float2 scale = sgm.splineGraph.payload.scales.data[v];
                        EditorGUILayout.Vector2Field("Scale", scale);

                        float2 leash = sgm.splineGraph.payload.leashes.data[v];
                        EditorGUILayout.Vector2Field("Leash", leash);

                        EditorGUILayout.EndVertical();
                    }
                }
            }
            
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        // TODO: Debug only:
        private void OnSceneGUI()
        {
            // If we are currently tumbling the camera, do not attempt to do anything else.
            if (Event.current.alt) { return; }

            serializedObject.Update();

            var sgm = target as SplineGraphManager;
            sgm.Verify();

            if (!sgm.isEditingEnabled) { return; }

            // return; // TODO: Remove?

            if (Tools.current == Tool.Move)
            {
                sgm.debugPosition = Handles.PositionHandle(sgm.debugPosition, quaternion.identity);

                for (int i = 0, iCount = sgm.debugFollowStates.Count; i < iCount; ++i)
                {
                    SplineMath.SplineGraphFollowState state = sgm.debugFollowStates[i];

                    int isReverse = state.DecodeIsReverse();
                    Int16 edgeIndex = state.DecodeEdgeIndex();
                    float t = state.t;
                    SplineMath.Spline spline = (state.DecodeIsReverse() == 0)
                        ? sgm.splineGraph.payload.edgeParentToChildSplines.data[edgeIndex]
                        : sgm.splineGraph.payload.edgeChildToParentSplines.data[edgeIndex];

                    float3 position = SplineMath.EvaluatePositionFromT(spline, t);
                    quaternion rotation = SplineMath.EvaluateRotationFromT(spline, t);

                    Handles.PositionHandle(position, rotation);
                }
            }

            if (sgm.isAutoUpdateEnabled)
            {
                sgm.BuildGraphFromInstances();
            }
        }

        private void OnInspectorGUISplineGraphBinaryDataTool(SplineGraphManager sgm)
        {
            if (sgm.splineGraphBinaryData == null)
            {
                if (GUILayout.Button("Create Binary Data"))
                {
                    sgm.UndoRecord("Created Spline Graph Binary Data");
                    SplineGraphBinaryDataScriptableObject binaryData = ScriptableObject.CreateInstance<SplineGraphBinaryDataScriptableObject>();

                    sgm.splineGraphBinaryData = binaryData;
                    sgm.splineGraphBinaryData.splineGraphSerializable = sgm.splineGraphSerializable;
                    sgm.splineGraphBinaryData.splineGraphPayloadSerializable = sgm.splineGraphPayloadSerializable;
                    sgm.splineGraphSerializable = null;
                    sgm.splineGraphPayloadSerializable = null;

                    AssetDatabase.CreateAsset(binaryData, string.Format("Assets/{0}_{1}_SplineGraphManager_SplineGraphBinaryData.asset", sgm.gameObject.scene.name, sgm.gameObject.name));
                    AssetDatabase.SaveAssets();
                }
            }

            {
                var binaryDataNext = EditorGUILayout.ObjectField("Spline Graph Binary Data", sgm.splineGraphBinaryData, typeof(SplineGraphBinaryDataScriptableObject), false) as SplineGraphBinaryDataScriptableObject;
                if (binaryDataNext != sgm.splineGraphBinaryData)
                {
                    sgm.UndoRecord("Edited Spline Graph Binary Data");

                    sgm.SetSplineGraphBinaryDataScriptableObjectAsSource(sgm.splineGraphUserBlobSchema, binaryDataNext);
                }
            }
        }
    }
#endif
}
