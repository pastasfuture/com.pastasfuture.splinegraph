// using UnityEngine;
// using System.Collections;
// using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System;

namespace Pastasfuture.SplineGraph.Runtime
{
    // Based on: https://github.com/andrewwillmott/splines-lib
    public static class SplineMath
    {
        [BurstCompile, System.Serializable]
        public struct Spline
        {
            public float4 xb; // x cubic bezier coefficients.
            public float4 yb; // y cubic bezier coefficients.
            public float4 zb; // z cubic bezier coefficients.

            public Spline(float4 xb, float4 yb, float4 zb)
            {
                this.xb = xb;
                this.yb = yb;
                this.zb = zb;
            }

            [BurstCompile]
            public float3 GetPosition0()
            {
                return new float3(this.xb.x, this.yb.x, this.zb.x);
            }

            [BurstCompile]
            public float3 GetPosition1()
            {
                return new float3(this.xb.w, this.yb.w, this.zb.w);
            }

            [BurstCompile]
            public float3 ComputeVelocity0()
            {
                return new float3(
                    3.0f * (this.xb.y - this.xb.x),
                    3.0f * (this.yb.y - this.yb.x),
                    3.0f * (this.zb.y - this.zb.x)
                );
            }

            [BurstCompile]
            public float3 ComputeVelocity1()
            {
                return new float3(
                    3.0f * (this.xb.w - this.xb.z),
                    3.0f * (this.yb.w - this.yb.z),
                    3.0f * (this.zb.w - this.zb.z)
                );
            }

            public static readonly Spline zero = new Spline(float4.zero, float4.zero, float4.zero);
        }

        public struct SplinePathData
        {
            public NativeArray<SplineMath.Spline> splines;

            public SplinePathData(int splinesCount, Unity.Collections.Allocator allocator)
            {
                Debug.Assert(splinesCount > 0);
                splines = new NativeArray<SplineMath.Spline>(splinesCount, allocator);
            }

            public void Dispose()
            {
                if (splines.Length > 0)
                {
                    splines.Dispose();
                }
            }

            public void EnsureCapacity(int splinesCount, Unity.Collections.Allocator allocator)
            {
                Debug.Assert(splinesCount > 0);
                if (splinesCount != splines.Length)
                {
                    Dispose();
                    this = new SplineMath.SplinePathData(splinesCount, allocator);
                }
            }
        }

        [BurstCompile]
        private static float4 ComputeCubicCoefficientsFromSplineComponent(float4 sc)
        {
            return new float4(
                sc.x,
                -3.0f * sc.x + 3.0f * sc.y,
                3.0f * sc.x - 6.0f * sc.y + 3.0f * sc.z,
                -sc.x + 3.0f * sc.y - 3.0f * sc.z + sc.w
            );
        }

        [BurstCompile]
        private static float2 ComputeSplineBoundsComponent(float4 splineComponent)
        /// Returns accurate bounds taking extrema into account.
        {
            float2 bounds;

            // First take endpoints into account
            if (splineComponent.x <= splineComponent.w)
            {
                bounds.x = splineComponent.x;
                bounds.y = splineComponent.w;
            }
            else
            {
                bounds.x = splineComponent.w;
                bounds.y = splineComponent.x;
            }

            // Now find extrema via standard quadratic equation: c.t' = 0
            float4 c = SplineMath.ComputeCubicCoefficientsFromSplineComponent(splineComponent);

            float c33 = 3.0f * c.w;
            float cx2 = c.z * c.z - c33 * c.y;

            if (cx2 < 0.0f)
            {
                // no roots!
                return bounds;
            }

            float invC33 = 1.0f / c33;
            float ct = -c.z * invC33;
            float cx = math.sqrt(cx2) * invC33;

            float t0 = ct + cx;
            float t1 = ct - cx;

            // Must make sure the roots are within the spline interval
            if (t0 > 0.0f && t0 < 1.0f)
            {
                float x = c.x + (c.y + (c.z + c.w * t0) * t0) * t0;

                if (bounds.x > x) { bounds.x = x; }
                else if (bounds.y < x) { bounds.y = x; }
            }

            if (t1 > 0.0f && t1 < 1.0f)
            {
                float x = c.x + (c.y + (c.z + c.w * t1) * t1) * t1;

                if (bounds.x > x) { bounds.x = x; }
                else if (bounds.y < x) { bounds.y = x; }
            }

            return bounds;
        }

        [BurstCompile]
        public static void ComputeSplineBounds(out float3 aabbMin, out float3 aabbMax, Spline spline)
        {
            float2 bx = SplineMath.ComputeSplineBoundsComponent(spline.xb);
            float2 by = SplineMath.ComputeSplineBoundsComponent(spline.yb);
            float2 bz = SplineMath.ComputeSplineBoundsComponent(spline.zb);

            aabbMin = new float3(bx.x, by.x, bz.x);
            aabbMax = new float3(bx.y, by.y, bz.y);
        }


        [BurstCompile]
        public static Spline SplineFromBezier(float3 p0, float3 p1, float3 p2, float3 p3)
        {
            return new Spline(
                new float4(p0.x, p1.x, p2.x, p3.x),
                new float4(p0.y, p1.y, p2.y, p3.y),
                new float4(p0.z, p1.z, p2.z, p3.z)
            );
        }

        [BurstCompile]
        public static Spline SplineFromHermite(float3 p0, float3 p1, float3 v0, float3 v1)
        {
            float3 pb1 = p0 + (1.0f / 3.0f) * v0;
            float3 pb2 = p1 - (1.0f / 3.0f) * v1;

            return SplineFromBezier(p0, pb1, pb2, p1);
        }

        [BurstCompile]
        public static Spline SplineFromCatmullRom(float3 p0, float3 p1, float3 p2, float3 p3)
        {
            return SplineFromCardinal(p0, p1, p2, p3, 0.5f);
            // float3 pb1 = p1 + (1.0f / 6.0f) * (p2 - p0);
            // float3 pb2 = p2 - (1.0f / 6.0f) * (p3 - p1);

            // return SplineFromBezier(p1, pb1, pb2, p2);
        }

