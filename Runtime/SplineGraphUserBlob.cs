using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;

namespace Pastasfuture.SplineGraph.Runtime
{
    public static class SplineGraphUserBlob
    {
        [StructLayout(LayoutKind.Explicit), System.Serializable]
        public struct Value
        {
            [FieldOffset(0)]
            public int i;

            [FieldOffset(0)]
            public uint u;

            [FieldOffset(0)]
            public float f;

            public string ToString(Scheme scheme)
            {
                switch (scheme.type)
                {
                    case Scheme.Type.Int: return i.ToString();
                    case Scheme.Type.UInt: return u.ToString();
                    case Scheme.Type.Float: return f.ToString();
                    default: Debug.Assert(false); return null;
                }
            }
        }

        [System.Serializable]
        public struct Scheme : IEquatable<Scheme>
        {
            [System.Serializable]
            public enum Type
            {
                Int = 0,
                UInt,
                Float,
                Count
            };

            public Type type;
            public int stride;
            public int offset;

            public override int GetHashCode()
            {
                int hash = type.GetHashCode();
                hash = hash * 7 + stride.GetHashCode();
                hash = hash * 7 + offset.GetHashCode();
                return hash;
            }

            public bool Equals(Scheme other)
            {
                return other.type == type
                    && other.stride == stride
                    && other.offset == offset;
            }

            public override bool Equals(object other)
            {
                return other is Scheme && Equals(other);
            }

            public static bool operator == (Scheme lhs, Scheme rhs)
            {
                return lhs.Equals(rhs);
            }

            public static bool operator !=(Scheme lhs, Scheme rhs)
            {
                return !lhs.Equals(rhs);
            }

            public Value Zero()
            {
                switch (type)
                {
                    case Scheme.Type.Int: return new Value { i = 0 };
                    case Scheme.Type.UInt: return new Value { u = 0 };
                    case Scheme.Type.Float: return new Value { f = 0.0f };
                    default: Debug.Assert(false); return new Value { i = 0 };
                }
            }
        }

#if UNITY_EDITOR
        public struct SchemeEditorOnlyInfo
        {
            public enum RangeMode
            {
                None = 0,
                MinMax,
                Color,
                ColorHDR
            }

            public RangeMode rangeMode;
            public Value min;
            public Value max;

            public static readonly SchemeEditorOnlyInfo zero = new SchemeEditorOnlyInfo
            {
                rangeMode = RangeMode.None,
                min = default,
                max = default
            };
        }
#endif

        public struct NativeSchemaBlob
        {
            public NativeArray<Scheme> schema;
            public NativeArray<Value> data;
            public int count;
            public int capacity;

            public static readonly NativeSchemaBlob zero = new NativeSchemaBlob { count = 0, capacity = 0 };

            public NativeSchemaBlob(ref NativeArray<Scheme> schemaIn, int capacityRequested, Allocator allocator)
            {
                schema = new NativeArray<Scheme>(schemaIn, allocator);
                ComputeOffsets(ref schema);

                count = 0;
                capacity = capacityRequested;
                data = default;
                Debug.Assert(!data.IsCreated);
                if (capacityRequested > 0)
                {
                    data = new NativeArray<Value>(capacityRequested * ComputePerElementValueCount(ref schema), allocator);
                }
            }

            public NativeSchemaBlob(Scheme[] schemaIn, int capacityRequested, Allocator allocator)
            {
                schema = new NativeArray<Scheme>(schemaIn, allocator);
                ComputeOffsets(ref schema);

                count = 0;
                capacity = capacityRequested;
                data = default;
                if (capacityRequested > 0)
                {
                    data = new NativeArray<Value>(capacityRequested * ComputePerElementValueCount(ref schema), allocator);
                }
            }

            public bool IsCreated { get => data.IsCreated || schema.IsCreated; }

            public void Dispose()
            {
                Clear();
                if (data.IsCreated) { data.Dispose(); }
                if (schema.IsCreated) { schema.Dispose(); }
                capacity = 0;
            }

            public void Clear()
            {
                count = 0;
            }

