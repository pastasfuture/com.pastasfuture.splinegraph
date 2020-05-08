using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using System;

namespace Pastasfuture.SplineGraph.Runtime
{
    public class SplineGraphFollowManager : MonoBehaviour
    {
        public SplineGraphManager splineGraphManager;
        public GameObject[] followPrefabs;
        public int requestedCount = 32;
        public int requestedCapacity = 32;
        public float velocityMin = 0.25f;
        public float velocityMax = 1.0f;
        public bool isTwoWayPathEnabled = true;
        public float leashNormalizedMin = 0.0f;
        public float leashNormalizedMax = 1.0f;
        [System.NonSerialized] private FollowPool followPool = null; // Instantiate OnEnable()
        private int count = 0;
        [System.NonSerialized] private NativeArray<float3> positions;
        [System.NonSerialized] private NativeArray<quaternion> rotations;
        [System.NonSerialized] private NativeArray<float> scales;
        [System.NonSerialized] private NativeArray<float2> leashes;
        [System.NonSerialized] private NativeArray<SplineMath.SplineGraphFollowState> followStates;
        [System.NonSerialized] private NativeArray<Unity.Mathematics.Random> randoms;
        [System.NonSerialized] private NativeArray<float> velocities;
        [System.NonSerialized] private NativeArray<float> ages;
        [System.NonSerialized] private NativeArray<float> lifetimes;
        [System.NonSerialized] private NativeArray<bool> facingReverseIsEnabled;
        // [System.NonSerialized] private NativeArray<bool> isActives;

        private class FollowInstanceData
        {
            public GameObject gameObject;
            public Renderer renderer;
        }

        private class FollowPool
        {
            public List<FollowInstanceData> followInstanceData = new List<FollowInstanceData>();
            public int isActiveCount = 0;

            public FollowPool(int capacity, GameObject[] prefabs, Transform root)
            {
                isActiveCount = 0;

                for (int i = 0, iLen = capacity; i < iLen; ++i)
                {
                    AllocateInstance(prefabs[i % prefabs.Length], root);
                }
            }

            public void Dispose()
            {
                for (int i = 0, iLen = followInstanceData.Count; i < iLen; ++i)
                {
                    Destroy(followInstanceData[i].gameObject);
                }
                followInstanceData.Clear();
            }

            private void AllocateInstance(GameObject prefab, Transform parent)
            {
                var instance = new FollowInstanceData();

                if (parent != null)
                {
                    instance.gameObject = Instantiate(prefab, parent);
                    instance.gameObject.SetActive(false);
                    instance.renderer = instance.gameObject.GetComponentInChildren<Renderer>(includeInactive: true);
                    Debug.Assert(instance.renderer != null);
                }

                followInstanceData.Add(instance);
            }

            public FollowInstanceData EnableInstanceNext()
            {
                Debug.Assert(isActiveCount < followInstanceData.Count);
                
                FollowInstanceData instance = followInstanceData[isActiveCount++];

                instance.gameObject.SetActive(true);

                return instance;
            }

            public void DisableInstance(FollowInstanceData instance)
            {
                int instanceIndex = followInstanceData.IndexOf(instance);
                Debug.Assert(instanceIndex >= 0 && instanceIndex < isActiveCount);

                --isActiveCount;
                if (isActiveCount > 0)
                {
                    FollowInstanceData instanceActiveLast = followInstanceData[isActiveCount];
                    followInstanceData[instanceIndex] = instanceActiveLast;
                    followInstanceData[isActiveCount] = instance;
                }

                instance.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            count = 0;
            EnsureCapacity(requestedCapacity, Allocator.Persistent);
        }

        void OnEnable()
        {
            followPool = new FollowPool(requestedCapacity, followPrefabs, this.transform);
        }

        void OnDisable()
        {
            followPool.Dispose();
        }

        private void Dispose()
        {
            if (positions != null && positions.Length > 0) { positions.Dispose(); }
            if (rotations != null && rotations.Length > 0) { rotations.Dispose(); }
            if (scales != null && scales.Length > 0) { scales.Dispose(); }
            if (leashes != null && leashes.Length > 0) { leashes.Dispose(); }
            if (followStates != null && followStates.Length > 0) { followStates.Dispose(); }
            if (randoms != null && randoms.Length > 0) { randoms.Dispose(); }
            if (velocities != null && velocities.Length > 0) { velocities.Dispose(); }
            if (ages != null && ages.Length > 0) { ages.Dispose(); }
            if (lifetimes != null && lifetimes.Length > 0) { lifetimes.Dispose(); }
            if (facingReverseIsEnabled != null && facingReverseIsEnabled.Length > 0) { facingReverseIsEnabled.Dispose(); }
        }