        // https://www.cubic.org/docs/hermite.htm
        [BurstCompile]
        public static Spline SplineFromCardinal(float3 p0, float3 p1, float3 p2, float3 p3, float tension)
        {
            Debug.Assert(tension >= 0.0f && tension <= 1.0f);

            float3 v0 = (p2 - p0) * tension;
            float3 v1 = (p3 - p1) * tension;

            return SplineFromHermite(p0, p1, v0, v1); 

        }

        [BurstCompile]
        public static Spline SplineFromReverse(Spline spline)
        {
            return new Spline(
                new float4(spline.xb.w, spline.xb.z, spline.xb.y, spline.xb.x),
                new float4(spline.yb.w, spline.yb.z, spline.yb.y, spline.yb.x),
                new float4(spline.zb.w, spline.zb.z, spline.zb.y, spline.zb.x)
            );
        }

        [BurstCompile]
        public static Spline SplineFromPointIndices(ref NativeArray<float3> points, int i0, int i1, int i2, int i3, float tension)
        {
            Debug.Assert(points.Length > i0);
            Debug.Assert(points.Length > i1);
            Debug.Assert(points.Length > i2);
            Debug.Assert(points.Length > i3);

            float3 p0 = points[i0];
            float3 p1 = points[i1];
            float3 p2 = points[i2];
            float3 p3 = points[i3];

            float s = (1.0f - tension) * (1.0f / 6.0f);

            float3 pb1 = p1 + s * (p2 - p0);
            float3 pb2 = p2 - s * (p3 - p1);

            return SplineFromBezier(p1, pb1, pb2, p2);
        }

        public static int SplinesCountFromPointsCount(int pointsCount)
        {
            return (pointsCount < 2) ? pointsCount : (pointsCount - 1);
        }

        [BurstCompile]
        public static void SplinesFromPoints(ref NativeArray<Spline> splines, out int splinesCount, ref NativeArray<float3> points, int pointsCount, float tension)
        {
            Debug.Assert(points.Length >= pointsCount);
            Debug.Assert(splines.Length >= SplinesCountFromPointsCount(pointsCount));

            splinesCount = 0;
            switch (pointsCount)
            {
                case 0: splinesCount = 0; return;
                case 1:
                    splinesCount = 1;
                    splines[0] = SplineFromPointIndices(ref points, 0, 0, 0, 0, tension);
                    return;
                case 2:
                    splinesCount = 1;
                    splines[0] = SplineFromPointIndices(ref points, 0, 0, 1, 1, tension);
                    return;
                default: break;
            }

            splines[splinesCount++] = SplineFromPointIndices(ref points, 0, 0, 1, 2, tension);

            int i = 0;
            for (int iLen = pointsCount - 3; i < iLen; ++i)
            {
                splines[splinesCount++] = SplineFromPointIndices(ref points, i + 0, i + 1, i + 2, i + 3, tension);
            }

            splines[splinesCount++] = SplineFromPointIndices(ref points, i + 0, i + 1, i + 2, i + 2, tension);
        }

        [BurstCompile]
        public static int SplinesFromBezier(NativeArray<Spline> splines, NativeArray<float3> points, NativeArray<float3> hullPoints, int pointCount, bool split)
        {
            int splineCount = split ? (pointCount / 2) : (pointCount - 1);
            int stride = split ? 2 : 1;

            Debug.Assert(splines.Length >= splineCount);
            for (int i = 0, iLen = splineCount; i < iLen; ++i)
            {
                splines[i] = SplineFromBezier(points[i * stride + 0], hullPoints[i * stride + 0], hullPoints[i * stride + 1], points[i * stride + 1]);
            }
            return splineCount;
        }

        [BurstCompile]
        public static int SplinesFromHermite(NativeArray<Spline> splines, NativeArray<float3> points, NativeArray<float3> tangents, int pointCount, bool split)
        {
            int splineCount = split ? (pointCount / 2) : (pointCount - 1);
            int stride = split ? 2 : 1;

            Debug.Assert(splines.Length >= splineCount);
            for (int i = 0, iLen = splineCount; i < iLen; ++i)
            {
                splines[i] = SplineFromHermite(points[i * stride + 0], points[i * stride + 1], tangents[i * stride + 0], tangents[i * stride + 1]);
            }
            return splineCount;
        }

        [BurstCompile]
        public static float3 EvaluateSplineFromWeights(Spline spline, float4 w)
        {
            return new float3(
                math.dot(spline.xb, w),
                math.dot(spline.yb, w),
                math.dot(spline.zb, w)
            );
        }

        [BurstCompile]
        public static float4 ComputeBezierWeightsFromScalar(float t)
        {
            float s = 1.0f - t;

            float t2 = t * t;
            float t3 = t2 * t;

            float s2 = s * s;
            float s3 = s2 * s;

            return new float4(s3, 3.0f * s2 * t, 3.0f * s * t2, t3);
        }

        [BurstCompile]
        public static float4 ComputeBezierWeightsFromVector(float4 t)
        {
            return new float4(
                t.x - 3.0f * t.y + 3.0f * t.z - t.w,
                3.0f * t.y - 6.0f * t.z + 3.0f * t.w,
                3.0f * t.z - 3.0f * t.w,
                t.w
            );
        }

        [BurstCompile]
        public static float3 EvaluatePositionFromT(Spline spline, float t)
        {
            return EvaluateSplineFromWeights(spline, ComputeBezierWeightsFromScalar(t));
        }

        [BurstCompile]
        public static float3 EvaluateVelocityFromT(Spline spline, float t)
        {
            float4 dt4 = new float4(0, 1, 2.0f * t, 3.0f * t * t);
            return EvaluateSplineFromWeights(spline, ComputeBezierWeightsFromVector(dt4));
        }

        [BurstCompile]
        public static float3 EvaluateAccelerationFromT(Spline spline, float t)
        {
            float4 ddt4 = new float4(0, 0, 2, 6.0f * t);

            return EvaluateSplineFromWeights(spline, ComputeBezierWeightsFromVector(ddt4));
        }

