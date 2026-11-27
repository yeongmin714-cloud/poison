using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Jobs;
using Unity.Mathematics;
using TimeWarp.Core.Data;

namespace TimeWarp.Core.Jobs
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RecordTransformsJob : IJobParallelForTransform
    {
        [NativeDisableUnsafePtrRestriction]
        [ReadOnly] public TransformSnapshot** HistoryBuffers;
        [ReadOnly] public NativeArray<int> HeadIndices;
        
        [ReadOnly] public NativeArray<byte> RecordFlags; 

        public void Execute(int index, TransformAccess transform)
        {
            byte flag = RecordFlags[index];
            if (flag == 0) return; 

            int head = HeadIndices[index];
            TransformSnapshot* objectHistory = HistoryBuffers[index];

            // FIX: Fetching GLOBAL position and rotation
            if ((flag & 1) != 0) objectHistory[head].position = transform.position;
            if ((flag & 2) != 0) objectHistory[head].rotation = transform.rotation;
            if ((flag & 4) != 0) objectHistory[head].scale = transform.localScale;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public unsafe struct ApplyTransformsJob : IJobParallelForTransform
    {
        [NativeDisableUnsafePtrRestriction]
        [ReadOnly] public TransformSnapshot** HistoryBuffers;

        [ReadOnly] public NativeArray<int> ReadIndicesA; 
        [ReadOnly] public NativeArray<int> ReadIndicesB; 
        [ReadOnly] public NativeArray<float> LerpFactors; 
        
        [ReadOnly] public NativeArray<byte> ApplyFlags;

        public void Execute(int index, TransformAccess transform)
        {
            byte flag = ApplyFlags[index];
            if (flag == 0) return; 

            int idxA = ReadIndicesA[index];
            int idxB = ReadIndicesB[index];
            float t = LerpFactors[index];

            TransformSnapshot* history = HistoryBuffers[index];
            TransformSnapshot snapA = history[idxA];
            TransformSnapshot snapB = history[idxB];

            bool useLerp = (flag & 8) != 0;

            if (useLerp)
            {
                // FIX: Applying GLOBAL position and rotation
                if ((flag & 1) != 0) transform.position = math.lerp(snapA.position, snapB.position, t);
                if ((flag & 2) != 0) transform.rotation = math.slerp(snapA.rotation, snapB.rotation, t);
                if ((flag & 4) != 0) transform.localScale = math.lerp(snapA.scale, snapB.scale, t);
            }
            else 
            {
                // Hard snap to frame A
                if ((flag & 1) != 0) transform.position = snapA.position;
                if ((flag & 2) != 0) transform.rotation = snapA.rotation;
                if ((flag & 4) != 0) transform.localScale = snapA.scale;
            }
        }
    }
}