            public void Ensure(int capacityRequested, Allocator allocator)
            {
                if (capacityRequested == 0 && !data.IsCreated)
                {
                    // Need to handle zero length array case so that if this structure is passed to a Burst Job it will be fully initialized
                    // otherwise the job system will fire the error: VAR_NAME has not been assigned or constructed. All containers must be valid when scheduling a job.
                    data = new NativeArray<Value>(0, allocator);
                    capacity = 0;
                    return;
                }

                if (capacity >= capacityRequested) { return; }

                NativeArray<Value> dataNext = new NativeArray<Value>(capacityRequested * ComputePerElementValueCount(ref schema), allocator);
                if (count > 0)
                {
                    for (int scheme = 0; scheme < schema.Length; ++scheme)
                    {
                        NativeArray<Value>.Copy(data, capacity * schema[scheme].offset, dataNext, capacityRequested * schema[scheme].offset, count * schema[scheme].stride);
                    }
                    
                    data.Dispose();
                }
                data = dataNext;
                capacity = capacityRequested;
            }

            public bool TryFromArray(int elementCountExpected, Scheme[] sourceSchema, Value[] sourceData, Allocator allocator)
            {
                int valueCount = (sourceData == null) ? 0 : sourceData.Length;
                int valueCountExpected = elementCountExpected * ComputePerElementValueCount(sourceSchema);
                if (valueCount == valueCountExpected)
                {
                    FromArray(sourceSchema, sourceData, allocator);
                    return true;
                }
                return false;
            }

            public void FromArray(Scheme[] sourceSchema, Value[] sourceData, Allocator allocator)
            {
                count = 0;
                capacity = 0;

                int sourceSchemaLength = (sourceSchema == null) ? 0 : sourceSchema.Length;
                if (schema.IsCreated && schema.Length != sourceSchemaLength) { schema.Dispose(); }
                if (sourceSchemaLength == 0)
                {
                    schema = new NativeArray<Scheme>(0, allocator);
                }
                else
                {
                    schema = new NativeArray<Scheme>(sourceSchema, allocator);
                    ComputeOffsets(ref schema);
                }

                int sourceDataLength = (sourceData == null) ? 0 : sourceData.Length;
                int perElementValueCount = ComputePerElementValueCount(sourceSchema);
                int countRequested = (perElementValueCount == 0) ? 0 : (sourceDataLength / perElementValueCount);
                Debug.Assert((countRequested * perElementValueCount) == sourceDataLength);

                Ensure(countRequested, allocator);

                for (int scheme = 0; scheme < schema.Length; ++scheme)
                {
                    NativeArray<Value>.Copy(sourceData, countRequested * schema[scheme].offset, data, capacity * schema[scheme].offset, countRequested * schema[scheme].stride);
                }

                count = countRequested;
            }

            public void ToArray(ref Scheme[] resSchema, ref Value[] resData)
            {
                if (!schema.IsCreated || schema.Length == 0)
                {
                    resSchema = null;
                    resData = null;
                    return;
                }

                if (resSchema == null || resSchema.Length != schema.Length)
                {
                    resSchema = new Scheme[schema.Length];
                }
                NativeArray<Scheme>.Copy(schema, resSchema, schema.Length);

                if (data.IsCreated && count > 0)
                {
                    int capacityRequested = count * ComputePerElementValueCount(ref schema);
                    if (resData == null || resData.Length != capacityRequested)
                    {
                        resData = new Value[capacityRequested];
                    }

                    for (int scheme = 0; scheme < schema.Length; ++scheme)
                    {
                        NativeArray<Value>.Copy(data, capacity * schema[scheme].offset, resData, count * schema[scheme].offset, count * schema[scheme].stride);
                    }
                }                
            }

            public static bool SchemaEquals(ref NativeSchemaBlob a, ref NativeSchemaBlob b)
            {
                bool memoryFootprintEquals = a.IsCreated && b.IsCreated && a.schema.Length == b.schema.Length;
                if (!memoryFootprintEquals) { return false; }

                for (int scheme = 0; scheme < a.schema.Length; ++scheme)
                {
                    if (a.schema[scheme] != b.schema[scheme])
                    {
                        return false;
                    }
                }
                return true;
            }