        [BurstCompile]
        public static float EvaluateCurvatureFromT(Spline spline, float t)
        {
            float3 v = EvaluateVelocityFromT(spline, t);
            float3 a = EvaluateAccelerationFromT(spline, t);

            float avCrossLen = math.length(math.cross(v, a));
            float vLen = math.length(v);
            if (vLen == 0.0f) return 1e10f;

            return avCrossLen / (vLen * vLen * vLen);
        }

        [BurstCompile]
        public static float ComputeLengthEstimateFromConvexHull(out float error, Spline s)
        {
            // Our convex hull is p0, p1, p2, p3, so p0_p3 is our minimum possible length, and p0_p1 + p1_p2 + p2_p3 our maximum.
            float d03 = (s.xb.x - s.xb.w) * (s.xb.x - s.xb.w) + (s.yb.x - s.yb.w) * (s.yb.x - s.yb.w) + (s.zb.x - s.zb.w) * (s.zb.x - s.zb.w);

            float d01 = (s.xb.x - s.xb.y) * (s.xb.x - s.xb.y) + (s.yb.x - s.yb.y) * (s.yb.x - s.yb.y) + (s.zb.x - s.zb.y) * (s.zb.x - s.zb.y);
            float d12 = (s.xb.y - s.xb.z) * (s.xb.y - s.xb.z) + (s.yb.y - s.yb.z) * (s.yb.y - s.yb.z) + (s.zb.y - s.zb.z) * (s.zb.y - s.zb.z);
            float d23 = (s.xb.z - s.xb.w) * (s.xb.z - s.xb.w) + (s.yb.z - s.yb.w) * (s.yb.z - s.yb.w) + (s.zb.z - s.zb.w) * (s.zb.z - s.zb.w);

            float minLength = math.sqrt(d03);
            float maxLength = math.sqrt(d01) + math.sqrt(d12) + math.sqrt(d23);

            minLength *= 0.5f;
            maxLength *= 0.5f;

            error = maxLength - minLength;
            return minLength + maxLength;
        }

        [BurstCompile]
        public static float ComputeLengthEstimate(Spline s, float errorThreshold, int depthThreshold = 32, int depth = 0)
        {
            float lengthEstimate = ComputeLengthEstimateFromConvexHull(out float error, s);
            if (error < errorThreshold || depth >= depthThreshold) { return lengthEstimate; }

            ComputeSplit(out Spline s0, out Spline s1, s);
            return ComputeLengthEstimate(s0, errorThreshold, depthThreshold, depth + 1) + ComputeLengthEstimate(s1, errorThreshold, depthThreshold, depth + 1);
        }

        [BurstCompile]
        public static void ComputeSplit(out Spline s0, out Spline s1, Spline s)
        {
            ComputeSplitComponent(out s0.xb, out s1.xb, s.xb);
            ComputeSplitComponent(out s0.yb, out s1.yb, s.yb);
            ComputeSplitComponent(out s0.zb, out s1.zb, s.zb);
        }

        [BurstCompile]
        private static void ComputeSplitComponent(out float4 splineComponent0, out float4 splineComponent1, float4 splineComponent)
        {
            float q0 = (splineComponent.x + splineComponent.y) * 0.5f;
            float q1 = (splineComponent.y + splineComponent.z) * 0.5f;
            float q2 = (splineComponent.z + splineComponent.w) * 0.5f;

            float r0 = (q0 + q1) * 0.5f; // x + 2y + z / 4
            float r1 = (q1 + q2) * 0.5f; // y + 2z + w / 4

            float s0 = (r0 + r1) * 0.5f; // q0 + 2q1 + q2 / 4 = x+y + 2(y+z) + z+w / 8 = x + 3y + 3z + w

            float sx = splineComponent.x; // support aliasing
            float sw = splineComponent.w;

            splineComponent0 = new float4(sx, q0, r0, s0);
            splineComponent1 = new float4(s0, r1, q2, sw);
        }

        [BurstCompile]
        public static void ComputeSplitAtT(out Spline s0, out Spline s1, Spline s, float t)
        {
            ComputeSplitComponentAtT(out s0.xb, out s1.xb, s.xb, t);
            ComputeSplitComponentAtT(out s0.yb, out s1.yb, s.yb, t);
            ComputeSplitComponentAtT(out s0.zb, out s1.zb, s.zb, t);
        }

        [BurstCompile]
        private static void ComputeSplitComponentAtT(out float4 splineComponent0, out float4 splineComponent1, float4 splineComponent, float t)
        {
            float q0 = math.lerp(splineComponent.x, splineComponent.y, t);
            float q1 = math.lerp(splineComponent.y, splineComponent.z, t);
            float q2 = math.lerp(splineComponent.z, splineComponent.w, t);

            float r0 = math.lerp(q0, q1, t);
            float r1 = math.lerp(q1, q2, t);

            float s0 = math.lerp(r0, r1, t);

            float sx = splineComponent.x; // support aliasing
            float sw = splineComponent.w;

            splineComponent0 = new float4(sx, q0, r0, s0);
            splineComponent1 = new float4(s0, r1, q2, sw);
        }

        [BurstCompile]
        public static quaternion EvaluateRotationFromT(Spline spline, float t)
        {
            float3 splineVelocity = SplineMath.EvaluateVelocityFromT(spline, t);
            float3 splineForward = math.normalize(splineVelocity);
            float3 splineUp = math.abs(splineForward.y) < 0.999f ? new float3(0.0f, 1.0f, 0.0f) : new float3(1.0f, 0.0f, 0.0f);
            float3 splineTangent = math.normalize(math.cross(splineForward, splineUp));
            float3 splineBitangent = math.cross(splineTangent, splineForward);

            quaternion rotation = new quaternion(new float3x3(-splineTangent, splineBitangent, splineForward));
            return rotation;
        }

