using NUnit.Framework;
using UnityEngine;
using TimeWarp.Core.Data;

namespace TimeWarp.Tests
{
    /// <summary>
    /// Edit Mode tests for the circular buffer arithmetic.
    ///
    /// This is the one piece of the engine where a regression would be genuinely hard to
    /// notice by eye: an off-by-one in the bitmask indexing does not throw, it just makes
    /// a rewind read one frame off, which shows up as "the rewind flickers sometimes".
    /// These tests pin the wrap-around behaviour so a future change to the indexing has
    /// to break a test rather than a game.
    ///
    /// No scene, no PlayMode, no manager required - the buffer is pure arithmetic over a
    /// NativeArray. Every test disposes its buffer to avoid leaking native memory, which
    /// the Editor would otherwise report as a leak on domain reload.
    /// </summary>
    public class TimeWarpBufferTests
    {
        private TimeWarpBuffer _buffer;

        [TearDown]
        public void TearDown()
        {
            if (_buffer != null && !_buffer.IsDisposed) _buffer.Dispose();
            _buffer = null;
        }

        // ==========================================
        // SIZING
        // ==========================================

        [Test]
        public void Constructor_RoundsCountUpToPowerOfTwo()
        {
            // 50 fps * 10 s = 500 raw frames -> 512
            _buffer = new TimeWarpBuffer(50, 10f);

            Assert.AreEqual(512, _buffer.Count);
        }

        [Test]
        public void Constructor_CountIsNeverSmallerThanRequested()
        {
            // 60 fps * 12 s = 720 raw frames -> 1024 (this is the rounding the inspector warns about)
            _buffer = new TimeWarpBuffer(60, 12f);

            Assert.GreaterOrEqual(_buffer.Count, 720);
            Assert.AreEqual(1024, _buffer.Count);
        }

        [Test]
        public void Constructor_CountIsAlwaysAPowerOfTwo()
        {
            // The bitmask indexing is only correct for power-of-two sizes.
            foreach (int fps in new[] { 24, 30, 50, 60, 90, 120 })
            {
                var buffer = new TimeWarpBuffer(fps, 7.3f);

                int count = buffer.Count;
                Assert.IsTrue(count > 0 && (count & (count - 1)) == 0,
                    $"Count {count} for {fps} fps is not a power of two - bitmask indexing would break.");

                buffer.Dispose();
            }
        }

        [Test]
        public void Constructor_AllocatesTransformHistory()
        {
            _buffer = new TimeWarpBuffer(50, 1f);

            Assert.IsTrue(_buffer.TransformHistory.IsCreated);
            Assert.AreEqual(_buffer.Count, _buffer.TransformHistory.Length);
        }

        // ==========================================
        // LAZY VELOCITY BUFFERS
        // ==========================================

        [Test]
        public void VelocityHistory_IsNotAllocatedUntilTouched()
        {
            _buffer = new TimeWarpBuffer(50, 1f);

            Assert.IsFalse(_buffer.HasVelocityHistory);
            Assert.IsFalse(_buffer.HasVelocity2DHistory);
        }

        [Test]
        public void VelocityHistory_AllocatesOnFirstAccessWithCorrectLength()
        {
            _buffer = new TimeWarpBuffer(50, 1f);

            var history = _buffer.VelocityHistory;

            Assert.IsNotNull(history);
            Assert.AreEqual(_buffer.Count, history.Length);
            Assert.IsTrue(_buffer.HasVelocityHistory);

            // Still no 2D allocation - the two channels are independent.
            Assert.IsFalse(_buffer.HasVelocity2DHistory);
        }

        [Test]
        public void VelocityHistory_ReturnsTheSameInstanceOnRepeatedAccess()
        {
            _buffer = new TimeWarpBuffer(50, 1f);

            Assert.AreSame(_buffer.VelocityHistory, _buffer.VelocityHistory);
        }

        // ==========================================
        // HEAD ADVANCE / WRAP-AROUND
        // ==========================================

        [Test]
        public void AdvanceHead_IncrementsByOne()
        {
            _buffer = new TimeWarpBuffer(50, 1f);
            int start = _buffer.Head;

            _buffer.AdvanceHead();

            Assert.AreEqual(start + 1, _buffer.Head);
        }

        [Test]
        public void AdvanceHead_WrapsBackToZeroAfterCountSteps()
        {
            _buffer = new TimeWarpBuffer(50, 1f);
            int count = _buffer.Count;

            for (int i = 0; i < count; i++) _buffer.AdvanceHead();

            Assert.AreEqual(0, _buffer.Head, "A full lap around the circular buffer must return to the start.");
        }

        // ==========================================
        // GetIndex
        // ==========================================

        [Test]
        public void GetIndex_ZeroOffsetReturnsMostRecentlyWrittenSlot()
        {
            _buffer = new TimeWarpBuffer(50, 1f);

            // Simulate writing one frame: write at Head, then advance.
            int written = _buffer.Head;
            _buffer.AdvanceHead();

            Assert.AreEqual(written, _buffer.GetIndex(0));
        }

        [Test]
        public void GetIndex_WrapsCorrectlyWhenHeadIsAtZero()
        {
            // This is the case a naive implementation gets wrong: Head - 1 - offset goes
            // negative. The bitmask relies on two's complement to wrap it into range.
            _buffer = new TimeWarpBuffer(50, 1f);
            int count = _buffer.Count;

            Assert.AreEqual(0, _buffer.Head, "Precondition: buffer starts at head 0.");

            Assert.AreEqual(count - 1, _buffer.GetIndex(0));
            Assert.AreEqual(count - 2, _buffer.GetIndex(1));
        }