            public static void Copy(ref NativeSchemaBlob src, ref NativeSchemaBlob dst, int count, Allocator allocator)
            {
                Debug.Assert(count >= 0 && count <= src.count);
                dst.Clear();
                if (count == 0) { return; }
                Debug.Assert(SchemaEquals(ref src, ref dst));

                dst.Ensure(count, allocator);

                for (int scheme = 0; scheme < src.schema.Length; ++scheme)
                {
                    NativeArray<Value>.Copy(src.data, src.capacity * src.schema[scheme].offset, dst.data, dst.capacity * src.schema[scheme].offset, count * src.schema[scheme].stride);
                }

                dst.count = count;
            }

            public static void Copy(ref NativeSchemaBlob src, int srcElementIndexStart, ref NativeSchemaBlob dst, int dstElementIndexStart, int count)
            {
                Debug.Assert(count >= 0 && count <= src.count);

                if (count == 0) { return; }
                Debug.Assert(SchemaEquals(ref src, ref dst));

                Debug.Assert(dst.count >= (dstElementIndexStart + count));

                for (int scheme = 0; scheme < src.schema.Length; ++scheme)
                {
                    NativeArray<Value>.Copy(src.data, src.capacity * src.schema[scheme].offset + srcElementIndexStart * src.schema[scheme].stride, dst.data, dst.capacity * src.schema[scheme].offset + dstElementIndexStart * src.schema[scheme].stride, count * src.schema[scheme].stride);
                }
            }

            public static void Swap(ref NativeSchemaBlob src, int srcElementIndex, ref NativeSchemaBlob dst, int dstElementIndex)
            {
                Debug.Assert(srcElementIndex >= 0 && srcElementIndex <= src.count);
                Debug.Assert(dstElementIndex >= 0 && dstElementIndex <= dst.count);
                Debug.Assert(SchemaEquals(ref src, ref dst));

                for (int scheme = 0; scheme < src.schema.Length; ++scheme)
                {
                    for (int component = 0; component < src.schema[scheme].stride; ++component)
                    {
                        int srcDataIndex = src.ComputeIndex(scheme, srcElementIndex, component);
                        int dstDataIndex = dst.ComputeIndex(scheme, dstElementIndex, component);
                        (src.data[srcDataIndex], dst.data[dstDataIndex]) = (dst.data[dstDataIndex], src.data[srcDataIndex]);
                    }
                }
            }

            public void PushZero(Allocator allocator)
            {
                Ensure(count + 1, allocator);

                for (int scheme = 0; scheme < schema.Length; ++scheme)
                {
                    for (int component = 0; component < schema[scheme].stride; ++component)
                    {
                        data[ComputeIndex(scheme, count, component)] = schema[scheme].Zero();
                    }
                }
                ++count;
            }

            public void Pop()
            {
                Debug.Assert(count > 0);
                --count;
            }