        [BurstCompile]
        public static quaternion EvaluateRotationWithRollFromT(Spline spline, quaternion q0, quaternion q1, float t)
        {
            float3 splineVelocity = SplineMath.EvaluateVelocityFromT(spline, t);
            float3 splineForward = math.normalize(splineVelocity);

            quaternion q = math.slerp(q0, q1, t);
            float3 splineBitangent = math.mul(q, new float3(0.0f, 1.0f, 0.0f));

            // Because quaternions have double coverage, it is ambiguous which direction a > 180 degree rotation goes.
            // Carefully preserve the sign by comparing to sign of bitangent out of q0 or q1 (dependant on which is closer).
            float splineBitangentSign = (math.mul((t < 0.5) ? q0 : q1, new float3(0.0f, 1.0f, 0.0f)).y >= 0.0f) ? 1.0f : -1.0f;
            splineBitangent.y = math.abs(splineBitangent.y) * splineBitangentSign;

            float3 splineTangent = math.normalize(math.cross(splineForward, splineBitangent));
            splineBitangent = math.cross(splineTangent, splineForward);

            quaternion rotation = new quaternion(new float3x3(-splineTangent, splineBitangent, splineForward));
            return rotation;
        }

        [BurstCompile]
        private static float ComputeTFromClosestPointOnLine(float3 point, float3 line0, float3 line1)
        {
            float3 w = line1 - line0;
            float3 v = point - line0;

            float vDotW = math.dot(v, w);
            if (vDotW <= 0.0f) { return 0.0f; }

            float wDotW = math.dot(w, w);
            if (vDotW >= wDotW) { return 1.0f; }

            return vDotW / wDotW;
        }

        [BurstCompile]
        private static void FindTFromClosestPointOnSplineNewtonRaphson(out float tOut, out float dOut, Spline spline, float3 point, float tIn, int iterationCount)
        {
            Debug.Assert(tIn >= 0.0f && tIn <= 1.0f);
            const float T_MAX = 1.0f - 1e-6f;

            float skLast = tIn;
            float sk = tIn;
            float dk = math.length(EvaluatePositionFromT(spline, sk) - point);
            const float WIDTH = 1e-3f;
            const float WIDTH_INVERSE = 1.0f / WIDTH;
            float maxJump = 0.5f; // Avoid jumping too far, leads to oscillation.

            for (int i = 0; i < iterationCount; ++i)
            {
                float ss = Mathf.Clamp(sk, WIDTH, 1.0f - WIDTH); // So we can interpolate from Newton's method.

                float d1 = math.length(EvaluatePositionFromT(spline, ss - WIDTH) - point);
                float d2 = math.length(EvaluatePositionFromT(spline, ss) - point);
                float d3 = math.length(EvaluatePositionFromT(spline, ss + WIDTH) - point);

                float g1 = (d2 - d1) * WIDTH_INVERSE;
                float g2 = (d3 - d2) * WIDTH_INVERSE;

                float grad = (d3 - d1) * 0.5f * WIDTH_INVERSE;
                float curv = (g2 - g1) * WIDTH_INVERSE;

                float sn; // Next candidate.

                if (curv > 0.0f) { sn = ss - grad / curv; } // If d' is heading towards a minima, apply NR for d'
                else if (grad != 0.0f) { sn = ss - d2 / grad; } // otherwise, apply for D.
                else { sn = sk; }

                sn = Mathf.Clamp(sn, sk - maxJump, sk + maxJump); // Avoid large steps, often unstable.

                // Only update our estimate if the new value is in range and closer.
                if (sn >= 0.0f && sn < T_MAX)
                {
                    float dn = math.length(EvaluatePositionFromT(spline, sn) - point);
                    if (dn < dk) // Only update sk if d actually gets smaller.
                    {
                        sk = sn;
                        dk = dn;
                    }
                }

                maxJump *= 0.5f; // Reduce on a schedule -- helps binary search back towards a jump that is valid.
                skLast = sk;
            }

            tOut = sk;
            dOut = dk;
        }

        [BurstCompile]
        public static void FindTFromClosestPointOnSpline(out float t, out float d, float3 point, Spline spline)
        {
            // Approximate T from straight line between start and end of spline.
            t = ComputeTFromClosestPointOnLine(point, spline.GetPosition0(), spline.GetPosition1());

            const int ITERATION_COUNT = 8;
            FindTFromClosestPointOnSplineNewtonRaphson(out t, out d, spline, point, t, ITERATION_COUNT);
        }

        // TODO: This is a fairly brute force method (checking all the spline segments).
        [BurstCompile]
        public static void FindTFromClosestPointOnSplines(out float t, out float d, out int splineIndex, float3 point, NativeArray<Spline> splines)
        {
            t = 0.0f;
            d = float.MaxValue;
            splineIndex = -1;
            for (int i = 0, iLen = splines.Length; i < iLen; ++i)
            {
                float currentT;
                float currentD;
                FindTFromClosestPointOnSpline(out currentT, out currentD, point, splines[i]);
                if (currentD < d)
                {
                    d = currentD;
                    t = currentT;
                    splineIndex = i;
                }
            }
        }

        [System.Serializable]
        public struct SplinesFollowState
        {
            public int index;
            public float t;
            public bool isComplete;

            public static SplinesFollowState SplinesFollowStateBegin()
            {
                SplinesFollowState state = new SplinesFollowState();
                state.index = 0;
                state.t = 0.0f;
                state.isComplete = false;
                return state;
            }
        } 

        [BurstCompile]
        public static float ComputeTFromDelta(Spline spline, float t, float delta)
        {
            float3 velocity = EvaluateVelocityFromT(spline, t);
            float velocityLength = math.length(velocity);
            float normalization = (velocityLength > 1e-5f) ? (1.0f / velocityLength) : 0.0f;
            return delta * normalization + t;
        }

        [BurstCompile]
        public static void NormalizeSplinesFollowState(ref SplinesFollowState state, int splinesCount)
        {
            float indexFloat = (float)state.index + state.t;
            float indexFloatMin = 0.0f;
            float indexFloatMax = (float)splinesCount;
            state.isComplete = ((indexFloat <= indexFloatMin) || (indexFloat >= indexFloatMax));

            // Stop follow state from ever quite going over into an out of range error with state.index.
            // This can happen if user resets isComplete to false and advances again.
            Debug.Assert((int)math.floor(indexFloatMax - 1e-3f) < splinesCount);
            indexFloat = math.clamp(indexFloat, indexFloatMin, indexFloatMax - 1e-3f);
            state.index = (int)math.floor(indexFloat);
            state.t = indexFloat - math.floor(indexFloat);
        }

