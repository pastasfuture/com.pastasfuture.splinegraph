using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace Pastasfuture.SplineGraph.Runtime
{
    public struct NativeArrayDynamic<T> where T : struct
    {
        public NativeArray<T> data;
        public int count;

        public NativeArrayDynamic(int capacityRequested, Allocator allocator)
        {
            Debug.Assert(capacityRequested > 0);
            data = new NativeArray<T>(capacityRequested, allocator);
            count = 0;
        }

        public void Dispose()
        {
            if (data.Length > 0) { data.Dispose(); }
            Clear();
        }

        public void Clear()
        {
            count = 0;
        }

        public void Ensure(int capacityRequested, Allocator allocator)
        {
            if (data.Length >= capacityRequested) { return; }

            NativeArray<T> dataNext = new NativeArray<T>(capacityRequested, allocator);
            if (data.Length > 0)
            {
                NativeArray<T>.Copy(data, dataNext, data.Length);
                data.Dispose();
            }
            data = dataNext;
        }

        public void FromArray(T[] source, Allocator allocator)
        {
            if (source == null || source.Length == 0)
            {
                count = 0;
                return;
            }
            Ensure(source.Length, allocator);
            NativeArray<T>.Copy(source, data, source.Length);
            count = source.Length;
        }

        public void ToArray(ref T[] res)
        {
            if (res == null || res.Length != count)
            {
                res = new T[count];
            }
            if (count == 0) { return; }
            NativeArray<T>.Copy(data, res, count);
        }

        public void Push(T value, Allocator allocator)
        {
            Ensure(count + 1, allocator);
            data[count++] = value;
        }

        public void Pop()
        {
            Debug.Assert(count > 0);
            --count;
        }

        public string DebugString()
        {
            string res = "{";
            for (int i = 0, iLen = count; i < iLen; ++i)
            {
                res += data[i];
                if ((i + 1) < count) { res += ", "; }
            }
            res += "}";
            return res;
        }
    }

    [BurstCompile, System.Serializable]
    public struct DirectedEdge
    {
        public Int16 vertexIndex;
        public Int16 next;

        public int IsValid()
        {
            return (vertexIndex >= 0) ? 1 : 0;
        }

        public static DirectedEdge CreateInvalidDirectedEdge()
        {
            return new DirectedEdge
            {
                vertexIndex = -1,
                next = -1 // Value of next doesn't actually matter. It should just be treated as garbage data.
            };
        }
    }

    [BurstCompile, System.Serializable]
    public struct DirectedVertex
    {
        public Int16 parentHead;
        public Int16 childHead;

        public int IsValid()
        {
            // Since the only valid range of values is {-1, Int16.MaxValue}, we can safely use
            // any number in the {-Int16.MaxValue, -2} range to indicate invalid state.
            return (parentHead == -2) ? 0 : 1;
        }

        public static DirectedVertex CreateInvalidDirectedVertex()
        {
            return new DirectedVertex
            {
                parentHead = -2,
                childHead = -1 // Value of childHead doesn't actually matter. It should just be treated as garbage data.
            };
        }
    }

    public interface IDirectedGraphPayload<T, SerializableT> where T : struct, IDirectedGraphPayload<T, SerializableT>
    {
        void Create(out T res, Allocator allocator);
        void Dispose();
        void Clear();
        void VertexEnsure(Int16 capacityRequested, Allocator allocator);
        void EdgeEnsure(Int16 capacityRequested, Allocator allocator);
        Int16 VertexPush(Allocator allocator);
        void VertexCopy(ref T src, Int16 vertexSrc, ref T dst, Int16 vertexDst); // Really, this is static.
        void VertexSwap(ref T src, Int16 vertexSrc, ref T dst, Int16 vertexDst); // Really, this is static.
        Int16 EdgePush(Allocator allocator);
        void EdgeCopy(ref T src, Int16 edgeSrc, ref T dst, Int16 edgeDst); // Really, this is static.
        void EdgeSwap(ref T src, Int16 edgeSrc, ref T dst, Int16 edgeDst); // Really, this is static.
        void VertexComputePayloads(ref DirectedGraph<T, SerializableT> graph, Int16 vertexIndex);
        void EdgeComputePayloads(ref DirectedGraph<T, SerializableT> graph, Int16 vertexParent, Int16 vertexChild);
        void Serialize(ref SerializableT o);
        void Deserialize(ref SerializableT i, Allocator allocator);
    }

    [System.Serializable]
    public class SplineGraphPayloadSerializable
    {
        public float3[] positions;
        public quaternion[] rotations;
        public float2[] scales;
        public float2[] leashes;
        public SplineMath.Spline[] edgeParentToChildSplines;
        public SplineMath.Spline[] edgeChildToParentSplines;
        public SplineMath.Spline[] edgeParentToChildSplinesLeashes;
        public SplineMath.Spline[] edgeChildToParentSplinesLeashes;
        public float[] edgeLengths;
        public float3[] edgeBounds;
    }

    public struct SplineGraphPayload : IDirectedGraphPayload<SplineGraphPayload, SplineGraphPayloadSerializable>
    {
        public NativeArrayDynamic<float3> positions;
        public NativeArrayDynamic<quaternion> rotations;
        public NativeArrayDynamic<float2> scales;
        public NativeArrayDynamic<float2> leashes;

        // Same spline segments stored at different locations to accelerate queries in either direction.
        public NativeArrayDynamic<SplineMath.Spline> edgeParentToChildSplines;
        public NativeArrayDynamic<SplineMath.Spline> edgeChildToParentSplines;
        public NativeArrayDynamic<SplineMath.Spline> edgeParentToChildSplinesLeashes;
        public NativeArrayDynamic<SplineMath.Spline> edgeChildToParentSplinesLeashes;

        public NativeArrayDynamic<float> edgeLengths;
        public NativeArrayDynamic<float3> edgeBounds;

        public static readonly int EDGE_BOUNDS_STRIDE = 2;

        public void Create(out SplineGraphPayload res, Allocator allocator)
        {
            // TODO: Fill these out with sensible default sizes.
            res.positions = new NativeArrayDynamic<float3>(1, allocator);
            res.rotations = new NativeArrayDynamic<quaternion>(1, allocator);
            res.scales = new NativeArrayDynamic<float2>(1, allocator);
            res.leashes = new NativeArrayDynamic<float2>(1, allocator);

            res.edgeParentToChildSplines = new NativeArrayDynamic<SplineMath.Spline>(1, allocator);
            res.edgeChildToParentSplines = new NativeArrayDynamic<SplineMath.Spline>(1, allocator);
            res.edgeParentToChildSplinesLeashes = new NativeArrayDynamic<SplineMath.Spline>(1, allocator);
            res.edgeChildToParentSplinesLeashes = new NativeArrayDynamic<SplineMath.Spline>(1, allocator);
            res.edgeLengths = new NativeArrayDynamic<float>(1, allocator);
            res.edgeBounds = new NativeArrayDynamic<float3>(1 * EDGE_BOUNDS_STRIDE, allocator);
        }

        public void Dispose()
        {
            positions.Dispose();
            rotations.Dispose();
            scales.Dispose();
            leashes.Dispose();
            edgeParentToChildSplines.Dispose();
            edgeChildToParentSplines.Dispose();
            edgeParentToChildSplinesLeashes.Dispose();
            edgeChildToParentSplinesLeashes.Dispose();
            edgeLengths.Dispose();
            edgeBounds.Dispose();
        }

        public void Clear()
        {
            positions.Clear();
            rotations.Clear();
            scales.Clear();
            leashes.Clear();
            edgeParentToChildSplines.Clear();
            edgeChildToParentSplines.Clear();
            edgeParentToChildSplinesLeashes.Clear();
            edgeChildToParentSplinesLeashes.Clear();
            edgeLengths.Clear();
            edgeBounds.Clear();
        }

        public void Serialize(ref SplineGraphPayloadSerializable o)
        {
            // Debug.Log("Serialize Payload!");
            if (o == null) { o = new SplineGraphPayloadSerializable(); }

            positions.ToArray(ref o.positions);
            rotations.ToArray(ref o.rotations);
            scales.ToArray(ref o.scales);
            leashes.ToArray(ref o.leashes);
            edgeParentToChildSplines.ToArray(ref o.edgeParentToChildSplines);
            edgeChildToParentSplines.ToArray(ref o.edgeChildToParentSplines);
            edgeParentToChildSplinesLeashes.ToArray(ref o.edgeParentToChildSplinesLeashes);
            edgeChildToParentSplinesLeashes.ToArray(ref o.edgeChildToParentSplinesLeashes);
            edgeLengths.ToArray(ref o.edgeLengths);
            edgeBounds.ToArray(ref o.edgeBounds);
        }

        public void Deserialize(ref SplineGraphPayloadSerializable i, Allocator allocator)
        {
            // Debug.Log("Deserialize payload!");

            positions.FromArray(i.positions, allocator);
            rotations.FromArray(i.rotations, allocator);
            scales.FromArray(i.scales, allocator);
            leashes.FromArray(i.leashes, allocator);
            edgeParentToChildSplines.FromArray(i.edgeParentToChildSplines, allocator);
            edgeChildToParentSplines.FromArray(i.edgeChildToParentSplines, allocator);
            edgeParentToChildSplinesLeashes.FromArray(i.edgeParentToChildSplinesLeashes, allocator);
            edgeChildToParentSplinesLeashes.FromArray(i.edgeChildToParentSplinesLeashes, allocator);
            edgeLengths.FromArray(i.edgeLengths, allocator);

            int edgeBoundsCountRequested = i.edgeLengths.Length * EDGE_BOUNDS_STRIDE;
            if ((i.edgeBounds == null) || (i.edgeBounds.Length != edgeBoundsCountRequested))
            {
                // Encountered old spline data that was authored before we introduced edgeBounds.
                // Need to generate data here.
                // TODO: In the future, we should add some version tracking and standardized migration system.
                edgeBounds.Ensure(i.edgeLengths.Length * EDGE_BOUNDS_STRIDE, allocator);
                edgeBounds.count = edgeBoundsCountRequested;

                for (Int16 edgeIndex = 0, edgeCount = (Int16)i.edgeParentToChildSplines.Length; edgeIndex < edgeCount; ++ edgeIndex)
                {
                    SplineMath.Spline edgeParentToChildSpline = edgeParentToChildSplines.data[edgeIndex];

                    SplineMath.ComputeSplineBounds(out float3 aabbMin, out float3 aabbMax, edgeParentToChildSpline);
                    edgeBounds.data[edgeIndex * EDGE_BOUNDS_STRIDE + 0] = aabbMin;
                    edgeBounds.data[edgeIndex * EDGE_BOUNDS_STRIDE + 1] = aabbMax;
                }
            }
            else
            {
                edgeBounds.FromArray(i.edgeBounds, allocator);
            }
            
        }

        public void VertexEnsure(Int16 capacityRequested, Allocator allocator)
        {
            positions.Ensure(capacityRequested, allocator);
            rotations.Ensure(capacityRequested, allocator);
            scales.Ensure(capacityRequested, allocator);
            leashes.Ensure(capacityRequested, allocator);
        }

        public void EdgeEnsure(Int16 capacityRequested, Allocator allocator)
        {
            edgeParentToChildSplines.Ensure(capacityRequested, allocator);
            edgeChildToParentSplines.Ensure(capacityRequested, allocator);
            edgeParentToChildSplinesLeashes.Ensure(capacityRequested, allocator);
            edgeChildToParentSplinesLeashes.Ensure(capacityRequested, allocator);
            edgeLengths.Ensure(capacityRequested, allocator);
            edgeBounds.Ensure(capacityRequested * EDGE_BOUNDS_STRIDE, allocator);
        }

        public Int16 VertexPush(Allocator allocator)
        {
            Int16 res = (Int16)positions.count;

            positions.Push(float3.zero, allocator);
            rotations.Push(quaternion.identity, allocator);
            scales.Push(new float2(1.0f, 1.0f), allocator);
            leashes.Push(new float2(0.0f, 0.0f), allocator);

            return res;
        }

        // Really, this is static.
        public void VertexCopy(ref SplineGraphPayload src, Int16 vertexSrc, ref SplineGraphPayload dst, Int16 vertexDst)
        {
            dst.positions.data[vertexDst] = src.positions.data[vertexSrc];
            dst.rotations.data[vertexDst] = src.rotations.data[vertexSrc];
            dst.scales.data[vertexDst] = src.scales.data[vertexSrc];
            dst.leashes.data[vertexDst] = src.leashes.data[vertexSrc];
        }

        // Really, this is static.
        public void VertexSwap(ref SplineGraphPayload src, Int16 vertexSrc, ref SplineGraphPayload dst, Int16 vertexDst)
        {
            // TODO:
        }

        public Int16 EdgePush(Allocator allocator)
        {
            Int16 res = (Int16)edgeParentToChildSplines.count;

            edgeParentToChildSplines.Push(new SplineMath.Spline(float4.zero, float4.zero, float4.zero), allocator);
            edgeChildToParentSplines.Push(new SplineMath.Spline(float4.zero, float4.zero, float4.zero), allocator);
            edgeParentToChildSplinesLeashes.Push(new SplineMath.Spline(float4.zero, float4.zero, float4.zero), allocator);
            edgeChildToParentSplinesLeashes.Push(new SplineMath.Spline(float4.zero, float4.zero, float4.zero), allocator);
            edgeLengths.Push(0.0f, allocator);
            edgeBounds.Push(float3.zero, allocator); edgeBounds.Push(float3.zero, allocator);

            return res;
        }

        // Really, this is static.
        public void EdgeCopy(ref SplineGraphPayload src, Int16 edgeSrc, ref SplineGraphPayload dst, Int16 edgeDst)
        {
            // TODO:
        }

        // Really, this is static.
        public void EdgeSwap(ref SplineGraphPayload src, Int16 edgeSrc, ref SplineGraphPayload dst, Int16 edgeDst)
        {
            // TODO:
        }

        public void VertexComputePayloads(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> graph, Int16 vertexIndex)
        {
            // TODO:
        }

        public void EdgeComputePayloads(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> graph, Int16 vertexParent, Int16 vertexChild)
        {
            Debug.Assert(vertexParent >= 0 && vertexParent < graph.vertices.count);
            Debug.Assert(vertexChild >= 0 && vertexChild < graph.vertices.count);
            Debug.Assert(graph.vertices.data[vertexParent].IsValid() == 1);
            Debug.Assert(graph.vertices.data[vertexChild].IsValid() == 1);

            // TODO: Could bubble up this search by passing in edge indices directly.
            // Would help remove redundant searches.
            // Might not matter.
            Int16 edgeIndex = graph.EdgeFindIndex(vertexParent, vertexChild);
            Debug.Assert(edgeIndex >= 0 && edgeIndex < edgeParentToChildSplines.count);

            SplineMath.Spline edgeParentToChildSpline = ComputeSplineFromVerticesParentToChild(ref graph, vertexParent, vertexChild);
            float edgeLength = SplineMath.ComputeLengthEstimate(edgeParentToChildSpline, 1e-5f);
            edgeParentToChildSplines.data[edgeIndex] = edgeParentToChildSpline;
            edgeLengths.data[edgeIndex] = edgeLength;

            SplineMath.ComputeSplineBounds(out float3 aabbMin, out float3 aabbMax, edgeParentToChildSpline);
            edgeBounds.data[edgeIndex * EDGE_BOUNDS_STRIDE + 0] = aabbMin;
            edgeBounds.data[edgeIndex * EDGE_BOUNDS_STRIDE + 1] = aabbMax;

            SplineMath.Spline edgeParentToChildSplineLeash = ComputeSplineLeashFromVerticesParentToChild(ref graph, vertexParent, vertexChild);
            edgeParentToChildSplinesLeashes.data[edgeIndex] = edgeParentToChildSplineLeash;

            // Reversing a spline is much cheaper (just a swizzle) than ComputeSplineFromVertices.
            edgeChildToParentSplines.data[edgeIndex] = SplineMath.SplineFromReverse(edgeParentToChildSpline);
            edgeChildToParentSplinesLeashes.data[edgeIndex] = SplineMath.SplineFromReverse(edgeParentToChildSplineLeash);
        }

        public SplineMath.Spline ComputeSplineFromVerticesParentToChild(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> graph, Int16 vertexParent, Int16 vertexChild)
        {
            Debug.Assert(vertexParent >= 0 && vertexParent < graph.vertices.count);
            Debug.Assert(vertexChild >= 0 && vertexChild < graph.vertices.count);
            Debug.Assert(graph.vertices.data[vertexParent].IsValid() == 1);
            Debug.Assert(graph.vertices.data[vertexChild].IsValid() == 1);

            float3 p0 = positions.data[vertexParent];
            float3 p1 = positions.data[vertexChild];

            // TODO: Create SplineMath.ComputeVelocityFromRotation() helper function?
            quaternion q0 = rotations.data[vertexParent];
            quaternion q1 = rotations.data[vertexChild];

            float s0 = scales.data[vertexParent].y;
            float s1 = scales.data[vertexChild].x;

            float3 v0 = math.mul(q0, new float3(0.0f, 0.0f, s0));
            float3 v1 = math.mul(q1, new float3(0.0f, 0.0f, s1));

            return SplineMath.SplineFromHermite(p0, p1, v0, v1);
        }

        public SplineMath.Spline ComputeSplineLeashFromVerticesParentToChild(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> graph, Int16 vertexParent, Int16 vertexChild)
        {
            Debug.Assert(vertexParent >= 0 && vertexParent < graph.vertices.count);
            Debug.Assert(vertexChild >= 0 && vertexChild < graph.vertices.count);
            Debug.Assert(graph.vertices.data[vertexParent].IsValid() == 1);
            Debug.Assert(graph.vertices.data[vertexChild].IsValid() == 1);

            float2 leash0 = leashes.data[vertexParent];
            float2 leash1 = leashes.data[vertexChild];

            float s0 = scales.data[vertexParent].y;
            float s1 = scales.data[vertexChild].x;

            // float2 leashB1 = leash0 + (1.0f / 3.0f) * math.abs(s0);
            // float2 leashB2 = leash1 - (1.0f / 3.0f) * math.abs(s1);

            // TODO: Figure out how to integrate scale (or if we want to).
            float2 leashB1 = leash0 + math.lerp(leash0, leash1, 1.0f / 3.0f);
            float2 leashB2 = leash1 - math.lerp(leash1, leash0, 1.0f / 3.0f);

            return SplineMath.SplineFromBezier(new float3(leash0, 0.0f), new float3(leashB1, 0.0f), new float3(leashB2, 0.0f), new float3(leash1, 0.0f));
        }
    }

    [System.Serializable]
    public class DirectedGraphSerializable
    {
        public DirectedVertex[] vertices;
        public DirectedEdge[] edgePoolParents;
        public DirectedEdge[] edgePoolChildren;

        public UInt16 verticesFreeHoleCount;
        public UInt16 edgePoolParentsFreeHoleCount;
        public UInt16 edgePoolChildrenFreeHoleCount;
    }

    [BurstCompile]
    public struct DirectedGraph<PayloadT, PayloadSerializableT> where PayloadT : struct, IDirectedGraphPayload<PayloadT, PayloadSerializableT>
    {
        public PayloadT payload;

        public NativeArrayDynamic<DirectedVertex> vertices;
        public NativeArrayDynamic<DirectedEdge> edgePoolParents;
        public NativeArrayDynamic<DirectedEdge> edgePoolChildren;

        public UInt16 verticesFreeHoleCount;
        public UInt16 edgePoolParentsFreeHoleCount;
        public UInt16 edgePoolChildrenFreeHoleCount;

        public DirectedGraph(Allocator allocator)
        {
            payload = new PayloadT();
            payload.Create(out payload, allocator);

            vertices = new NativeArrayDynamic<DirectedVertex>(1, allocator);
            edgePoolParents = new NativeArrayDynamic<DirectedEdge>(1, allocator);
            edgePoolChildren = new NativeArrayDynamic<DirectedEdge>(1, allocator);

            verticesFreeHoleCount = 0;
            edgePoolParentsFreeHoleCount = 0;
            edgePoolChildrenFreeHoleCount = 0;
        }

        public void Dispose()
        {
            payload.Dispose();

            vertices.Dispose();
            edgePoolParents.Dispose();
            edgePoolChildren.Dispose();
        }

        public void Clear()
        {
            payload.Clear();

            vertices.Clear();
            edgePoolParents.Clear();
            edgePoolChildren.Clear();

            verticesFreeHoleCount = 0;
            edgePoolParentsFreeHoleCount = 0;
            edgePoolChildrenFreeHoleCount = 0;
        }

        public void Serialize(ref DirectedGraphSerializable og, ref PayloadSerializableT op)
        {
            payload.Serialize(ref op);

            og.verticesFreeHoleCount = verticesFreeHoleCount;
            og.edgePoolParentsFreeHoleCount = edgePoolParentsFreeHoleCount;
            og.edgePoolChildrenFreeHoleCount = edgePoolChildrenFreeHoleCount;

            vertices.ToArray(ref og.vertices);
            edgePoolParents.ToArray(ref og.edgePoolParents);
            edgePoolChildren.ToArray(ref og.edgePoolChildren);
        }

        public void Deserialize(ref DirectedGraphSerializable ig, ref PayloadSerializableT ip, Allocator allocator)
        {
            payload.Deserialize(ref ip, allocator);

            verticesFreeHoleCount = ig.verticesFreeHoleCount;
            edgePoolParentsFreeHoleCount = ig.edgePoolParentsFreeHoleCount;
            edgePoolChildrenFreeHoleCount = ig.edgePoolChildrenFreeHoleCount;

            vertices.FromArray(ig.vertices, allocator);
            edgePoolParents.FromArray(ig.edgePoolParents, allocator);
            edgePoolChildren.FromArray(ig.edgePoolChildren, allocator);
        }

        public Int16 VertexAdd(Allocator allocator)
        {
            Int16 res = (Int16)vertices.count;

            vertices.Push(new DirectedVertex { parentHead = -1, childHead = -1 }, allocator);

            payload.VertexPush(allocator);

            return res;
        }

        public void VertexRemove(Int16 vertexIndex)
        {
            Debug.Assert(vertexIndex >= 0 && vertexIndex < vertices.count);
            Debug.Assert(vertices.data[vertexIndex].IsValid() == 1);

            // 1) Visit all parents of vertex we are removing, and remove their links to that vertex.
            while (vertices.data[vertexIndex].parentHead != -1)
            {
                Int16 edgeIndex = vertices.data[vertexIndex].parentHead;
                Int16 vertexParent = edgePoolParents.data[edgeIndex].vertexIndex;
                EdgeRemove(vertexParent, vertexIndex);
            }

            // 2) Visit all children of vertex we are removing, and remove their links to that vertex.
            while (vertices.data[vertexIndex].childHead != -1)
            {
                Int16 edgeIndex = vertices.data[vertexIndex].childHead;
                Int16 vertexChild = edgePoolChildren.data[edgeIndex].vertexIndex;
                EdgeRemove(vertexIndex, vertexChild);
            }

            // 3) Remove vertex.
            vertices.data[vertexIndex] = DirectedVertex.CreateInvalidDirectedVertex();
            ++verticesFreeHoleCount;
        }

        public void VertexMerge(Int16 vertexParent, Int16 vertexChild, Allocator allocator)
        {
            // Vertex Merge:
            {
                Debug.Assert(vertexParent != vertexChild);
                Debug.Assert(vertexParent >= 0 && vertexParent < vertices.count);
                Debug.Assert(vertexChild >= 0 && vertexChild < vertices.count);
                Debug.Assert(vertices.data[vertexParent].IsValid() == 1);
                Debug.Assert(vertices.data[vertexChild].IsValid() == 1);

                DirectedVertex vertexA = vertices.data[vertexParent];
                DirectedVertex vertexB = vertices.data[vertexChild];

                // 1) Connect all vertexChild children to vertexParent.
                for (Int16 edgeIndex = vertexB.childHead;
                    edgeIndex != -1;
                    edgeIndex = edgePoolChildren.data[edgeIndex].next)
                {
                    DirectedEdge edge = edgePoolChildren.data[edgeIndex];
                    Debug.Assert(edge.IsValid() == 1);

                    Int16 vertexChildChild = edge.vertexIndex;
                    Debug.Assert(vertexChildChild >= 0 && vertexChildChild < vertices.count);
                    Debug.Assert(vertices.data[vertexChildChild].IsValid() == 1);
                    Debug.Assert(vertexChildChild != vertexChild);

                    if (vertexChildChild == vertexParent)
                    {
                        // Vertex cannot become parent / child to itself.
                        continue;
                    }

                    if (EdgeContains(vertexParent, vertexChildChild) == 1)
                    {
                        continue;
                    }

                    EdgeAdd(vertexParent, vertexChildChild, allocator);
                }

                // 2) Connect all vertexChild parents to vertexParent.
                for (Int16 edgeIndex = vertexB.parentHead;
                    edgeIndex != -1;
                    edgeIndex = edgePoolParents.data[edgeIndex].next)
                {
                    DirectedEdge edge = edgePoolParents.data[edgeIndex];
                    Debug.Assert(edge.IsValid() == 1);

                    Int16 vertexChildParent = edge.vertexIndex;
                    Debug.Assert(vertexChildParent >= 0 && vertexChildParent < vertices.count);
                    Debug.Assert(vertices.data[vertexChildParent].IsValid() == 1);
                    Debug.Assert(vertexChildParent != vertexChild);

                    if (vertexChildParent == vertexParent)
                    {
                        // Vertex cannot become parent / child to itself.
                        continue;
                    }

                    if (EdgeContains(vertexChildParent, vertexParent) == 1)
                    {
                        continue;
                    }

                    EdgeAdd(vertexChildParent, vertexParent, allocator);
                }


                // 3) Remove vertexChild.
                VertexRemove(vertexChild);
            }

            // TODO: (maybe): Compute average position, rotation, and scale of merged vertices and use that.
            // For now, simply using the data from vertexA.

            payload.VertexComputePayloads(ref this, vertexParent);
        }

        public Int16 VertexComputeParentCount(Int16 vertexIndex)
        {
            Debug.Assert(vertexIndex >= 0 && vertexIndex < vertices.count);
            DirectedVertex vertex = vertices.data[vertexIndex];
            Debug.Assert(vertex.IsValid() == 1);

            Int16 parentCount = 0;
            for (Int16 edgeIndex = vertex.parentHead; edgeIndex != -1; edgeIndex = edgePoolParents.data[edgeIndex].next)
            {
                parentCount = (Int16)(parentCount + 1);
            }
            return parentCount;
        }

        public Int16 VertexComputeChildCount(Int16 vertexIndex)
        {
            Debug.Assert(vertexIndex >= 0 && vertexIndex < vertices.count);
            DirectedVertex vertex = vertices.data[vertexIndex];
            Debug.Assert(vertex.IsValid() == 1);

            Int16 childCount = 0;
            for (Int16 edgeIndex = vertex.childHead; edgeIndex != -1; edgeIndex = edgePoolChildren.data[edgeIndex].next)
            {
                childCount = (Int16)(childCount + 1);
            }
            return childCount;
        }

        public void VertexComputePayloads(Int16 vertexIndex)
        {
            Debug.Assert(vertexIndex >= 0 && vertexIndex < vertices.count);
            
            DirectedVertex vertex = vertices.data[vertexIndex];
            Debug.Assert(vertices.data[vertexIndex].IsValid() == 1);

            payload.VertexComputePayloads(ref this, vertexIndex);

            // Compute all child edge payloads.
            for (Int16 edgeIndex = vertex.childHead; edgeIndex != -1; edgeIndex = edgePoolChildren.data[edgeIndex].next)
            {
                DirectedEdge edge = edgePoolChildren.data[edgeIndex];
                Int16 vertexChild = edge.vertexIndex;

                EdgeComputePayloads(vertexIndex, vertexChild);
            }

            // Compute all parent edge payloads.
            for (Int16 edgeIndex = vertex.parentHead; edgeIndex != -1; edgeIndex = edgePoolParents.data[edgeIndex].next)
            {
                DirectedEdge edge = edgePoolParents.data[edgeIndex];
                Int16 vertexParent = edge.vertexIndex;

                EdgeComputePayloads(vertexParent, vertexIndex);
            }
        }

        public void EdgeComputePayloads(Int16 vertexParent, Int16 vertexChild)
        {
            payload.EdgeComputePayloads(ref this, vertexParent, vertexChild);
        }

        public void EdgeAdd(Int16 vertexParent, Int16 vertexChild, Allocator allocator)
        {
            Debug.Assert(vertexParent >= 0 && vertexParent < vertices.count, "Error: EdgeAdd(): vertexParent: " + vertexChild + " out of range: [0, " + vertices.count + "]");
            Debug.Assert(vertexChild >= 0 && vertexChild < vertices.count, "Error: EdgeAdd(): vertexChild: " + vertexChild + " out of range: [0, " + vertices.count + "]");

            Debug.Assert(vertices.data[vertexParent].IsValid() == 1);
            Debug.Assert(vertices.data[vertexChild].IsValid() == 1);

            Debug.Assert(EdgeContains(vertexParent, vertexChild) == 0);

            // Scan to end of child links and add new link.
            // Note: To accelerate adding edges, we could simply push front instead of push back,
            // which would allow us to skip the scanning process.
            // This would mean that edge data would be laid out in reverse order per vertex.
            // Haven't thought through the performance implications of this, but it might be fine.
            //
            // Also note, it appears that parent edge links are always stored at same index as child edge links
            // (just in different data arrays).

            // TODO!
            for (Int16 edgeIndexPrevious = -1, edgeIndex = vertices.data[vertexParent].childHead;
                true;
                edgeIndexPrevious = edgeIndex, edgeIndex = edgePoolChildren.data[edgeIndex].next)
            {
                if (edgeIndex == -1)
                {
                    edgePoolChildren.Push(
                        new DirectedEdge
                        { 
                            vertexIndex = vertexChild,
                            next = -1
                        },
                        allocator
                    );

                    if (edgeIndexPrevious != -1)
                    {
                        DirectedEdge edge = edgePoolChildren.data[edgeIndexPrevious];
                        edge.next = (Int16)(edgePoolChildren.count - 1);
                        edgePoolChildren.data[edgeIndexPrevious] = edge;
                    }

                    if (vertices.data[vertexParent].childHead == -1)
                    {
                        DirectedVertex vertex = vertices.data[vertexParent]; 
                        vertex.childHead = (Int16)(edgePoolChildren.count - 1);
                        vertices.data[vertexParent] = vertex; 
                    }

                    break;
                }
            }

            for (Int16 edgeIndexPrevious = -1, edgeIndex = vertices.data[vertexChild].parentHead;
                true;
                edgeIndexPrevious = edgeIndex, edgeIndex = edgePoolParents.data[edgeIndex].next)
            {
                if (edgeIndex == -1)
                {
                    edgePoolParents.Push(
                        new DirectedEdge
                        {
                            vertexIndex = vertexParent,
                            next = -1
                        },
                        allocator
                    );

                    if (edgeIndexPrevious != -1)
                    {
                        DirectedEdge edge = edgePoolParents.data[edgeIndexPrevious]; 
                        edge.next = (Int16)(edgePoolParents.count - 1);
                        edgePoolParents.data[edgeIndexPrevious] = edge;
                    }

                    if (vertices.data[vertexChild].parentHead == -1)
                    {
                        DirectedVertex vertex = vertices.data[vertexChild]; 
                        vertex.parentHead = (Int16)(edgePoolParents.count - 1);
                        vertices.data[vertexChild] = vertex;
                    }

                    break;
                }
            }

            payload.EdgePush(allocator);

            payload.EdgeComputePayloads(ref this, vertexParent, vertexChild);
        }

        public int EdgeContains(Int16 vertexParent, Int16 vertexChild)
        {
            Debug.Assert(vertexParent >= 0 && vertexParent < vertices.count);
            Debug.Assert(vertexChild >= 0 && vertexChild < vertices.count);

            for (Int16 edgeIndex = vertices.data[vertexParent].childHead; edgeIndex != -1; edgeIndex = edgePoolChildren.data[edgeIndex].next)
            {
                Debug.Assert(edgeIndex >= 0 && edgeIndex < edgePoolChildren.count);
                DirectedEdge edge = edgePoolChildren.data[edgeIndex];
                Debug.Assert(edge.IsValid() == 1);

                if (edge.vertexIndex == vertexChild)
                {
                    // Found edge.
                    return 1;
                }
            }

            return 0;
        }

        public void EdgeRemove(Int16 vertexParent, Int16 vertexChild)
        {
            Debug.Assert(vertexParent >= 0 && vertexParent < vertices.count);
            Debug.Assert(vertexChild >= 0 && vertexChild < vertices.count);

            for (Int16 edgeIndexPrevious = -1, edgeIndex = vertices.data[vertexParent].childHead;
                true;
                edgeIndexPrevious = edgeIndex, edgeIndex = edgePoolChildren.data[edgeIndex].next)
            {
                Debug.Assert(edgeIndex != -1);

                if (edgePoolChildren.data[edgeIndex].vertexIndex == vertexChild)
                {
                    // Remove links to edge.
                    if (edgeIndexPrevious != -1)
                    {
                        DirectedEdge edge = edgePoolChildren.data[edgeIndexPrevious]; 
                        edge.next = edgePoolChildren.data[edgeIndex].next;
                        edgePoolChildren.data[edgeIndexPrevious] = edge;
                    }
                    else
                    {
                        DirectedVertex vertex = vertices.data[vertexParent];
                        vertex.childHead = edgePoolChildren.data[edgeIndex].next;
                        vertices.data[vertexParent] = vertex;
                    }

                    // Flag edge as invalid, so that it can be skipped over during iteration.
                    edgePoolChildren.data[edgeIndex] = DirectedEdge.CreateInvalidDirectedEdge();

                    // Account for new hole so that graph can be potentially compacted if enough holes accumulate.
                    ++edgePoolChildrenFreeHoleCount;
                    break;
                }
            }

            for (Int16 edgeIndexPrevious = -1, edgeIndex = vertices.data[vertexChild].parentHead;
                true;
                edgeIndexPrevious = edgeIndex, edgeIndex = edgePoolParents.data[edgeIndex].next)
            {
                Debug.Assert(edgeIndex != -1);

                if (edgePoolParents.data[edgeIndex].vertexIndex == vertexParent)
                {
                    // Remove links to edge.
                    if (edgeIndexPrevious != -1)
                    {
                        DirectedEdge edge = edgePoolParents.data[edgeIndexPrevious];
                        edge.next = edgePoolParents.data[edgeIndex].next;
                        edgePoolParents.data[edgeIndexPrevious] = edge;
                    }
                    else
                    {
                        DirectedVertex vertex = vertices.data[vertexChild]; 
                        vertex.parentHead = edgePoolParents.data[edgeIndex].next;
                        vertices.data[vertexChild] = vertex;
                    }

                    // Flag edge as invalid, so that it can be skipped over during iteration.
                    edgePoolParents.data[edgeIndex] = DirectedEdge.CreateInvalidDirectedEdge();

                    // Account for new hole so that graph can be potentially compacted if enough holes accumulate.
                    ++edgePoolParentsFreeHoleCount;
                    break;
                }
            }
        }

        public void EdgeReverse(Int16 vertexParent, Int16 vertexChild, Allocator allocator)
        {
            Debug.Assert(vertexParent >= 0 && vertexParent < vertices.count);
            Debug.Assert(vertexChild >= 0 && vertexChild < vertices.count);
            Debug.Assert(vertices.data[vertexParent].IsValid() == 1);
            Debug.Assert(vertices.data[vertexChild].IsValid() == 1);

            EdgeRemove(vertexParent, vertexChild);
            EdgeAdd(vertexChild, vertexParent, allocator);
        }

        public Int16 EdgeFindIndex(Int16 vertexParent, Int16 vertexChild)
        {
            Debug.Assert(vertexParent >= 0 && vertexParent < vertices.count);
            Debug.Assert(vertexChild >= 0 && vertexChild < vertices.count);

            Debug.Assert(vertices.data[vertexParent].IsValid() == 1);
            Debug.Assert(vertices.data[vertexChild].IsValid() == 1);

            for (Int16 edgeIndex = vertices.data[vertexParent].childHead; edgeIndex != -1; edgeIndex = edgePoolChildren.data[edgeIndex].next)
            {
                Debug.Assert(edgeIndex >= 0 && edgeIndex < edgePoolChildren.count);

                DirectedEdge edge = edgePoolChildren.data[edgeIndex];
                Debug.Assert(edge.IsValid() == 1);

                if (edge.vertexIndex == vertexChild)
                {
                    return edgeIndex;
                }
            }

            // Failed to find edge. For now lets asset and see if we want to support failed queries.
            Debug.Assert(false);
            return -1;
        }

        public string DebugStringFromAdjacencyList()
        {
            string res = "";


            for (Int16 v = 0, vCount = (Int16)vertices.count; v < vCount; ++v)
            {
                if (vertices.data[v].IsValid() == 0)
                {
                    // Hole. Print for debugging purposes:
                    res += "v[" + v + "] = Invalid";
                    if ((v + 1) < vCount)
                    {
                        res += ",\n";
                    }
                    continue;
                }

                res += "v[" + v + "] = { ";

                res += " children: { ";
                for (Int16 e = vertices.data[v].childHead; e != -1; e = edgePoolChildren.data[e].next)
                {
                    res += edgePoolChildren.data[e].vertexIndex;
                    if (edgePoolChildren.data[e].next != -1) { res += ", "; }
                }

                res += "}, parents: { ";
                for (Int16 e = vertices.data[v].parentHead; e != -1; e = edgePoolParents.data[e].next)
                {
                    res += edgePoolParents.data[e].vertexIndex;
                    if (edgePoolParents.data[e].next != -1) { res += ", "; }
                }
                res += "} }";
                if ((v + 1) < vCount)
                {
                    res += ",\n";
                }
            }

            return res;
        }

        public void PushDirectedGraph(ref DirectedGraph<PayloadT, PayloadSerializableT> src, Allocator allocator)
        {
            Int16 vertexCountNew = (Int16)(vertices.count + (src.vertices.count - src.verticesFreeHoleCount));
            Int16 edgeCountNew = (Int16)(edgePoolParents.count + (src.edgePoolParents.count - src.edgePoolParentsFreeHoleCount));

            vertices.Ensure(vertexCountNew, allocator);
            edgePoolParents.Ensure(edgeCountNew, allocator);
            edgePoolChildren.Ensure(edgeCountNew, allocator);
            payload.VertexEnsure(vertexCountNew, allocator);
            payload.EdgeEnsure(edgeCountNew, allocator);

            // Vertices from source graph will be appended onto the end of the current vertex array,
            // therefore, their new indices will be their old indices, offset by the count before appending.
            Int16 vertexOffset = (Int16)vertices.count;

            for (Int16 v = 0, vCount = (Int16)src.vertices.count, vNew = vertexOffset; v < vCount; ++v)
            {
                DirectedVertex vertex = src.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                VertexAdd(allocator);
                payload.VertexCopy(ref src.payload, v, ref payload, vNew);

                ++vNew;
            }

            for (Int16 v = 0, vCount = (Int16)src.vertices.count, vNew = vertexOffset; v < vCount; ++v)
            {
                DirectedVertex vertex = src.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                for (Int16 edgeIndex = src.vertices.data[v].childHead; edgeIndex != -1; edgeIndex = src.edgePoolChildren.data[edgeIndex].next)
                {
                    DirectedEdge edge = src.edgePoolChildren.data[edgeIndex];
                    Debug.Assert(edge.IsValid() == 1);

                    Int16 vChildNew = (Int16)(vertexOffset + edge.vertexIndex);

                    // Scan account for invalid spaces that were compacted:
                    for (Int16 vp = 0; vp < edge.vertexIndex; ++vp)
                    {
                        DirectedVertex vertexPrevious = src.vertices.data[vp];
                        if (vertexPrevious.IsValid() == 0) { --vChildNew; }
                    }

                    EdgeAdd(vNew, vChildNew, allocator);
                }
                ++vNew;
            }
        }

        public void BuildCompactDirectedGraph(ref DirectedGraph<PayloadT, PayloadSerializableT> res, Allocator allocator)
        {
            res.Clear();

            Int16 vertexCountCompact = (Int16)(vertices.count - verticesFreeHoleCount);
            Int16 edgeCountCompact = (Int16)(edgePoolParents.count - edgePoolParentsFreeHoleCount);

            res.vertices.Ensure(vertexCountCompact, allocator);
            res.edgePoolParents.Ensure(edgeCountCompact, allocator);
            res.edgePoolChildren.Ensure(edgeCountCompact, allocator);

            res.payload.VertexEnsure(vertexCountCompact, allocator);
            res.payload.EdgeEnsure(edgeCountCompact, allocator);

            // Because EdgeAdd() implicity computes edge data from vertex data, vertex data must be valid before edges are added.
            // This means we need to first loop through and add all vertices,
            // then loop through again and add all edges.
            // This is another example of how automatically updating edge data on EdgeAdd() calls has complicated things.
            // Would it have been better to simply avoid dirty tracking?
            for (Int16 v = 0, vCount = (Int16)vertices.count; v < vCount; ++v)
            {
                DirectedVertex vertex = vertices.data[v];
                if (vertex.IsValid() == 0)
                {
                    // Do not increment vNew. Just continue.
                    continue;
                }

                Int16 vNew = res.VertexAdd(allocator);
                res.payload.VertexCopy(ref payload, v, ref res.payload, vNew);
            }

            for (Int16 v = 0, vCount = (Int16)vertices.count, vNew = 0; v < vCount; ++v)
            {
                DirectedVertex vertex = vertices.data[v];
                if (vertex.IsValid() == 0)
                {
                    // Do not increment vNew. Just continue.
                    continue;
                }

                // Add all child edges.
                // Because we are going through the EdgeAdd() function, all parent edges will be implicitly added at this time as well.
                for (Int16 edgeIndex = vertices.data[v].childHead; edgeIndex != -1; edgeIndex = edgePoolChildren.data[edgeIndex].next)
                {
                    DirectedEdge edge = edgePoolChildren.data[edgeIndex];

                    Int16 vChildNew = edge.vertexIndex;

                    Debug.Assert(vChildNew >= 0 && vChildNew < vertices.count);

                    // Scan to see how many vertex holes exist between {0, vChildNew - 1} and update index of vChildNew to account for compaction.
                    // This of course makes compaction like this slow.
                    // If we encounter any situations where we want to run compaction at runtime, this data structure should be revisited.
                    for (Int16 v1 = (Int16)0, v1Count = vChildNew; v1 < v1Count; ++v1)
                    {
                        if (vertices.data[v1].IsValid() == 0)
                        {
                            // Found a hole. Decrement to account for compaction.
                            --vChildNew;
                        }
                    }

                    res.EdgeAdd(vNew, vChildNew, allocator);
                }

                ++vNew;
            }

        }

        public void BuildCompactReverseDirectedGraph(ref DirectedGraph<PayloadT, PayloadSerializableT> res, Allocator allocator)
        {
            res.Clear();

            Int16 vertexCountCompact = (Int16)(vertices.count - verticesFreeHoleCount);
            Int16 edgeCountCompact = (Int16)(edgePoolParents.count - edgePoolParentsFreeHoleCount);

            res.vertices.Ensure(vertexCountCompact, allocator);
            res.edgePoolParents.Ensure(edgeCountCompact, allocator);
            res.edgePoolChildren.Ensure(edgeCountCompact, allocator);

            res.payload.VertexEnsure(vertexCountCompact, allocator);
            res.payload.EdgeEnsure(edgeCountCompact, allocator);

            // Because EdgeAdd() implicity computes edge data from vertex data, vertex data must be valid before edges are added.
            // This means we need to first loop through and add all vertices,
            // then loop through again and add all edges.
            // This is another example of how automatically updating edge data on EdgeAdd() calls has complicated things.
            // Would it have been better to simply avoid dirty tracking?
            for (Int16 v = 0, vCount = (Int16)vertices.count; v < vCount; ++v)
            {
                DirectedVertex vertex = vertices.data[v];
                if (vertex.IsValid() == 0)
                {
                    // Do not increment vNew. Just continue.
                    continue;
                }

                Int16 vNew = res.VertexAdd(allocator);
                res.payload.VertexCopy(ref payload, v, ref res.payload, vNew);
            }

            for (Int16 v = 0, vCount = (Int16)vertices.count, vNew = 0; v < vCount; ++v)
            {
                DirectedVertex vertex = vertices.data[v];
                if (vertex.IsValid() == 0)
                {
                    // Do not increment vNew. Just continue.
                    continue;
                }

                // Add all child edges.
                // Because we are going through the EdgeAdd() function, all parent edges will be implicitly added at this time as well.
                for (Int16 edgeIndex = vertices.data[v].childHead; edgeIndex != -1; edgeIndex = edgePoolChildren.data[edgeIndex].next)
                {
                    DirectedEdge edge = edgePoolChildren.data[edgeIndex];

                    Int16 vChildNew = edge.vertexIndex;

                    Debug.Assert(vChildNew >= 0 && vChildNew < vertices.count);

                    // Scan to see how many vertex holes exist between {0, vChildNew - 1} and update index of vChildNew to account for compaction.
                    // This of course makes compaction like this slow.
                    // If we encounter any situations where we want to run compaction at runtime, this data structure should be revisited.
                    for (Int16 v1 = (Int16)0, v1Count = vChildNew; v1 < v1Count; ++v1)
                    {
                        if (vertices.data[v1].IsValid() == 0)
                        {
                            // Found a hole. Decrement to account for compaction.
                            --vChildNew;
                        }
                    }

                    res.EdgeAdd(vChildNew, vNew, allocator);
                }

                ++vNew;
            }

        }

        

        // public static void DebugRunTests()
        // {
        //     SplineGraph.DebugRunTests();
        //     // SplineGraph splineGraph = new SplineGraph(Allocator.Temp);

        //     // Int16 a = splineGraph.VertexAdd(Allocator.Temp);
        //     // Int16 b = splineGraph.VertexAdd(Allocator.Temp);
        //     // Int16 c = splineGraph.VertexAdd(Allocator.Temp);
        //     // Int16 d = splineGraph.VertexAdd(Allocator.Temp);

        //     // splineGraph.positions.data[a] = new float3(0.0f, 0.0f, 0.0f);
        //     // splineGraph.positions.data[b] = new float3(1.0f, 1.0f, 1.0f);
        //     // splineGraph.positions.data[c] = new float3(2.0f, 2.0f, 2.0f);
        //     // splineGraph.positions.data[d] = new float3(3.0f, 3.0f, 3.0f);

        //     // // TODO: Test with better rotation values.
        //     // splineGraph.rotations.data[a] = quaternion.identity;
        //     // splineGraph.rotations.data[b] = quaternion.identity;
        //     // splineGraph.rotations.data[c] = quaternion.identity;
        //     // splineGraph.rotations.data[d] = quaternion.identity;

        //     // splineGraph.EdgeAdd(a, b, Allocator.Temp);
        //     // splineGraph.EdgeAdd(b, c, Allocator.Temp);
        //     // splineGraph.EdgeAdd(c, d, Allocator.Temp);
        //     // splineGraph.EdgeAdd(d, a, Allocator.Temp);
        //     // // splineGraph.EdgeRemove(d, a);
        //     // // splineGraph.EdgeAdd(b, d, Allocator.Temp);
        //     // Debug.Log(splineGraph.graph.DebugStringFromAdjacencyList());

        //     // Debug.Log("Now removing vertex c:");
        //     // splineGraph.VertexRemove(c);
        //     // Debug.Log(splineGraph.graph.DebugStringFromAdjacencyList());

        //     // splineGraph.Dispose();
        // }

        // public static void DebugRunTests()
        // {
        //     DirectedGraph graph = new DirectedGraph(Allocator.Temp);

        //     Int16 a = graph.VertexAdd(Allocator.Temp);
        //     Int16 b = graph.VertexAdd(Allocator.Temp);
        //     Int16 c = graph.VertexAdd(Allocator.Temp);
        //     Int16 d = graph.VertexAdd(Allocator.Temp);

        //     // NativeArrayDynamic<int> buffer = new NativeArrayDynamic<int>(4, Allocator.Persistent);
        //     // Debug.Log(buffer.DebugString());
        //     // buffer.Push(0, Allocator.Persistent);
        //     // Debug.Log(buffer.DebugString());
        //     // buffer.Push(1, Allocator.Persistent);
        //     // Debug.Log(buffer.DebugString());
        //     // buffer.Push(2, Allocator.Persistent);
        //     // Debug.Log(buffer.DebugString());
        //     // buffer.Push(3, Allocator.Persistent);
        //     // buffer.Dispose();


        //     graph.EdgeAdd(a, b, Allocator.Temp);
        //     graph.EdgeAdd(b, c, Allocator.Temp);
        //     graph.EdgeAdd(c, d, Allocator.Temp);
        //     graph.EdgeAdd(d, a, Allocator.Temp);

        //     Debug.Assert(graph.EdgeContains(a, b) == 1);
        //     Debug.Assert(graph.EdgeContains(b, c) == 1);
        //     Debug.Assert(graph.EdgeContains(c, d) == 1);
        //     Debug.Assert(graph.EdgeContains(d, a) == 1);

        //     graph.EdgeRemove(d, a);
        //     Debug.Assert(graph.EdgeContains(d, a) == 0);

        //     graph.EdgeAdd(b, d, Allocator.Temp);
        //     Debug.Assert(graph.EdgeContains(b, d) == 1);

        //     graph.EdgeAdd(d, a, Allocator.Temp);
        //     Debug.Assert(graph.EdgeContains(d, a) == 1);

        //     Debug.Log(graph.DebugStringFromAdjacencyList());

        //     Debug.Log("Now removing vertex c:");
        //     graph.VertexRemove(c);
        //     Debug.Log(graph.DebugStringFromAdjacencyList());
        //     Debug.Assert(graph.EdgeContains(c, d) == 0);

        //     Debug.Log("Now merging vertex b, and d:");
        //     graph.VertexMerge(b, d, Allocator.Temp);
        //     Debug.Log(graph.DebugStringFromAdjacencyList());

        //     Debug.Log("Now reversing edge a->b");
        //     graph.EdgeReverse(a, b, Allocator.Temp);

        //     graph.Dispose();
        // }
    }
}