            public void Set(int schemeIndex, int elementIndex, int value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Int);
                Debug.Assert(schema[schemeIndex].stride == 1);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 1) <= data.Length);
                data[dataIndexBase + 0] = new Value { i = value };
            }

            public void Set(int schemeIndex, int elementIndex, int2 value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Int);
                Debug.Assert(schema[schemeIndex].stride == 2);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 2) <= data.Length);
                data[dataIndexBase + 0] = new Value { i = value.x };
                data[dataIndexBase + 1] = new Value { i = value.y };
            }

            public void Set(int schemeIndex, int elementIndex, int3 value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Int);
                Debug.Assert(schema[schemeIndex].stride == 3);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 3) <= data.Length);
                data[dataIndexBase + 0] = new Value { i = value.x };
                data[dataIndexBase + 1] = new Value { i = value.y };
                data[dataIndexBase + 2] = new Value { i = value.z };
            }

            public void Set(int schemeIndex, int elementIndex, int4 value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Int);
                Debug.Assert(schema[schemeIndex].stride == 4);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 4) <= data.Length);
                data[dataIndexBase + 0] = new Value { i = value.x };
                data[dataIndexBase + 1] = new Value { i = value.y };
                data[dataIndexBase + 2] = new Value { i = value.z };
                data[dataIndexBase + 3] = new Value { i = value.w };
            }

            public void Set(int schemeIndex, int elementIndex, uint value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.UInt);
                Debug.Assert(schema[schemeIndex].stride == 1);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 1) <= data.Length);
                data[dataIndexBase + 0] = new Value { u = value };
            }

            public void Set(int schemeIndex, int elementIndex, uint2 value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.UInt);
                Debug.Assert(schema[schemeIndex].stride == 2);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 2) <= data.Length);
                data[dataIndexBase + 0] = new Value { u = value.x };
                data[dataIndexBase + 1] = new Value { u = value.y };
            }

            public void Set(int schemeIndex, int elementIndex, uint3 value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.UInt);
                Debug.Assert(schema[schemeIndex].stride == 3);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 3) <= data.Length);
                data[dataIndexBase + 0] = new Value { u = value.x };
                data[dataIndexBase + 1] = new Value { u = value.y };
                data[dataIndexBase + 2] = new Value { u = value.z };
            }

            public void Set(int schemeIndex, int elementIndex, uint4 value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.UInt);
                Debug.Assert(schema[schemeIndex].stride == 4);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 4) <= data.Length);
                data[dataIndexBase + 0] = new Value { u = value.x };
                data[dataIndexBase + 1] = new Value { u = value.y };
                data[dataIndexBase + 2] = new Value { u = value.z };
                data[dataIndexBase + 3] = new Value { u = value.w };
            }

            public void Set(int schemeIndex, int elementIndex, float value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Float);
                Debug.Assert(schema[schemeIndex].stride == 1);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 1) <= data.Length);
                data[dataIndexBase + 0] = new Value { f = value };
            }

            public void Set(int schemeIndex, int elementIndex, float2 value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Float);
                Debug.Assert(schema[schemeIndex].stride == 2);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 2) <= data.Length);
                data[dataIndexBase + 0] = new Value { f = value.x };
                data[dataIndexBase + 1] = new Value { f = value.y };
            }

            public void Set(int schemeIndex, int elementIndex, float3 value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Float);
                Debug.Assert(schema[schemeIndex].stride == 3);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 3) <= data.Length);
                data[dataIndexBase + 0] = new Value { f = value.x };
                data[dataIndexBase + 1] = new Value { f = value.y };
                data[dataIndexBase + 2] = new Value { f = value.z };
            }

            public void Set(int schemeIndex, int elementIndex, float4 value)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Float);
                Debug.Assert(schema[schemeIndex].stride == 4);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 4) <= data.Length);
                data[dataIndexBase + 0] = new Value { f = value.x };
                data[dataIndexBase + 1] = new Value { f = value.y };
                data[dataIndexBase + 2] = new Value { f = value.z };
                data[dataIndexBase + 3] = new Value { f = value.w };
            }

            public int GetInt(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Int);
                Debug.Assert(schema[schemeIndex].stride == 1);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 1) <= data.Length);
                return data[dataIndexBase + 0].i;
            }

            public int2 GetInt2(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Int);
                Debug.Assert(schema[schemeIndex].stride == 2);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 2) <= data.Length);
                return new int2(data[dataIndexBase + 0].i, data[dataIndexBase + 1].i);
            }

            public int3 GetInt3(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Int);
                Debug.Assert(schema[schemeIndex].stride == 3);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 3) <= data.Length);
                return new int3(data[dataIndexBase + 0].i, data[dataIndexBase + 1].i, data[dataIndexBase + 2].i);
            }

            public int4 GetInt4(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Int);
                Debug.Assert(schema[schemeIndex].stride == 4);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 4) <= data.Length);
                return new int4(data[dataIndexBase + 0].i, data[dataIndexBase + 1].i, data[dataIndexBase + 2].i, data[dataIndexBase + 3].i);
            }

            public uint GetUInt(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.UInt);
                Debug.Assert(schema[schemeIndex].stride == 1);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 1) <= data.Length);
                return data[dataIndexBase + 0].u;
            }

            public uint2 GetUInt2(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.UInt);
                Debug.Assert(schema[schemeIndex].stride == 2);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 2) <= data.Length);
                return new uint2(data[dataIndexBase + 0].u, data[dataIndexBase + 1].u);
            }

            public uint3 GetUInt3(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.UInt);
                Debug.Assert(schema[schemeIndex].stride == 3);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 3) <= data.Length);
                return new uint3(data[dataIndexBase + 0].u, data[dataIndexBase + 1].u, data[dataIndexBase + 2].u);
            }

            public uint4 GetUInt4(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.UInt);
                Debug.Assert(schema[schemeIndex].stride == 4);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 4) <= data.Length);
                return new uint4(data[dataIndexBase + 0].u, data[dataIndexBase + 1].u, data[dataIndexBase + 2].u, data[dataIndexBase + 3].u);
            }

            public float GetFloat(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Float);
                Debug.Assert(schema[schemeIndex].stride == 1);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 1) <= data.Length);
                return data[dataIndexBase + 0].f;
            }

            public float2 GetFloat2(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Float);
                Debug.Assert(schema[schemeIndex].stride == 2);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 2) <= data.Length);
                return new float2(data[dataIndexBase + 0].f, data[dataIndexBase + 1].f);
            }

            public float3 GetFloat3(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Float);
                Debug.Assert(schema[schemeIndex].stride == 3);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 3) <= data.Length);
                return new float3(data[dataIndexBase + 0].f, data[dataIndexBase + 1].f, data[dataIndexBase + 2].f);
            }

            public float4 GetFloat4(int schemeIndex, int elementIndex)
            {
                Debug.Assert(IsCreated);
                Debug.Assert(schema[schemeIndex].type == Scheme.Type.Float);
                Debug.Assert(schema[schemeIndex].stride == 4);
                Debug.Assert(count > 0);
                int dataIndexBase = ComputeIndex(schemeIndex, elementIndex, 0);
                Debug.Assert(dataIndexBase >= 0);
                Debug.Assert((dataIndexBase + 4) <= data.Length);
                return new float4(data[dataIndexBase + 0].f, data[dataIndexBase + 1].f, data[dataIndexBase + 2].f, data[dataIndexBase + 3].f);
            }

            private int ComputeIndex(int schemeIndex, int elementIndex, int componentIndex)
            {
                return capacity * schema[schemeIndex].offset + elementIndex * schema[schemeIndex].stride + componentIndex;
            }

            public string DebugString()
            {
                string res = "{";
                if (IsCreated && schema.Length > 0)
                {
                    for (int scheme = 0; scheme < schema.Length; ++scheme)
                    {
                        res += "{";
                        for (int i = 0, iLen = count; i < iLen; ++i)
                        {
                            if (schema[scheme].stride > 1)
                            {
                                res += "[";
                            }
                            for (int component = 0; component < schema[scheme].stride; ++component)
                            {
                                Value value = data[capacity * schema[scheme].offset + i * schema[scheme].stride + component];
                                res += value.ToString(schema[scheme]);
                                if ((component + 1) < schema[scheme].stride) { res += ", "; }
                            }
                            if (schema[scheme].stride > 1)
                            {
                                res += "]";
                            }

                            if ((i + 1) < count) { res += ", "; }
                        }
                        res += "}";
                    }
                }
                res += "}";
                return res;
            }

            private static void ComputeOffsets(ref NativeArray<Scheme> schema)
            {
                int offsetCurrent = 0;
                for (int i = 0; i < schema.Length; ++i)
                {
                    Debug.Assert(schema[i].type >= 0 && schema[i].type < Scheme.Type.Count);
                    Debug.Assert(schema[i].type > 0);

                    schema[i] = new Scheme()
                    {
                        type = schema[i].type,
                        stride = schema[i].stride,
                        offset = offsetCurrent
                    };

                    offsetCurrent += schema[i].stride;
                }
            }

            private static int ComputePerElementValueCount(ref NativeArray<Scheme> schema)
            {
                int count = 0;
                for (int i = 0; i < schema.Length; ++i)
                {
                    Debug.Assert(schema[i].type >= 0 && schema[i].type < Scheme.Type.Count);
                    Debug.Assert(schema[i].type > 0);

                    count += schema[i].stride;
                }
                return count;
            }

            private static int ComputePerElementValueCount(Scheme[] schema)
            {
                if (schema == null) { return 0; }

                int count = 0;
                for (int i = 0; i < schema.Length; ++i)
                {
                    Debug.Assert(schema[i].type >= 0 && schema[i].type < Scheme.Type.Count);
                    Debug.Assert(schema[i].type > 0);

                    count += schema[i].stride;
                }
                return count;
            }
        }
    }
}