        [BurstCompile]
        public static void AdvanceTFromDelta(ref SplinesFollowState state, ref NativeArray<Spline> splines, int splinesCount, float delta)
        {
            state.t = ComputeTFromDelta(splines[state.index], state.t, delta);
            NormalizeSplinesFollowState(ref state, splinesCount);
        }


        // IMPLEMENT THIS SHIT
        [System.Serializable]
        public struct SplineGraphFollowState
        {
            public float t;
            public UInt32 packed;

            public SplineGraphFollowState(float t, Int16 edgeIndex, int isComplete, int isReverse)
            {
                this.t = t;
                this.packed =
                    (((UInt32)isReverse) << 17)
                    | (((UInt32)isComplete) << 16)
                    | ((UInt32)(Int32)edgeIndex);
            }

            public Int16 DecodeEdgeIndex()
            {
                UInt32 mask = (UInt32)(1 << 16) - 1;
                return (Int16)(packed & mask);
            }

            public int DecodeIsComplete()
            {
                UInt32 mask = (UInt32)(1 << 16);
                return ((packed & mask) != 0) ? 1 : 0;
            }

            public int DecodeIsReverse()
            {
                UInt32 mask = (UInt32)(1 << 17);
                return ((packed & mask) != 0) ? 1 : 0;
            }

            public string DebugString()
            {
                string res = "SplineGraphFollowState { ";
                res += "t: " + t + ", ";
                res += "edgeIndex: " + DecodeEdgeIndex() + ", ";
                res += "isComplete: " + DecodeIsComplete() + ", ";
                res += "isReverse: " + DecodeIsReverse();
                res += "}";

                return res;
            }
        }

        // This is a brute force method (checking all of the spline segments).
        public static void FindTFromClosestPointOnSplineGraph(
            out float t,
            out float d,
            out Int16 edgeIndex,
            int isReverse,
            float3 point,
            NativeArray<DirectedVertex> vertices,
            int verticesCount,
            NativeArray<DirectedEdge> edgesParentToChild,
            NativeArray<DirectedEdge> edgesChildToParent,
            NativeArray<Spline> splinesParentToChild,
            NativeArray<Spline> splinesChildToParent)
        {
            t = 0.0f;
            d = float.MaxValue;
            edgeIndex = -1;
            for (Int16 v = 0; v < verticesCount; ++v)
            {
                DirectedVertex vertex = vertices[v];
                if (vertex.IsValid() == 0) { continue; }

                for (Int16 edgeIndexCurrent = vertex.childHead;
                    edgeIndexCurrent != -1;
                    edgeIndexCurrent = edgesParentToChild[edgeIndexCurrent].next)
                {
                    DirectedEdge edge = edgesParentToChild[edgeIndexCurrent];
                    Debug.Assert(edge.IsValid() == 1);

                    Spline spline = splinesParentToChild[edgeIndexCurrent];

                    float currentT;
                    float currentD;
                    FindTFromClosestPointOnSpline(out currentT, out currentD, point, spline);
                    if (currentD < d)
                    {
                        d = currentD;
                        t = currentT;
                        edgeIndex = edgeIndexCurrent;
                    }
                }
            }

            if ((isReverse == 1) && (edgeIndex != -1)) { t = 1.0f - t; }
        }

        // This is a brute force method (checking all of the spline segments).
        [BurstCompile]
        public static void FindTFromClosestPointOnSplineGraph(
            out float t,
            out float d,
            out Int16 edgeIndex,
            int isReverse,
            float3 point,
            float distanceThresholdMin,
            float distanceThresholdMax,
            NativeArray<DirectedVertex> vertices,
            int verticesCount,
            NativeArray<DirectedEdge> edgesParentToChild,
            NativeArray<Spline> splinesParentToChild,
            NativeArray<float3> splineBounds)
        {
            FindTFromClosestPointOnSplineGraph(
                out t,
                out d,
                out edgeIndex,
                out Int16 vertexIndexUnused,
                isReverse,
                point,
                distanceThresholdMin,
                distanceThresholdMax,
                vertices,
                verticesCount,
                edgesParentToChild,
                splinesParentToChild,
                splineBounds
            );
        }

        // This is a brute force method (checking all of the spline segments).
        [BurstCompile]
        public static void FindTFromClosestPointOnSplineGraph(
            out float t,
            out float d,
            out Int16 edgeIndex,
            out Int16 vertexIndex,
            int isReverse,
            float3 point,
            float distanceThresholdMin,
            float distanceThresholdMax,
            NativeArray<DirectedVertex> vertices,
            int verticesCount,
            NativeArray<DirectedEdge> edgesParentToChild,
            NativeArray<Spline> splinesParentToChild,
            NativeArray<float3> splineBounds)
        {
            t = 0.0f;
            d = float.MaxValue;
            edgeIndex = -1;
            vertexIndex = -1;
            for (Int16 v = 0; v < verticesCount; ++v)
            {
                DirectedVertex vertex = vertices[v];
                if (vertex.IsValid() == 0) { continue; }

                for (Int16 edgeIndexCurrent = vertex.childHead;
                    edgeIndexCurrent != -1;
                    edgeIndexCurrent = edgesParentToChild[edgeIndexCurrent].next)
                {

                    FindTFromClosestPointOnSplineGraphEdge(
                        out float currentT,
                        out float currentD,
                        edgeIndexCurrent,
                        point,
                        distanceThresholdMin,
                        distanceThresholdMax,
                        edgesParentToChild,
                        splinesParentToChild,
                        splineBounds
                    );

                    if (currentD >= distanceThresholdMin && currentD <= distanceThresholdMax && currentD < d)
                    {
                        d = currentD;
                        t = currentT;
                        edgeIndex = edgeIndexCurrent;
                        vertexIndex = v;
                    }
                }
            }

            if ((isReverse == 1) && (edgeIndex != -1)) { t = 1.0f - t; }
        }