        private void EnsureCapacity(int capacity, Unity.Collections.Allocator allocator)
        {
            Debug.Assert(capacity > 0);

            if (positions != null && positions.Length > capacity)
            {
                return;
            }

            if (positions != null)
            {
                Dispose();
            }

            positions = new NativeArray<float3>(capacity, allocator);
            rotations = new NativeArray<quaternion>(capacity, allocator);
            scales = new NativeArray<float>(capacity, allocator);
            leashes = new NativeArray<float2>(capacity, allocator);
            followStates = new NativeArray<SplineMath.SplineGraphFollowState>(capacity, allocator);
            randoms = new NativeArray<Unity.Mathematics.Random>(capacity, allocator);
            velocities = new NativeArray<float>(capacity, allocator);
            ages = new NativeArray<float>(capacity, allocator);
            lifetimes = new NativeArray<float>(capacity, allocator);
            facingReverseIsEnabled = new NativeArray<bool>(capacity, allocator);
        }

        private void Swap(int a, int b)
        {
            int capacity = positions.Length;
            Debug.Assert(a >= 0 && a < capacity);
            Debug.Assert(b >= 0 && b < capacity);

            float3 positionTemp = positions[b];
            positions[b] = positions[a];
            positions[a] = positionTemp;

            quaternion rotationTemp = rotations[b];
            rotations[b] = rotations[a];
            rotations[a] = rotationTemp;

            float scaleTemp = scales[b];
            scales[b] = scales[a];
            scales[a] = scaleTemp;

            float2 leashTemp = leashes[b];
            leashes[b] = leashes[a];
            leashes[a] = leashTemp;

            SplineMath.SplineGraphFollowState followStateTemp = followStates[b];
            followStates[b] = followStates[a];
            followStates[a] = followStateTemp;

            Unity.Mathematics.Random randomTemp = randoms[b];
            randoms[b] = randoms[a];
            randoms[a] = randomTemp;

            float velocityTemp = velocities[b];
            velocities[b] = velocities[a];
            velocities[a] = velocityTemp;

            float ageTemp = ages[b];
            ages[b] = ages[a];
            ages[a] = ageTemp;

            float lifetimeTemp = lifetimes[b];
            lifetimes[b] = lifetimes[a];
            lifetimes[a] = lifetimeTemp;

            bool facingReverseIsEnabledTemp = facingReverseIsEnabled[b];
            facingReverseIsEnabled[b] = facingReverseIsEnabled[a];
            facingReverseIsEnabled[a] = facingReverseIsEnabledTemp;
        }

        void OnDestroy()
        {
            Dispose();
        }

        void Update()
        {
            Debug.Assert(splineGraphManager != null, "Error: CarSpawningManager: Need to assign SplineGraphManager.");
            DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph = splineGraphManager.GetSplineGraph();

            if (splineGraph.vertices.count == 0)
            {
                // No paths to spawn vehicles on. Early out.
                return;
            }

            NativeArray<float3> splineBounds = splineGraphManager.GetSplineBounds(Allocator.Persistent);

            EnsureCapacity(requestedCount, Allocator.Persistent);
            Spawn(Time.deltaTime, ref splineGraph, ref splineBounds);
            Follow(Time.deltaTime, ref splineGraph, ref splineBounds);
            Despawn();
            Present();
        }

