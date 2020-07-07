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
    public class SplineGraphManagerMesh : MonoBehaviour
    {
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

        #if UNITY_EDITOR
        void Update()
        {
            if (!isAutoUpdateEnabled) { return; }
            if (splineGraphManager == null) { return; }
            if (meshFilter == null) { return; }

            splineGraphManager.Verify();
            if (lastDirtyTimestamp == splineGraphManager.lastDirtyTimestamp
                && lastDirtyRadius == radius
                && lastDirtySubdivisionsPerMeter == subdivisionsPerMeter
                && lastDirtyUVScale == uvScale
                && lastDirtyRadialEdgeCount == radialEdgeCount)
            {
                // Completely up to date. Nothing to do.
                return;
            }
            UpdateMeshFromSplineGraphManager();
            lastDirtyTimestamp = splineGraphManager.lastDirtyTimestamp;
        }

        void UpdateMeshFromSplineGraphManager()
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null) { mesh = new Mesh(); }

            if (radialEdgeCount <= 0)
            {
                // TODO: Cleanup case when radialEdgeCount == 0 due to user still editing into inspector field.
                // This should be handled with a delayed int field.
                return;
            }

            var splineGraph = splineGraphManager.GetSplineGraph();

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
                    for (int v = 0; v < radialEdgeCount; ++v)
                    {
                        float thetaNormalized = (float)v / (float)radialEdgeCount;
                        float theta = thetaNormalized * 2.0f * math.PI;
                        float2 vertexOffsetOS = new float2(
                            math.cos(theta),
                            math.sin(theta)
                        ) * radius;

                        float3 vertexOffsetWS = math.mul(rotationOnSpline, new float3(vertexOffsetOS, 0.0f));
                        float3 vertexPositionWS = positionOnSpline + vertexOffsetWS;

                        vertices[meshVertexIndex] = vertexPositionWS;
                        uvs[meshVertexIndex] = new float2(
                            thetaNormalized,
                            vNormalized * splineLength * uvScale // TODO: Gotta figure out propogation of UVs.
                        );
                        normals[meshVertexIndex] = math.normalize(vertexOffsetWS);

                        ++meshVertexIndex;
                    }
                }
                
            }



            // Finally assign back data (causes GC allocs).
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles;
            meshFilter.sharedMesh = mesh;
        }
        #endif
    }
}