        [BurstCompile]
        public static void FindTFromClosestPointOnSplineGraphEdge(
            out float t,
            out float d,
            Int16 edgeIndex,
            float3 point,
            float distanceThresholdMin,
            float distanceThresholdMax,
            NativeArray<DirectedEdge> edgesParentToChild,
            NativeArray<Spline> splinesParentToChild,
            NativeArray<float3> splineBounds)
        {
            t = 0.0f;
            d = float.MaxValue;

            DirectedEdge edge = edgesParentToChild[edgeIndex];
            Debug.Assert(edge.IsValid() == 1);

            Spline spline = splinesParentToChild[edgeIndex];

            float3 aabbMin = splineBounds[edgeIndex * 2 + 0];
            float3 aabbMax = splineBounds[edgeIndex * 2 + 1];

            // Dilate spline AABB by distance threshold.
            aabbMin -= new float3(distanceThresholdMax, distanceThresholdMax, distanceThresholdMax);
            aabbMax += new float3(distanceThresholdMax, distanceThresholdMax, distanceThresholdMax);

            // If dilated spline AABB does not contain point, then there is no point along spline that is within distanceThresholdMax from point.
            // Early out.
            if (point.x > aabbMax.x || point.y > aabbMax.y || point.z > aabbMax.z
                || point.x < aabbMin.x || point.y < aabbMin.y || point.z < aabbMin.z)
            {
                return;
            }

            FindTFromClosestPointOnSpline(out t, out d, point, spline);
        }

        // Same as FindTFromClosestPointOnSplineGraph but only searches within the parent and child edges for the passed in edgeIndex.
        // Warning: This function has no bookkeeping to track feedback loops where children point back to already checked parents.
        // For a single parent child neighborhood, this should rarely result in duplicate checks (in practice).
        // The only time a duplicate check should occur would be with this topology:
        //
        //   --------
        //  /        \
        // +          +
        //  \        /
        //   --------
        //
        [BurstCompile]
        public static void FindTFromClosestPointOnSplineGraphNeighborhood(
            out float t,
            out float d,
            out Int16 edgeIndex,
            out Int16 vertexIndex,
            Int16 edgeIndexNeighborhoodCenter,
            int isReverse,
            float3 point,
            float distanceThresholdMin,
            float distanceThresholdMax,
            NativeArray<DirectedVertex> vertices,
            int verticesCount,
            NativeArray<DirectedEdge> edgesParentToChild,
            NativeArray<DirectedEdge> edgesChildToParent,
            NativeArray<Spline> splinesParentToChild,
            NativeArray<float3> splineBounds)
        {
            t = 0.0f;
            d = float.MaxValue;
            edgeIndex = -1;
            vertexIndex = -1;

            Int16 vertexIndexChild = edgesParentToChild[edgeIndexNeighborhoodCenter].vertexIndex;
            DirectedVertex vertexChild = vertices[vertexIndexChild];
            Debug.Assert(vertexChild.IsValid() == 1);

            Int16 vertexIndexParent = edgesChildToParent[edgeIndexNeighborhoodCenter].vertexIndex;
            DirectedVertex vertexParent = vertices[vertexIndexParent];
            Debug.Assert(vertexParent.IsValid() == 1);

            // First: Check the neighborhood center edge.
            Int16 edgeIndexCurrent = edgeIndexNeighborhoodCenter;
            {
                FindTFromClosestPointOnSplineGraphEdge(
                    out float currentT,
                    out float currentD,
                    edgeIndexCurrent,
                    point,
                    distanceThresholdMin,
                    distanceThresholdMax,
                    edgesParentToChild,
                    splinesParentToChild,
                    splineBounds
                );

                if (currentD >= distanceThresholdMin && currentD <= distanceThresholdMax && currentD < d)
                {
                    d = currentD;
                    t = currentT;
                    edgeIndex = edgeIndexCurrent;
                    vertexIndex = vertexIndexParent;
                }
            }

            // Then check the neighborhood center edge's child vertex children.
            for (edgeIndexCurrent = vertexChild.childHead;
                edgeIndexCurrent != -1;
                edgeIndexCurrent = edgesParentToChild[edgeIndexCurrent].next)
            {
                FindTFromClosestPointOnSplineGraphEdge(
                    out float currentT,
                    out float currentD,
                    edgeIndexCurrent,
                    point,
                    distanceThresholdMin,
                    distanceThresholdMax,
                    edgesParentToChild,
                    splinesParentToChild,
                    splineBounds
                );

                if (currentD >= distanceThresholdMin && currentD <= distanceThresholdMax && currentD < d)
                {
                    d = currentD;
                    t = currentT;
                    edgeIndex = edgeIndexCurrent;
                    vertexIndex = vertexIndexChild;
                }
            }

            // Last, check the neighborhood center edge's parent vertex parents.
            for (edgeIndexCurrent = vertexParent.parentHead;
                edgeIndexCurrent != -1;
                edgeIndexCurrent = edgesChildToParent[edgeIndexCurrent].next)
            {
                FindTFromClosestPointOnSplineGraphEdge(
                    out float currentT,
                    out float currentD,
                    edgeIndexCurrent,
                    point,
                    distanceThresholdMin,
                    distanceThresholdMax,
                    edgesParentToChild,
                    splinesParentToChild,
                    splineBounds
                );

                if (currentD >= distanceThresholdMin && currentD <= distanceThresholdMax && currentD < d)
                {
                    d = currentD;
                    t = currentT;
                    edgeIndex = edgeIndexCurrent;
                    vertexIndex = edgesChildToParent[edgeIndexCurrent].vertexIndex;
                }
            }

            if ((isReverse == 1) && (edgeIndex != -1)) { t = 1.0f - t; }
        }