        [Test]
        public void GetIndex_IsAlwaysInsideTheBuffer()
        {
            _buffer = new TimeWarpBuffer(50, 2f);
            int count = _buffer.Count;

            // Sweep several head positions and a wide offset range, including offsets
            // larger than the buffer itself.
            for (int step = 0; step < 5; step++)
            {
                for (int offset = 0; offset < count * 2; offset++)
                {
                    int index = _buffer.GetIndex(offset);
                    Assert.IsTrue(index >= 0 && index < count,
                        $"GetIndex({offset}) returned {index}, outside [0,{count}).");
                }

                for (int i = 0; i < 37; i++) _buffer.AdvanceHead();
            }
        }

        [Test]
        public void GetIndex_OffsetOfCountReturnsTheSameSlot()
        {
            _buffer = new TimeWarpBuffer(50, 1f);
            int count = _buffer.Count;

            Assert.AreEqual(_buffer.GetIndex(0), _buffer.GetIndex(count),
                "Offsetting by a full buffer length must land on the same slot.");
        }

        // ==========================================
        // GetFractionalIndex
        // ==========================================

        [Test]
        public void GetFractionalIndex_WholeOffsetGivesZeroBlend()
        {
            _buffer = new TimeWarpBuffer(50, 1f);
            for (int i = 0; i < 10; i++) _buffer.AdvanceHead();

            _buffer.GetFractionalIndex(3f, out int a, out int b, out float t);

            Assert.AreEqual(_buffer.GetIndex(3), a);
            Assert.AreEqual(_buffer.GetIndex(4), b);
            Assert.AreEqual(0f, t, 0.0001f);
        }

        [Test]
        public void GetFractionalIndex_BlendFactorStaysInUnitRange()
        {
            _buffer = new TimeWarpBuffer(50, 1f);
            for (int i = 0; i < 10; i++) _buffer.AdvanceHead();

            for (float offset = 0f; offset < 8f; offset += 0.137f)
            {
                _buffer.GetFractionalIndex(offset, out _, out _, out float t);

                Assert.IsTrue(t >= 0f && t < 1f, $"Blend factor {t} for offset {offset} is outside [0,1).");
            }
        }

        [Test]
        public void GetFractionalIndex_IndicesAreAdjacentFrames()
        {
            _buffer = new TimeWarpBuffer(50, 1f);
            for (int i = 0; i < 20; i++) _buffer.AdvanceHead();

            _buffer.GetFractionalIndex(2.5f, out int a, out int b, out float t);

            Assert.AreEqual(0.5f, t, 0.0001f);
            // b is one frame further into the past than a, so it sits one slot lower
            // (modulo the buffer length).
            Assert.AreEqual(a - 1 < 0 ? _buffer.Count - 1 : a - 1, b);
        }

        // ==========================================
        // CommitRewind
        // ==========================================

        [Test]
        public void CommitRewind_MovesHeadBackByTheGivenFrameCount()
        {
            _buffer = new TimeWarpBuffer(50, 1f);
            for (int i = 0; i < 100; i++) _buffer.AdvanceHead();

            int headBefore = _buffer.Head;
            _buffer.CommitRewind(10);

            // CommitRewind sets Head to GetIndex(10) and then advances once,
            // so the net movement is 10 frames back.
            Assert.AreEqual(headBefore - 10, _buffer.Head);
        }

        [Test]
        public void CommitRewind_WrapsCorrectlyAcrossTheBufferStart()
        {
            _buffer = new TimeWarpBuffer(50, 1f);
            int count = _buffer.Count;

            for (int i = 0; i < 5; i++) _buffer.AdvanceHead(); // Head = 5

            _buffer.CommitRewind(10); // rewinding past slot 0

            Assert.AreEqual(count - 5, _buffer.Head);
            Assert.IsTrue(_buffer.Head >= 0 && _buffer.Head < count);
        }

        [Test]
        public void CommitRewind_ZeroFramesLeavesHeadUnchanged()
        {
            _buffer = new TimeWarpBuffer(50, 1f);
            for (int i = 0; i < 42; i++) _buffer.AdvanceHead();

            int headBefore = _buffer.Head;
            _buffer.CommitRewind(0);

            Assert.AreEqual(headBefore, _buffer.Head);
        }

        // ==========================================
        // DISPOSAL
        // ==========================================

        [Test]
        public void Dispose_ReleasesNativeMemoryAndSetsFlag()
        {
            _buffer = new TimeWarpBuffer(50, 1f);

            _buffer.Dispose();

            Assert.IsTrue(_buffer.IsDisposed);
            Assert.IsFalse(_buffer.TransformHistory.IsCreated);
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            _buffer = new TimeWarpBuffer(50, 1f);

            _buffer.Dispose();

            // Disposing a NativeArray twice throws, so the guard inside Dispose matters.
            Assert.DoesNotThrow(() => _buffer.Dispose());
        }

        [Test]
        public void Dispose_DropsManagedVelocityArrays()
        {
            _buffer = new TimeWarpBuffer(50, 1f);
            _ = _buffer.VelocityHistory; // force allocation
            Assert.IsTrue(_buffer.HasVelocityHistory);

            _buffer.Dispose();

            Assert.IsFalse(_buffer.HasVelocityHistory,
                "Dispose should drop managed references so the GC can reclaim them promptly.");
        }
    }
}
