using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace Pastasfuture.SplineGraph.Runtime
{
    [ExecuteAlways]
    public class SplineGraphMesh : MonoBehaviour
    {
        public SplineGraphComponent splineGraphComponent;
        public SplineGraphManager splineGraphManager;

        public MeshFilter meshFilter;
        public float radius = 1.0f;
        public float subdivisionsPerMeter = 1.0f;
        public float uvScale = 1.0f;
        public int radialEdgeCount = 4;
        public bool isAutoUpdateEnabled = true;

        // Do not need actual time, just a counter.
        // This is used to track / sync changes between the SplineGraphManager we are watching, and the Mesh generated in this class.
        [System.NonSerialized] public int lastDirtyTimestamp = 0;
        [System.NonSerialized] public float lastDirtyRadius = 0.0f;
        [System.NonSerialized] public float lastDirtySubdivisionsPerMeter = 0.0f;
        [System.NonSerialized] public float lastDirtyUVScale = 0.0f;
        [System.NonSerialized] public int lastDirtyRadialEdgeCount = -1;

        #if !UNITY_EDITOR
        void OnEnable()
        {
            if (splineGraphComponent == null && splineGraphManager == null) { return; }
            if (meshFilter == null) { return; }

            UpdateMeshFromSplineGraph();
        }
        #endif

        #if UNITY_EDITOR
        void Update()
        {
            UpdateInternal();
        }

        void UpdateInternal()
        {
            if (!isAutoUpdateEnabled) { return; }
            if (splineGraphComponent == null && splineGraphManager == null) { return; }
            if (meshFilter == null) { return; }

            int splineGraphLastDirtyTimestamp = 0;
            if (splineGraphComponent != null)
            {
                splineGraphComponent.Verify();
                splineGraphLastDirtyTimestamp = splineGraphComponent.lastDirtyTimestamp;
            }
            else if (splineGraphManager != null)
            {
                splineGraphManager.Verify();
                splineGraphLastDirtyTimestamp = splineGraphManager.lastDirtyTimestamp;
            }

            if (lastDirtyTimestamp == splineGraphLastDirtyTimestamp
                && lastDirtyRadius == radius
                && lastDirtySubdivisionsPerMeter == subdivisionsPerMeter
                && lastDirtyUVScale == uvScale
                && lastDirtyRadialEdgeCount == radialEdgeCount)
            {
                // Completely up to date. Nothing to do.
                return;
            }
            UpdateMeshFromSplineGraph();

            lastDirtyTimestamp = (splineGraphComponent != null)
                ? splineGraphComponent.lastDirtyTimestamp
                : splineGraphManager.lastDirtyTimestamp;
            lastDirtyRadius = radius;
            lastDirtySubdivisionsPerMeter = subdivisionsPerMeter;
            lastDirtyUVScale = uvScale;
            lastDirtyRadialEdgeCount = radialEdgeCount;
        }

        void OnDrawGizmos()
        {
            UpdateInternal();
        }
        #endif

        public void ForceUpdateMeshFromSplineGraph()
        {
            if (splineGraphComponent == null && splineGraphManager == null) { return; }
            if (meshFilter == null) { return; }

            if (splineGraphComponent != null)
            {
                splineGraphComponent.Verify();
            }
            else if (splineGraphManager != null)
            {
                splineGraphManager.Verify();
            }

            UpdateMeshFromSplineGraph();

            lastDirtyTimestamp = (splineGraphComponent != null)
                ? splineGraphComponent.lastDirtyTimestamp
                : splineGraphManager.lastDirtyTimestamp;
            lastDirtyRadius = radius;
            lastDirtySubdivisionsPerMeter = subdivisionsPerMeter;
            lastDirtyUVScale = uvScale;
            lastDirtyRadialEdgeCount = radialEdgeCount;
        }

        void UpdateMeshFromSplineGraph()
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null) { mesh = new Mesh(); }

            if (radialEdgeCount <= 0)
            {
                // TODO: Cleanup case when radialEdgeCount == 0 due to user still editing into inspector field.
                // This should be handled with a delayed int field.
                return;
            }

            var splineGraph = (splineGraphComponent != null)
                ? splineGraphComponent.GetSplineGraph()
                : splineGraphManager.GetSplineGraph();

            // First, go through and compute the number of vertex ring subdivisions required to represent the spline graph based on curvature.
            Int16 vertexIsValidCount = splineGraph.ComputeVertexIsValidCount();
            Int16 edgeIsValidCount = splineGraph.ComputeEdgeIsValidCount();

            int[] edgeSubdivisionIndex = new int[splineGraph.edgePoolChildren.count];
            int[] edgeSubdivisionCount = new int[splineGraph.edgePoolChildren.count];
            int meshSubdivisionCountTotal = 0;
            int meshRingCountTotal = 0;

            
            for (Int16 edgeIndex = 0; edgeIndex < splineGraph.edgePoolChildren.count; ++edgeIndex)
            {
                DirectedEdge edge = splineGraph.edgePoolChildren.data[edgeIndex];
                if (edge.IsValid() == 0) { continue; }

                float splineLength = splineGraph.payload.edgeLengths.data[edgeIndex];
                int subdivisionCount = math.max(1, (int)math.floor(subdivisionsPerMeter * splineLength + 0.5f));
                int ringCount = subdivisionCount + 1;

                edgeSubdivisionIndex[edgeIndex] = meshSubdivisionCountTotal;
                edgeSubdivisionCount[edgeIndex] = subdivisionCount;
                meshSubdivisionCountTotal += subdivisionCount;
                meshRingCountTotal += ringCount;
            }

            // TODO: Calculate counts.
            int meshVertexCount = meshRingCountTotal * radialEdgeCount;
            Vector3[] vertices = new Vector3[meshVertexCount];
            Vector2[] uvs = new Vector2[meshVertexCount];
            Vector3[] normals = new Vector3[meshVertexCount];
            int[] triangles = new int[meshVertexCount * 6];

            int meshVertexIndex = 0;
            int meshTriangleIndex = 0;
            for (Int16 edgeIndex = 0; edgeIndex < splineGraph.edgePoolChildren.count; ++edgeIndex)
            {
                DirectedEdge edge = splineGraph.edgePoolChildren.data[edgeIndex];
                if (edge.IsValid() == 0) { continue; }

                Int16 vertexIndexChild = splineGraph.edgePoolChildren.data[edgeIndex].vertexIndex;
                Int16 vertexIndexParent = splineGraph.edgePoolParents.data[edgeIndex].vertexIndex;

                SplineMath.Spline spline = splineGraph.payload.edgeParentToChildSplines.data[edgeIndex];
                float splineLength = splineGraph.payload.edgeLengths.data[edgeIndex];

                quaternion rotationParent = splineGraph.payload.rotations.data[vertexIndexParent];
                quaternion rotationChild = splineGraph.payload.rotations.data[vertexIndexChild];

                // For now, simply evaluate the current leash value by lerping between the parent and child leash values, rather than using spline interpolation.
                // This seems good enough for now (there is a bug in the spline interpolation code commented out below.)
                // float2 leashParent = splineGraph.payload.leashes.data[vertexIndexParent];
                // float2 leashChild = splineGraph.payload.leashes.data[vertexIndexChild];
                SplineMath.Spline splineLeash = true//(followState.DecodeIsReverse() == 0)
                    ? splineGraph.payload.edgeParentToChildSplinesLeashes.data[edgeIndex]
                    : splineGraph.payload.edgeChildToParentSplinesLeashes.data[edgeIndex];

                // Find neighboring edge (if it exists) for use in CSG style operation against current one to handle intersections in branching region.
                DirectedVertex vertexParent = splineGraph.vertices.data[vertexIndexParent];
                Debug.Assert(vertexParent.IsValid() == 1);

                DirectedVertex vertexChild = splineGraph.vertices.data[vertexIndexChild];
                Debug.Assert(vertexChild.IsValid() == 1);

                // bool edgeHasSibling = false;
                // Int16 edgeIndexSibling = -1;
                // Int16 siblingVertexIndexChild = -1;
                // SplineMath.Spline splineSibling = SplineMath.Spline.zero;
                // SplineMath.Spline splineLeashSibling = SplineMath.Spline.zero;
                // quaternion siblingRotationChild = quaternion.identity;
                // for (edgeIndexSibling = vertexParent.childHead; edgeIndexSibling != -1; edgeIndexSibling = splineGraph.edgePoolChildren.data[edgeIndexSibling].next)
                // {
                //     DirectedEdge edgeSibling = splineGraph.edgePoolChildren.data[edgeIndexSibling];
                //     Debug.Assert(edgeSibling.IsValid() == 1);
                    
                //     if (edgeIndexSibling == edgeIndex)
                //     {
                //         // Ignore ourselves.
                //         continue;
                //     }

                //     // Found our sibling edge. Only use the first one, as we only currently support CSG against a single branch.
                //     edgeHasSibling = true;
                //     siblingVertexIndexChild = edgeSibling.vertexIndex;
                //     Debug.Assert(siblingVertexIndexChild != -1);
                //     splineSibling = splineGraph.payload.edgeParentToChildSplines.data[edgeIndexSibling];
                //     splineLeashSibling = splineGraph.payload.edgeParentToChildSplinesLeashes.data[edgeIndexSibling];
                //     siblingRotationChild = splineGraph.payload.rotations.data[siblingVertexIndexChild];
                // }

                int edgeSubdivisionCurrentCount = edgeSubdivisionCount[edgeIndex]; 
                for (int s = 0; s <= edgeSubdivisionCurrentCount; ++s)
                {
                    float t = (float)s / (float)edgeSubdivisionCurrentCount;
                    // Debug.Log("s = " + s + ", sCount = " + edgeSubdivisionCurrentCount + ", t = " + t);

                    float vNormalized = t;
                    // TODO: Get this code working
                    // if (s > 0)
                    // {
                    //     // Compute the relative distance we have traveled between our current edge ring and previous.
                    //     SplineMath.ComputeSplitAtT(out SplineMath.Spline s00, out SplineMath.Spline s01, spline, t);
                        
                    //     vNormalized = SplineMath.ComputeLengthEstimate(s00, 1e-5f) / splineLength;
                    // }

                    float3 positionOnSpline = SplineMath.EvaluatePositionFromT(spline, t);
                    quaternion rotationOnSpline = SplineMath.EvaluateRotationWithRollFromT(spline, rotationParent, rotationChild, t);
                    // float2 leashMaxOS = math.lerp(leashParent, leashChild, t);               
                    float2 leashMaxOS = SplineMath.EvaluatePositionFromT(splineLeash, t).xy;

                    // Generate radial ring of triangles
                    if (s < edgeSubdivisionCurrentCount)
                    {
                        for (int v = 0, vLen = radialEdgeCount; v < vLen; ++v)
                        {
                            triangles[meshTriangleIndex + v * 6 + 0] = meshVertexIndex + ((v + 0) % radialEdgeCount) + (0 * radialEdgeCount);
                            triangles[meshTriangleIndex + v * 6 + 1] = meshVertexIndex + ((v + 0) % radialEdgeCount) + (1 * radialEdgeCount);
                            triangles[meshTriangleIndex + v * 6 + 2] = meshVertexIndex + ((v + 1) % radialEdgeCount) + (1 * radialEdgeCount);

                            triangles[meshTriangleIndex + v * 6 + 3] = meshVertexIndex + ((v + 1) % radialEdgeCount) + (1 * radialEdgeCount);
                            triangles[meshTriangleIndex + v * 6 + 4] = meshVertexIndex + ((v + 1) % radialEdgeCount) + (0 * radialEdgeCount);
                            triangles[meshTriangleIndex + v * 6 + 5] = meshVertexIndex + ((v + 0) % radialEdgeCount) + (0 * radialEdgeCount);
                        }

                        meshTriangleIndex += radialEdgeCount * 6;
                    }

                    // Generate radial ring of vertices.
                    bool ringIntersectsSibling = false;
                    for (int v = 0; v < radialEdgeCount; ++v)
                    {
                        float thetaNormalized = (float)v / (float)radialEdgeCount;
                        float theta = thetaNormalized * 2.0f * math.PI;
                        float2 vertexOffsetOS = new float2(
                            math.cos(theta),
                            math.sin(theta)
                        ) * leashMaxOS;// * radius;

                        float3 vertexOffsetWS = math.mul(rotationOnSpline, new float3(vertexOffsetOS, 0.0f));
                        float3 vertexPositionWS = positionOnSpline + vertexOffsetWS;

                        // {
                        //     // Test for intersections along previous points on the spline segment (can happen with large leashes and sharp turns).
                        //     SplineMath.FindTFromClosestPointOnSpline(out float otherClosestT, out float otherClosestDistance, vertexPositionWS, spline);

                        //     float3 vertexOtherOriginWS = SplineMath.EvaluatePositionFromT(spline, otherClosestT);
                        //     quaternion otherRotationOnSpline = SplineMath.EvaluateRotationWithRollFromT(spline, rotationParent, rotationChild, otherClosestT);
                        //     float2 otherLeashMaxOS = SplineMath.EvaluatePositionFromT(splineLeash, otherClosestT).xy;

                        //     float3 vertexOtherOffsetWS = vertexPositionWS - vertexOtherOriginWS;
                        //     float2 vertexOtherOffsetOS = math.mul(math.inverse(otherRotationOnSpline), vertexOtherOffsetWS).xy;
                        //     float2 vertexOtherOffsetNormalizedOS = vertexOtherOffsetOS.xy / otherLeashMaxOS;

                        //     vertexOtherOffsetOS = (math.lengthsq(vertexOtherOffsetNormalizedOS) < 1.0f)
                        //         ? (math.normalize(vertexOtherOffsetNormalizedOS) * otherLeashMaxOS)
                        //         : vertexOtherOffsetOS;

                        //     vertexOtherOffsetWS = math.mul(otherRotationOnSpline, new float3(vertexOtherOffsetOS, 0.0f));

                        //     vertexPositionWS = vertexOtherOffsetWS + vertexOtherOriginWS;
                        // }

                        // if (edgeHasSibling)
                        // {
                        //     SplineMath.FindTFromClosestPointOnSpline(out float siblingClosestT, out float siblingClosestDistance, vertexPositionWS, splineSibling);

                        //     float3 siblingPositionOnSpline = SplineMath.EvaluatePositionFromT(splineSibling, siblingClosestT);
                        //     quaternion siblingRotationOnSpline = SplineMath.EvaluateRotationWithRollFromT(splineSibling, rotationParent, siblingRotationChild, siblingClosestT);
                        //     float2 siblingLeashMaxOS = SplineMath.EvaluatePositionFromT(splineLeashSibling, siblingClosestT).xy;

                        //     if (math.length(math.mul(math.inverse(siblingRotationOnSpline), vertexPositionWS - siblingPositionOnSpline).xy / leashMaxOS) < 1.0f)
                        //     {
                        //         // // Inside of sibling.
                        //         // float2 siblingVertexOffsetOS = new float2(
                        //         //     math.cos(theta),
                        //         //     math.sin(theta)
                        //         // ) * siblingLeashMaxOS;

                        //         // float3 siblingVertexOffsetWS = math.mul(siblingRotationOnSpline, new float3(siblingVertexOffsetOS, 0.0f));
                        //         // float3 siblingVertexPositionWS = siblingPositionOnSpline + siblingVertexOffsetWS;

                        //         // vertexOffsetWS = siblingVertexOffsetWS;
                        //         // vertexPositionWS = siblingVertexPositionWS;
                        //         // vertexPositionWS = float.NaN;

                        //         ringIntersectsSibling = true;
                        //     }

                        // }

                        {
                            if (!ringIntersectsSibling)
                            {
                                for (Int16 edgeIndexSibling = vertexParent.childHead; edgeIndexSibling != -1; edgeIndexSibling = splineGraph.edgePoolChildren.data[edgeIndexSibling].next)
                                {
                                    DirectedEdge edgeSibling = splineGraph.edgePoolChildren.data[edgeIndexSibling];
                                    Debug.Assert(edgeSibling.IsValid() == 1);
                                    
                                    if (edgeIndexSibling == edgeIndex)
                                    {
                                        // Ignore ourselves.
                                        continue;
                                    }

                                    // Found our sibling edge. Only use the first one, as we only currently support CSG against a single branch.
                                    Int16 siblingVertexIndexChild = edgeSibling.vertexIndex;
                                    Debug.Assert(siblingVertexIndexChild != -1);

                                    Int16 siblingVertexIndexParent = splineGraph.edgePoolParents.data[edgeIndexSibling].vertexIndex;
                                    Debug.Assert(siblingVertexIndexParent != -1);

                                    SplineMath.Spline splineSibling = splineGraph.payload.edgeParentToChildSplines.data[edgeIndexSibling];
                                    SplineMath.Spline splineLeashSibling = splineGraph.payload.edgeParentToChildSplinesLeashes.data[edgeIndexSibling];
                                    quaternion siblingRotationParent = splineGraph.payload.rotations.data[siblingVertexIndexParent];
                                    quaternion siblingRotationChild = splineGraph.payload.rotations.data[siblingVertexIndexChild];

                                    SplineMath.FindTFromClosestPointOnSpline(out float siblingClosestT, out float siblingClosestDistance, vertexPositionWS, splineSibling);

                                    float3 siblingPositionOnSpline = SplineMath.EvaluatePositionFromT(splineSibling, siblingClosestT);
                                    quaternion siblingRotationOnSpline = SplineMath.EvaluateRotationWithRollFromT(splineSibling, siblingRotationParent, siblingRotationChild, siblingClosestT);
                                    float2 siblingLeashMaxOS = SplineMath.EvaluatePositionFromT(splineLeashSibling, siblingClosestT).xy;

                                    if (math.length(math.mul(math.inverse(siblingRotationOnSpline), vertexPositionWS - siblingPositionOnSpline).xy / leashMaxOS) < 1.0f)
                                    {
                                        ringIntersectsSibling = true;
                                        break;
                                    }
                                }
                            }

                            if (!ringIntersectsSibling)
                            {
                                for (Int16 edgeIndexSibling = vertexChild.parentHead; edgeIndexSibling != -1; edgeIndexSibling = splineGraph.edgePoolParents.data[edgeIndexSibling].next)
                                {
                                    DirectedEdge edgeSibling = splineGraph.edgePoolChildren.data[edgeIndexSibling];
                                    Debug.Assert(edgeSibling.IsValid() == 1);
                                    
                                    if (edgeIndexSibling == edgeIndex)
                                    {
                                        // Ignore ourselves.
                                        continue;
                                    }

                                    // Found our sibling edge. Only use the first one, as we only currently support CSG against a single branch.
                                    Int16 siblingVertexIndexParent = edgeSibling.vertexIndex;
                                    Debug.Assert(siblingVertexIndexParent != -1);

                                    Int16 siblingVertexIndexChild = splineGraph.edgePoolChildren.data[edgeIndexSibling].vertexIndex;
                                    Debug.Assert(siblingVertexIndexChild != -1);

                                    SplineMath.Spline splineSibling = splineGraph.payload.edgeParentToChildSplines.data[edgeIndexSibling];
                                    SplineMath.Spline splineLeashSibling = splineGraph.payload.edgeParentToChildSplinesLeashes.data[edgeIndexSibling];
                                    quaternion siblingRotationParent = splineGraph.payload.rotations.data[siblingVertexIndexParent];
                                    quaternion siblingRotationChild = splineGraph.payload.rotations.data[siblingVertexIndexChild];

                                    SplineMath.FindTFromClosestPointOnSpline(out float siblingClosestT, out float siblingClosestDistance, vertexPositionWS, splineSibling);

                                    float3 siblingPositionOnSpline = SplineMath.EvaluatePositionFromT(splineSibling, siblingClosestT);
                                    quaternion siblingRotationOnSpline = SplineMath.EvaluateRotationWithRollFromT(splineSibling, siblingRotationParent, siblingRotationChild, siblingClosestT);
                                    float2 siblingLeashMaxOS = SplineMath.EvaluatePositionFromT(splineLeashSibling, siblingClosestT).xy;

                                    if (math.length(math.mul(math.inverse(siblingRotationOnSpline), vertexPositionWS - siblingPositionOnSpline).xy / leashMaxOS) < 1.0f)
                                    {
                                        ringIntersectsSibling = true;
                                        break;
                                    }
                                }
                            }
                            
                        }

                        vertices[meshVertexIndex] = vertexPositionWS;
                        uvs[meshVertexIndex] = new float2(
                            thetaNormalized,
                            vNormalized * splineLength * uvScale // TODO: Gotta figure out propogation of UVs.
                        );
                        normals[meshVertexIndex] = math.normalize(vertexOffsetWS);

                        ++meshVertexIndex;
                    }

                    if (ringIntersectsSibling)
                    {
                        // Delete entire ring if any vertices intersect their sibling.
                        meshVertexIndex -= radialEdgeCount;
                        meshTriangleIndex -= radialEdgeCount * 6;
                    }
                }
                
            }

            // Trim arrays to final size.
            Vector3[] verticesTrimmed = new Vector3[meshVertexIndex];
            Vector2[] uvsTrimmed = new Vector2[meshVertexIndex];
            Vector3[] normalsTrimmed = new Vector3[meshVertexIndex];
            int[] trianglesTrimmed = new int[meshTriangleIndex];

            Array.Copy(vertices, verticesTrimmed, meshVertexIndex);
            Array.Copy(uvs, uvsTrimmed, meshVertexIndex);
            Array.Copy(normals, normalsTrimmed, meshVertexIndex);
            Array.Copy(triangles, trianglesTrimmed, meshTriangleIndex);

            // Finally assign back data (causes GC allocs).
            mesh.Clear();
            mesh.vertices = verticesTrimmed;
            mesh.uv = uvsTrimmed;
            mesh.normals = normalsTrimmed;
            mesh.triangles = trianglesTrimmed;
            meshFilter.sharedMesh = mesh;

            MeshCollider meshCollider = meshFilter.gameObject.GetComponent<MeshCollider>();
            if (meshCollider != null) { meshCollider.sharedMesh = mesh; }
        }
    }
}