        [BurstCompile]
        public static void NormalizeSplineGraphFollowState(
            ref SplineGraphFollowState state,
            ref Unity.Mathematics.Random random,
            NativeArray<DirectedVertex> vertices,
            int verticesCount,
            NativeArray<DirectedEdge> edgesParentToChild,
            NativeArray<DirectedEdge> edgesChildToParent)
        {
            int isComplete = state.DecodeIsComplete();
            if (isComplete != 0) { return; }
            int isReverse = state.DecodeIsReverse();
            Int16 edgeIndex = state.DecodeEdgeIndex();
            NativeArray<DirectedEdge> edges = (isReverse == 0) ? edgesParentToChild : edgesChildToParent;
            NativeArray<DirectedEdge> edgesBackward = (isReverse == 0) ? edgesChildToParent : edgesParentToChild;

            float t = state.t;
            while (t < 0.0f)
            {
                t += 1.0f;

                Int16 vertexIndex = edgesBackward[edgeIndex].vertexIndex;
                DirectedVertex vertex = vertices[vertexIndex];

                // TODO: Precompute and store edge count?
                int edgeCount = 0;
                for (Int16 edgeIndexNext = (isReverse == 0) ? vertex.parentHead : vertex.childHead;
                    edgeIndexNext != -1;
                    edgeIndexNext = edgesBackward[edgeIndexNext].next)
                {
                    ++edgeCount;
                }

                if (edgeCount == 0)
                {
                    // No children. We are done.
                    isComplete = 1;
                    state = new SplineGraphFollowState(0.0f, edgeIndex, isComplete, isReverse);
                    return;
                }

                float randomSample = random.NextFloat();
                int edgeSelectIndex = (int)math.floor(((float)edgeCount - 1.0f) * randomSample + 0.5f);

                edgeCount = 0;
                for (Int16 edgeIndexNext = (isReverse == 0) ? vertex.parentHead : vertex.childHead;
                    edgeIndexNext != -1;
                    edgeIndexNext = edgesBackward[edgeIndexNext].next)
                {
                    if (edgeCount == edgeSelectIndex)
                    {
                        edgeIndex = edgeIndexNext;
                        break;
                    }
                    ++edgeCount;
                }
            }

            while (t > 1.0f)
            {
                t -= 1.0f;

                Int16 vertexIndex = edges[edgeIndex].vertexIndex;
                DirectedVertex vertex = vertices[vertexIndex];

                // TODO: Precompute and store edge count?
                int edgeCount = 0;
                for (Int16 edgeIndexNext = (isReverse == 0) ? vertex.childHead : vertex.parentHead;
                    edgeIndexNext != -1;
                    edgeIndexNext = edges[edgeIndexNext].next)
                {
                    ++edgeCount;
                }

                if (edgeCount == 0)
                {
                    // No children. We are done.
                    isComplete = 1;
                    state = new SplineGraphFollowState(1.0f, edgeIndex, isComplete, isReverse);
                    return;
                }

                float randomSample = random.NextFloat();
                int edgeSelectIndex = (int)math.floor(((float)edgeCount - 1.0f) * randomSample + 0.5f);

                edgeCount = 0;
                for (Int16 edgeIndexNext = (isReverse == 0) ? vertex.childHead : vertex.parentHead;
                    edgeIndexNext != -1;
                    edgeIndexNext = edges[edgeIndexNext].next)
                {
                    if (edgeCount == edgeSelectIndex)
                    {
                        edgeIndex = edgeIndexNext;
                        break;
                    }
                    ++edgeCount;
                }
            }

            state = new SplineGraphFollowState(t, edgeIndex, isComplete, isReverse);
        }

        [BurstCompile]
        public static void AdvanceTFromDelta(
            ref SplineGraphFollowState state,
            ref Unity.Mathematics.Random random,
            float delta,
            NativeArray<DirectedVertex> vertices,
            int verticesCount,
            NativeArray<DirectedEdge> edgesParentToChild,
            NativeArray<DirectedEdge> edgesChildToParent,
            NativeArray<Spline> splinesParentToChild,
            NativeArray<Spline> splinesChildToParent)
        {
            int isComplete = state.DecodeIsComplete();
            if (isComplete == 1) { return; }
            int isReverse = state.DecodeIsReverse();
            Int16 edgeIndex = state.DecodeEdgeIndex();
            Spline spline = (isReverse == 0) ? splinesParentToChild[edgeIndex] : splinesChildToParent[edgeIndex];
            state = new SplineGraphFollowState(ComputeTFromDelta(spline, state.t, delta), edgeIndex, isComplete, isReverse);

            NormalizeSplineGraphFollowState(
                ref state,
                ref random,
                vertices,
                verticesCount,
                edgesParentToChild,
                edgesChildToParent
            );
        }

        [BurstCompile]
        public struct SplineGraphIteratorState
        {
            public float distance;
            public float t;
            public Int16 vertexIndex;
            public Int16 edgeIndex;

            public static SplineGraphIteratorState zero = new SplineGraphIteratorState
            {
                distance = 0.0f,
                t = 0.0f,
                vertexIndex = -1,
                edgeIndex = -1
            };
        }