        //
        void Spawn(float deltaTime, ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, ref NativeArray<float3> splineBounds)
        {
            if (count >= requestedCount) { return; }

            for (; count < requestedCount; ++count)
            {
                float t = UnityEngine.Random.value;
                Int16 edgeIndex = (Int16)Mathf.FloorToInt((splineGraph.edgePoolChildren.data.Length - 1) * UnityEngine.Random.value + 0.5f );
                int isComplete = 0;
                int isReverse = isTwoWayPathEnabled ? ((UnityEngine.Random.value >= 0.5f) ? 1 : 0) : 0;
                followStates[count] = new SplineMath.SplineGraphFollowState(t, edgeIndex, isComplete, isReverse);

                randoms[count] = new Unity.Mathematics.Random((uint)count + 1);
                velocities[count] = Mathf.Lerp(velocityMin, velocityMax, UnityEngine.Random.value);
                scales[count] = Mathf.Lerp(1.0f, 1.0f, UnityEngine.Random.value);
                leashes[count] = new float2(
                    Mathf.Lerp(leashNormalizedMin, leashNormalizedMax, math.pow(UnityEngine.Random.value, 2.0f)),
                    Mathf.Lerp(leashNormalizedMin, leashNormalizedMax, math.pow(UnityEngine.Random.value, 2.0f))
                );

                followPool.EnableInstanceNext();
            }
        }

        // Can be trivially parallelized.
        void Follow(float deltaTime, ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, ref NativeArray<float3> splineBounds)
        {
            for (int i = 0; i < count; ++i)
            {
                float positionDelta = math.length(velocities[i]) * deltaTime;

                SplineMath.SplineGraphFollowState followState = followStates[i];
                Unity.Mathematics.Random random = randoms[i];

                SplineMath.AdvanceTFromDelta(
                    ref followState,
                    ref random,
                    positionDelta,
                    splineGraph.vertices.data,
                    splineGraph.vertices.count,
                    splineGraph.edgePoolChildren.data,
                    splineGraph.edgePoolParents.data,
                    splineGraph.payload.edgeParentToChildSplines.data,
                    splineGraph.payload.edgeChildToParentSplines.data
                );

                followStates[i] = followState;
                randoms[i] = random;

                Int16 edgeIndex = followState.DecodeEdgeIndex();
                SplineMath.Spline spline = (followState.DecodeIsReverse() == 0)
                    ? splineGraph.payload.edgeParentToChildSplines.data[edgeIndex]
                    : splineGraph.payload.edgeChildToParentSplines.data[edgeIndex];

                SplineMath.Spline splineLeash = (followState.DecodeIsReverse() == 0)
                    ? splineGraph.payload.edgeParentToChildSplinesLeashes.data[edgeIndex]
                    : splineGraph.payload.edgeChildToParentSplinesLeashes.data[edgeIndex];

                Int16 vertexIndexChild = splineGraph.edgePoolChildren.data[edgeIndex].vertexIndex;
                Int16 vertexIndexParent = splineGraph.edgePoolParents.data[edgeIndex].vertexIndex;
                if (followState.DecodeIsReverse() == 1)
                {
                    Int16 vertexIndexTemp = vertexIndexChild;
                    vertexIndexChild = vertexIndexParent;
                    vertexIndexParent = vertexIndexTemp;
                }

                quaternion rotationParent = splineGraph.payload.rotations.data[vertexIndexParent];
                quaternion rotationChild = splineGraph.payload.rotations.data[vertexIndexChild];


                positions[i] = SplineMath.EvaluatePositionFromT(spline, followState.t);
                // rotations[i] = SplineMath.EvaluateRotationFromT(spline, followState.t);
                // rotations[i] = math.slerp(rotationParent, rotationChild, followState.t);
                rotations[i] = SplineMath.EvaluateRotationWithRollFromT(spline, rotationParent, rotationChild, followState.t);



                float2 leashMaxOS = SplineMath.EvaluatePositionFromT(splineLeash, followState.t).xy;
                float2 leashOS = leashMaxOS * leashes[i];
                float3 leashWS = math.mul(rotations[i], new float3(leashOS, 0.0f));
                positions[i] += leashWS;
            }
        }

        // Main thread merge.
        void Despawn()
        {
            for (int i = 0; i < count; ++i)
            {
                if (followStates[i].DecodeIsComplete() == 0) { continue; }

                if (count > 0) { Swap(i, count - 1); }

                followPool.DisableInstance(followPool.followInstanceData[count - 1]);
                
                --count;
            }
        }

        // Main thread game object interaction.
        void Present()
        {
            for (int i = 0; i < count; ++i)
            {
                followPool.followInstanceData[i].gameObject.transform.position = positions[i];
                followPool.followInstanceData[i].gameObject.transform.rotation = rotations[i];
                // followPool.followInstanceData[i].gameObject.transform.localScale = new float3(scales[i], scales[i], scales[i]);
            }
        }
    }
}