        [BurstCompile]
        public static bool SplineGraphIteratorForward(
            ref SplineGraphIteratorState state,
            NativeArray<DirectedVertex> vertices,
            int verticesCount,
            NativeArray<DirectedEdge> edgesParentToChild,
            NativeArray<Spline> splinesParentToChild,
            NativeArray<float> edgeLengths,
            float errorThreshold = 1e-5f,
            int depthThreshold = 32
        )
        {
            Debug.Assert(state.distance > 0.0f);
            Debug.Assert(state.vertexIndex != -1);
            Debug.Assert(state.edgeIndex != -1);
            Debug.Assert(state.t >= 0.0f && state.t <= 1.0f);

            Spline spline = splinesParentToChild[state.edgeIndex];
            float edgeLengthRemaining = edgeLengths[state.edgeIndex]; // Fast path.
            if (state.t > 1e-5f)
            {
                ComputeSplitAtT(out Spline s0, out Spline s1, spline, state.t);
                edgeLengthRemaining = ComputeLengthEstimate(s1, errorThreshold, depthThreshold);
            }
            
            float distanceNext = state.distance - edgeLengthRemaining;
            if (distanceNext >= 0.0f)
            {
                // Advance to the next spline.
                Int16 vertexIndexNext = edgesParentToChild[state.edgeIndex].vertexIndex;
                DirectedVertex vertexNext = vertices[vertexIndexNext];
                Debug.Assert(vertexNext.IsValid() == 1);
                if (vertexNext.childHead == -1)
                {
                    // We have reached a dead end.
                    state = new SplineGraphIteratorState
                    { 
                        distance = distanceNext,
                        t = 1.0f,
                        vertexIndex = state.vertexIndex,
                        edgeIndex = state.edgeIndex 
                    };
                    return false;
                }
                else
                {
                    state = new SplineGraphIteratorState
                    { 
                        distance = distanceNext,
                        t = 0.0f,
                        vertexIndex = vertexIndexNext,
                        edgeIndex = -1 // Caller will now need to choose which child edge index to traverse into. 
                    };
                    return true;
                }
                
            }

            // Reaching the end within our current spline.
            int sampleCount = (int)math.ceil(state.distance / edgeLengths[state.edgeIndex] * 1024.0f);
            sampleCount = math.min(sampleCount, 1024);
            float sampleCountInverse = 1.0f / (float)sampleCount;
            float nextT = state.t;
            for (int s = 0; s < sampleCount; ++s)
            {
                nextT = ComputeTFromDelta(spline, nextT, state.distance * sampleCountInverse);
            }
            Debug.Assert(nextT <= 1.0f);
            nextT = math.saturate(nextT); // saturate is not strictly necessary - it is simply performed to clean up precision issues.
            state = new SplineGraphIteratorState
            {
                distance = 0.0f,
                t = nextT,
                vertexIndex = state.vertexIndex,
                edgeIndex = state.edgeIndex
            };
            return false;
        }

        [BurstCompile]
        public static bool SplineGraphIteratorReverse(
            ref SplineGraphIteratorState state,
            NativeArray<DirectedVertex> vertices,
            int verticesCount,
            NativeArray<DirectedEdge> edgesChildToParent,
            NativeArray<Spline> splinesChildToParent,
            NativeArray<float> edgeLengths,
            float errorThreshold = 1e-5f,
            int depthThreshold = 32
        )
        {
            Debug.Assert(state.distance > 0.0f);
            Debug.Assert(state.vertexIndex != -1);
            Debug.Assert(state.edgeIndex != -1);
            Debug.Assert(state.t >= 0.0f && state.t <= 1.0f);

            Spline spline = splinesChildToParent[state.edgeIndex];
            float edgeLengthRemaining = edgeLengths[state.edgeIndex]; // Fast path.
            if (state.t > 1e-5f)
            {
                ComputeSplitAtT(out Spline s0, out Spline s1, spline, state.t);
                edgeLengthRemaining = ComputeLengthEstimate(s1, errorThreshold, depthThreshold);
            }
            
            float distanceNext = state.distance - edgeLengthRemaining;
            if (distanceNext >= 0.0f)
            {
                // Advance to the next spline.
                Int16 vertexIndexNext = edgesChildToParent[state.edgeIndex].vertexIndex;
                DirectedVertex vertexNext = vertices[vertexIndexNext];
                Debug.Assert(vertexNext.IsValid() == 1);
                if (vertexNext.parentHead == -1)
                {
                    // We have reached a dead end.
                    state = new SplineGraphIteratorState
                    { 
                        distance = distanceNext,
                        t = 1.0f,
                        vertexIndex = state.vertexIndex,
                        edgeIndex = state.edgeIndex 
                    };
                    return false;
                }
                else
                {
                    state = new SplineGraphIteratorState
                    { 
                        distance = distanceNext,
                        t = 0.0f,
                        vertexIndex = vertexIndexNext,
                        edgeIndex = -1 // Caller will now need to choose which child edge index to traverse into. 
                    };
                    return true;
                }
                
            }

            // Reaching the end within our current spline.
            int sampleCount = (int)math.ceil(state.distance / edgeLengths[state.edgeIndex] * 1024.0f);
            sampleCount = math.min(sampleCount, 1024);
            float sampleCountInverse = 1.0f / (float)sampleCount;
            float nextT = state.t;
            for (int s = 0; s < sampleCount; ++s)
            {
                nextT = ComputeTFromDelta(spline, nextT, state.distance * sampleCountInverse);
            }
            Debug.Assert(nextT <= 1.0f);
            nextT = math.saturate(nextT); // saturate is not strictly necessary - it is simply performed to clean up precision issues.
            state = new SplineGraphIteratorState
            {
                distance = 0.0f,
                t = nextT,
                vertexIndex = state.vertexIndex,
                edgeIndex = state.edgeIndex
            };
            return false;
        }

        [BurstCompile]
        public static void ComputePositionRotationLeashFromT(
            out float3 position,
            out quaternion rotation,
            out float2 leash,
            Int16 vertexIndexParent,
            Int16 vertexIndexChild,
            Int16 edgeIndex,
            float t,
            NativeArray<float3> positions,
            NativeArray<quaternion> rotations,
            NativeArray<float2> leashes,
            NativeArray<SplineMath.Spline> splines,
            NativeArray<SplineMath.Spline> splineLeashes
            )
        {
            SplineMath.Spline spline = splines[edgeIndex];
            quaternion rotationParent = rotations[vertexIndexParent];
            quaternion rotationChild = rotations[vertexIndexChild];
            SplineMath.Spline splineLeash = splineLeashes[edgeIndex];

            float3 positionOnSpline = SplineMath.EvaluatePositionFromT(spline, t);
            quaternion rotationOnSpline = SplineMath.EvaluateRotationWithRollFromT(spline, rotationParent, rotationChild, t);         
            float2 leashMaxOS = SplineMath.EvaluatePositionFromT(splineLeash, t).xy;

            position = positionOnSpline;
            rotation = rotationOnSpline;
            leash = leashMaxOS;
        }
    }
}